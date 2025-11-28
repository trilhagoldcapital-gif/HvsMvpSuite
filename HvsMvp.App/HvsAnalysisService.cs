using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace HvsMvp.App
{
    /// <summary>
    /// Core HVS analysis service.
    /// - Uses SampleMaskService to separate sample from background.
    /// - Classifies each sample pixel as Metal / Crystal / Gem using HSV ranges from HvsConfig.
    /// - Generates global statistics (SampleFullAnalysisResult).
    /// - Generates a LabelMap (PixelLabel[,]) with per-pixel classification.
    /// 
    /// Special focus on Gold (Au) vs Platinum (Pt) differentiation using robust heuristics.
    /// </summary>
    public class HvsAnalysisService
    {
        private readonly HvsConfig _config;

        private record MatEntry(
            string Id, string Name, string Group, int Type,
            (double Min, double Max) H, bool HWrap,
            (double Min, double Max) S, (double Min, double Max) V,
            double HMid, double SHalf, double VHalf
        );

        private MatEntry[] _metals = Array.Empty<MatEntry>();
        private MatEntry[] _crystals = Array.Empty<MatEntry>();
        private MatEntry[] _gems = Array.Empty<MatEntry>();
        
        // Index cache for quick lookup
        private int _auIndex = -1;
        private int _ptIndex = -1;

        // ===== CONFIGURABLE THRESHOLDS =====
        
        /// <summary>Minimum saturation to consider pixel for classification.</summary>
        public double MinSaturation { get; set; } = 0.05;
        
        /// <summary>Minimum value (brightness) to consider pixel for classification.</summary>
        public double MinValue { get; set; } = 0.08;
        
        /// <summary>Maximum value to avoid saturated white pixels.</summary>
        public double MaxValue { get; set; } = 0.98;
        
        /// <summary>Minimum score for pixel classification.</summary>
        public double MinScorePixel { get; set; } = 0.45;
        
        /// <summary>Gold heuristic boost score.</summary>
        public double GoldBoostScore { get; set; } = 0.85;
        
        /// <summary>PGM heuristic boost score.</summary>
        public double PgmBoostScore { get; set; } = 0.70;

        public HvsAnalysisService(HvsConfig config)
        {
            _config = config;
            _metals = BuildEntries(_config.Materials?.Metais, 0);
            _crystals = BuildEntries(_config.Materials?.Cristais, 1);
            _gems = BuildEntries(_config.Materials?.Gemas, 2);
            
            // Cache indices for Au and Pt for quick lookup
            _auIndex = Array.FindIndex(_metals, m => m.Id.Equals("Au", StringComparison.OrdinalIgnoreCase));
            _ptIndex = Array.FindIndex(_metals, m => m.Id.Equals("Pt", StringComparison.OrdinalIgnoreCase));
        }

        private MatEntry[] BuildEntries(IEnumerable<HvsMaterial>? list, int type)
        {
            if (list == null) return Array.Empty<MatEntry>();
            
            // Use dictionary to deduplicate by ID (keep first occurrence with valid HSV)
            var seen = new Dictionary<string, MatEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in list)
            {
                if (string.IsNullOrWhiteSpace(m.Id)) continue;
                if (seen.ContainsKey(m.Id)) continue; // Skip duplicates
                if (!TryGetHsvRangeNormalized(m, out var h, out var s, out var v, out bool hWrap)) continue;

                double hMid = MidHue(h.Min, h.Max, hWrap);
                double sHalf = Math.Max(1e-6, (s.Max - s.Min) / 2.0);
                double vHalf = Math.Max(1e-6, (v.Max - v.Min) / 2.0);

                seen[m.Id] = new MatEntry(
                    Id: m.Id!,
                    Name: m.Nome ?? m.Id!,
                    Group: m.Grupo ?? "",
                    Type: type,
                    H: h, HWrap: hWrap,
                    S: s, V: v,
                    HMid: hMid, SHalf: sHalf, VHalf: vHalf
                );
            }

            return seen.Values.ToArray();
        }

        /// <summary>
        /// Run full analysis and return tuple with result, mask, and preview.
        /// </summary>
        public (SampleFullAnalysisResult analysis, SampleMaskClass?[,] mask, Bitmap maskPreview)
            RunFullAnalysis(Bitmap bmp, string? imagePath)
        {
            var scene = AnalyzeScene(bmp, imagePath);
            return (scene.Summary, scene.Mask, scene.MaskPreview);
        }

        /// <summary>
        /// Analyze a complete scene/image and return full analysis results.
        /// </summary>
        public FullSceneAnalysis AnalyzeScene(Bitmap bmp, string? imagePath)
        {
            using var src24 = Ensure24bpp(bmp);

            var maskService = new SampleMaskService();
            var (mask, maskPreview) = maskService.BuildMask(src24);

            int w = src24.Width, h = src24.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = src24.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            long sampleCount = 0;
            long[] metalCounts = new long[_metals.Length];
            long[] crystalCounts = new long[_crystals.Length];
            long[] gemCounts = new long[_gems.Length];

            long diagTotal = 0;
            long diagClip = 0;
            double gradSum = 0;

            var labels = new PixelLabel[w, h];

            try
            {
                int stride = data.Stride;
                int bytes = stride * h;
                byte[] buf = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);

                object lockAgg = new object();

                System.Threading.Tasks.Parallel.For(
                    0, h,
                    () => new LocalAcc(_metals.Length, _crystals.Length, _gems.Length),
                    (y, loop, acc) =>
                    {
                        int row = y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            acc.Total++;

                            var lbl = new PixelLabel();
                            labels[x, y] = lbl;

                            // Default: background
                            lbl.IsSample = false;
                            lbl.MaterialType = PixelMaterialType.Background;
                            lbl.MaterialId = null;
                            lbl.MaterialConfidence = 0;
                            lbl.RawScore = 0;

                            var mc = mask[x, y];
                            if (mc == null || !mc.IsSample)
                                continue;

                            lbl.IsSample = true;
                            lbl.MaterialType = PixelMaterialType.None;

                            System.Threading.Interlocked.Increment(ref sampleCount);

                            int off = row + x * 3;
                            byte B = buf[off + 0], G = buf[off + 1], R = buf[off + 2];

                            int gray = (int)(0.299 * R + 0.587 * G + 0.114 * B);
                            if (gray < 5 || gray > 250) acc.Clip++;

                            // Calculate gradient for focus score
                            if (x > 0 && x < w - 1 && y > 0 && y < h - 1)
                            {
                                int offL = row + (x - 1) * 3;
                                int offR = row + (x + 1) * 3;
                                int offU = (y - 1) * stride + x * 3;
                                int offD = (y + 1) * stride + x * 3;

                                int grayL = (int)(0.299 * buf[offL + 2] + 0.587 * buf[offL + 1] + 0.114 * buf[offL + 0]);
                                int grayR = (int)(0.299 * buf[offR + 2] + 0.587 * buf[offR + 1] + 0.114 * buf[offR + 0]);
                                int grayU = (int)(0.299 * buf[offU + 2] + 0.587 * buf[offU + 1] + 0.114 * buf[offU + 0]);
                                int grayD = (int)(0.299 * buf[offD + 2] + 0.587 * buf[offD + 1] + 0.114 * buf[offD + 0]);

                                int gx = grayR - grayL;
                                int gy = grayD - grayU;
                                acc.GradSum += (gx * gx + gy * gy);
                            }

                            RgbToHsvFast(R, G, B, out double H, out double S, out double V);
                            lbl.H = H;
                            lbl.S = S;
                            lbl.V = V;

                            // Apply heuristics FIRST to determine if this is clearly gold or PGM
                            bool looksLikeGold = LooksLikeGold(R, G, B, H, S, V);
                            bool looksLikePgm = LooksLikePgm(R, G, B, H, S, V);

                            double bestScore = 0;
                            int bestType = -1;
                            int bestIndex = -1;

                            // If gold heuristic fires strongly, prioritize Au
                            if (looksLikeGold && _auIndex >= 0)
                            {
                                bestScore = GoldBoostScore;
                                bestType = 0;
                                bestIndex = _auIndex;
                                
                                // Still evaluate other materials but with gold priority
                                double altScore = 0;
                                int altType = -1, altIndex = -1;
                                EvaluateListExcluding(H, S, V, _metals, ref altScore, ref altType, ref altIndex, 0, _auIndex);
                                
                                // Only override if alternative is significantly better
                                if (altScore > bestScore + 0.2)
                                {
                                    bestScore = altScore;
                                    bestType = altType;
                                    bestIndex = altIndex;
                                }
                            }
                            // If PGM heuristic fires and NOT gold, prioritize Pt
                            else if (looksLikePgm && !looksLikeGold && _ptIndex >= 0)
                            {
                                bestScore = PgmBoostScore;
                                bestType = 0;
                                bestIndex = _ptIndex;
                                
                                // Evaluate alternatives
                                double altScore = 0;
                                int altType = -1, altIndex = -1;
                                EvaluateList(H, S, V, _crystals, ref altScore, ref altType, ref altIndex, 1);
                                EvaluateList(H, S, V, _gems, ref altScore, ref altType, ref altIndex, 2);
                                
                                // Only override if alternative is significantly better
                                if (altScore > bestScore + 0.15)
                                {
                                    bestScore = altScore;
                                    bestType = altType;
                                    bestIndex = altIndex;
                                }
                            }
                            else
                            {
                                // Standard evaluation - check all materials
                                if (S >= MinSaturation && V >= MinValue && V <= MaxValue)
                                {
                                    EvaluateList(H, S, V, _metals, ref bestScore, ref bestType, ref bestIndex, 0);
                                    EvaluateList(H, S, V, _crystals, ref bestScore, ref bestType, ref bestIndex, 1);
                                    EvaluateList(H, S, V, _gems, ref bestScore, ref bestType, ref bestIndex, 2);
                                }
                            }

                            // Apply classification if score is high enough
                            if (bestScore >= MinScorePixel && bestIndex >= 0)
                            {
                                lbl.RawScore = bestScore;
                                lbl.MaterialConfidence = Math.Min(1.0, bestScore);

                                if (bestType == 0 && bestIndex < _metals.Length)
                                {
                                    acc.MetalCounts[bestIndex]++;
                                    lbl.MaterialType = PixelMaterialType.Metal;
                                    lbl.MaterialId = _metals[bestIndex].Id;
                                }
                                else if (bestType == 1 && bestIndex < _crystals.Length)
                                {
                                    acc.CrystalCounts[bestIndex]++;
                                    lbl.MaterialType = PixelMaterialType.Crystal;
                                    lbl.MaterialId = _crystals[bestIndex].Id;
                                }
                                else if (bestType == 2 && bestIndex < _gems.Length)
                                {
                                    acc.GemCounts[bestIndex]++;
                                    lbl.MaterialType = PixelMaterialType.Gem;
                                    lbl.MaterialId = _gems[bestIndex].Id;
                                }
                            }
                        }

                        return acc;
                    },
                    acc =>
                    {
                        lock (lockAgg)
                        {
                            diagTotal += acc.Total;
                            diagClip += acc.Clip;
                            gradSum += acc.GradSum;
                            for (int i = 0; i < metalCounts.Length; i++) metalCounts[i] += acc.MetalCounts[i];
                            for (int i = 0; i < crystalCounts.Length; i++) crystalCounts[i] += acc.CrystalCounts[i];
                            for (int i = 0; i < gemCounts.Length; i++) gemCounts[i] += acc.GemCounts[i];
                        }
                    });
            }
            finally
            {
                src24.UnlockBits(data);
            }

            // Calculate focus score
            double focus = 0;
            if (sampleCount > 0)
            {
                focus = gradSum / sampleCount / (255.0 * 255.0);
                focus = Math.Min(1.0, focus * 10); // Scale up for better visibility
            }

            var diag = new ImageDiagnosticsResult
            {
                FocusScore = focus,
                SaturationClippingFraction = diagTotal > 0 ? (double)diagClip / diagTotal : 0,
                ForegroundFraction = (double)sampleCount / Math.Max(1, w * h)
            };

            var result = new SampleFullAnalysisResult
            {
                Id = Guid.NewGuid(),
                ImagePath = imagePath,
                CaptureDateTimeUtc = DateTime.UtcNow,
                Diagnostics = diag
            };

            long denom = Math.Max(1, sampleCount);

            // Build metal results
            for (int i = 0; i < _metals.Length; i++)
            {
                double pct = (double)metalCounts[i] / denom;
                result.Metals.Add(new MetalResult
                {
                    Id = _metals[i].Id,
                    Name = _metals[i].Name,
                    Group = _metals[i].Group,
                    PctSample = pct,
                    PpmEstimated = pct > 0 ? pct * 1_000_000.0 : null,
                    Score = CalculateMetalScore(pct, _metals[i].Group)
                });
            }

            // Build crystal results
            for (int i = 0; i < _crystals.Length; i++)
            {
                double pct = (double)crystalCounts[i] / denom;
                result.Crystals.Add(new CrystalResult
                {
                    Id = _crystals[i].Id,
                    Name = _crystals[i].Name,
                    PctSample = pct,
                    Score = Math.Max(0, Math.Min(1.0, pct * 5))
                });
            }

            // Build gem results
            for (int i = 0; i < _gems.Length; i++)
            {
                double pct = (double)gemCounts[i] / denom;
                result.Gems.Add(new GemResult
                {
                    Id = _gems[i].Id,
                    Name = _gems[i].Name,
                    PctSample = pct,
                    Score = Math.Max(0, Math.Min(1.0, pct * 8))
                });
            }

            result.ShortReport = BuildShortReport(result);

            return new FullSceneAnalysis
            {
                Summary = result,
                Labels = labels,
                Mask = mask,
                MaskPreview = (Bitmap)maskPreview.Clone(),
                Width = w,
                Height = h
            };
        }

        /// <summary>
        /// Calculate metal score with group-specific weighting.
        /// Noble metals and PGM get higher scores for small percentages.
        /// </summary>
        private double CalculateMetalScore(double pct, string group)
        {
            double multiplier = 10.0;
            
            if (!string.IsNullOrEmpty(group))
            {
                string g = group.ToLowerInvariant();
                if (g.Contains("nobre") || g.Contains("noble"))
                    multiplier = 15.0;
                else if (g.Contains("pgm"))
                    multiplier = 12.0;
            }
            
            return Math.Max(0, Math.Min(1.0, pct * multiplier));
        }

        private class LocalAcc
        {
            public long Total;
            public long Clip;
            public double GradSum;
            public long[] MetalCounts;
            public long[] CrystalCounts;
            public long[] GemCounts;

            public LocalAcc(int m, int c, int g)
            {
                MetalCounts = new long[m];
                CrystalCounts = new long[c];
                GemCounts = new long[g];
            }
        }

        private static Bitmap Ensure24bpp(Bitmap src)
        {
            if (src.PixelFormat == PixelFormat.Format24bppRgb)
                return (Bitmap)src.Clone();

            var clone = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(clone);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
            return clone;
        }

        private static void RgbToHsvFast(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            v = max;
            double delta = max - min;
            s = max == 0 ? 0 : delta / max;

            if (delta == 0) { h = 0; return; }
            double hue;
            if (max == rd) hue = 60 * (((gd - bd) / delta) % 6);
            else if (max == gd) hue = 60 * (((bd - rd) / delta) + 2);
            else hue = 60 * (((rd - gd) / delta) + 4);
            if (hue < 0) hue += 360;
            h = hue;
        }

        private void EvaluateList(double H, double S, double V, MatEntry[] arr, ref double bestScore, ref int bestType, ref int bestIndex, int typeVal)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                var e = arr[i];
                if (!InHueRange(H, e.H, e.HWrap)) continue;
                if (S < e.S.Min || S > e.S.Max) continue;
                if (V < e.V.Min || V > e.V.Max) continue;

                double sh = HueScore(H, e.H, e.HWrap, e.HMid);
                double ss = RangeScore(S, e.S, e.SHalf);
                double sv = RangeScore(V, e.V, e.VHalf);
                double score = (sh + ss + sv) / 3.0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestType = typeVal;
                    bestIndex = i;
                }
            }
        }
        
        private void EvaluateListExcluding(double H, double S, double V, MatEntry[] arr, ref double bestScore, ref int bestType, ref int bestIndex, int typeVal, int excludeIndex)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (i == excludeIndex) continue;
                
                var e = arr[i];
                if (!InHueRange(H, e.H, e.HWrap)) continue;
                if (S < e.S.Min || S > e.S.Max) continue;
                if (V < e.V.Min || V > e.V.Max) continue;

                double sh = HueScore(H, e.H, e.HWrap, e.HMid);
                double ss = RangeScore(S, e.S, e.SHalf);
                double sv = RangeScore(V, e.V, e.VHalf);
                double score = (sh + ss + sv) / 3.0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestType = typeVal;
                    bestIndex = i;
                }
            }
        }

        private static bool InHueRange(double h, (double Min, double Max) r, bool wrap)
        {
            if (!wrap) return h >= r.Min && h <= r.Max;
            return (h >= r.Min) || (h <= r.Max);
        }

        private static double HueScore(double h, (double Min, double Max) r, bool wrap, double hMid)
        {
            double halfSpan = !wrap ? ((r.Max - r.Min) / 2.0) : (((360 - r.Min) + r.Max) / 2.0);
            if (halfSpan <= 0) halfSpan = 1;
            double d = AngularDistance(h, hMid);
            double norm = Math.Max(0, 1 - (d / halfSpan));
            return Math.Min(1, norm);
        }

        private static double AngularDistance(double a, double b)
        {
            double d = Math.Abs(a - b) % 360.0;
            return d > 180 ? 360 - d : d;
        }

        private static double RangeScore(double v, (double Min, double Max) r, double half)
        {
            if (v < r.Min || v > r.Max) return 0;
            double mid = (r.Min + r.Max) / 2.0;
            if (half <= 0) half = (r.Max - r.Min) / 2.0;
            if (half <= 0) return 1;
            double dist = Math.Abs(v - mid) / half;
            return Math.Max(0, 1 - dist);
        }

        private static double MidHue(double min, double max, bool wrap)
        {
            if (!wrap) return (min + max) / 2.0;
            double span = (360 - min) + max;
            double half = span / 2.0;
            return (min + half) % 360.0;
        }

        private bool TryGetHsvRangeNormalized(HvsMaterial mat,
            out (double Min, double Max) hRange, out (double Min, double Max) sRange, out (double Min, double Max) vRange, out bool hWrap)
        {
            hRange = (0, 0); sRange = (0, 0); vRange = (0, 0); hWrap = false;

            if (mat.Optico == null) return false;
            if (!mat.Optico.TryGetValue("cor_hsv", out var hsvObj) || hsvObj == null) return false;

            try
            {
                var json = JsonSerializer.Serialize(hsvObj);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var h = ReadRange(root, "h", 0, 360);
                var s = ReadRange(root, "s", 0, 1);
                var v = ReadRange(root, "v", 0, 1);

                // Normalize saturation and value if they appear to be percentages
                if (s.Max > 1.0 || s.Min > 1.0) { s = (s.Min / 100.0, s.Max / 100.0); }
                if (v.Max > 1.0 || v.Min > 1.0) { v = (v.Min / 100.0, v.Max / 100.0); }

                hWrap = h.Min > h.Max;
                hRange = h; sRange = s; vRange = v;
                return true;
            }
            catch { return false; }
        }

        private (double Min, double Max) ReadRange(JsonElement root, string name, double defMin, double defMax)
        {
            if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return (defMin, defMax);
            if (arr.GetArrayLength() != 2) return (defMin, defMax);
            double min = arr[0].GetDouble();
            double max = arr[1].GetDouble();
            return (min, max);
        }

        private string BuildShortReport(SampleFullAnalysisResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("       RESUMO DA ANÁLISE HVS-MVP");
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Data/Hora (UTC): {r.CaptureDateTimeUtc:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"ID da Análise:   {r.Id}");
            sb.AppendLine();
            sb.AppendLine("─── DIAGNÓSTICOS ───");
            sb.AppendLine($"  Foco (0..1):       {r.Diagnostics.FocusScore:F3}");
            sb.AppendLine($"  Clipping:          {r.Diagnostics.SaturationClippingFraction:P2}");
            sb.AppendLine($"  Fração amostra:    {r.Diagnostics.ForegroundFraction:P2}");
            sb.AppendLine();
            
            var topMetals = r.Metals.Where(m => m.PctSample > 0).OrderByDescending(m => m.PctSample).Take(5).ToList();
            sb.AppendLine("─── METAIS (top 5) ───");
            if (topMetals.Count == 0)
            {
                sb.AppendLine("  (nenhum metal detectado)");
            }
            else
            {
                foreach (var m in topMetals)
                {
                    var ppm = m.PpmEstimated.HasValue ? $"{m.PpmEstimated.Value:F0} ppm" : "-";
                    string groupTag = !string.IsNullOrEmpty(m.Group) ? $"[{m.Group}]" : "";
                    sb.AppendLine($"  • {m.Name} ({m.Id}) {groupTag}");
                    sb.AppendLine($"      {m.PctSample:P4} · {ppm} · score={m.Score:F2}");
                }
            }
            sb.AppendLine();
            
            var topCrystals = r.Crystals.Where(c => c.PctSample > 0).OrderByDescending(c => c.PctSample).Take(3).ToList();
            sb.AppendLine("─── CRISTAIS (top 3) ───");
            if (topCrystals.Count == 0)
            {
                sb.AppendLine("  (nenhum cristal detectado)");
            }
            else
            {
                foreach (var c in topCrystals)
                {
                    sb.AppendLine($"  • {c.Name} ({c.Id}): {c.PctSample:P4} · score={c.Score:F2}");
                }
            }
            sb.AppendLine();
            
            var topGems = r.Gems.Where(g => g.PctSample > 0).OrderByDescending(g => g.PctSample).Take(3).ToList();
            sb.AppendLine("─── GEMAS (top 3) ───");
            if (topGems.Count == 0)
            {
                sb.AppendLine("  (nenhuma gema detectada)");
            }
            else
            {
                foreach (var g in topGems)
                {
                    sb.AppendLine($"  • {g.Name} ({g.Id}): {g.PctSample:P4} · score={g.Score:F2}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════");
            
            return sb.ToString();
        }

        // ===== GOLD AND PGM HEURISTICS =====

        /// <summary>
        /// Determine if a pixel looks like gold based on RGB and HSV values.
        /// Gold typically has:
        /// - Hue in yellow range (40-70 degrees)
        /// - Moderate to high saturation
        /// - High brightness
        /// - R and G channels significantly higher than B
        /// - Warm, yellow-golden appearance
        /// </summary>
        private static bool LooksLikeGold(byte R, byte G, byte B, double H, double S, double V)
        {
            // Hue must be in yellow/gold range (expanded range for variations)
            if (H < 35 || H > 75) return false;
            
            // Saturation must indicate color presence (not grayscale)
            if (S < 0.15) return false;
            
            // Brightness must be reasonable
            if (V < 0.25 || V > 0.98) return false;
            
            // Gold has warm tones: R+G should dominate over B
            double avgRG = (R + G) / 2.0;
            if (avgRG <= B + 10) return false; // Must have warmer tones
            
            // R and G should be relatively close (not too red or too green)
            double diffRG = Math.Abs(R - G);
            if (diffRG > 60) return false; // Too unbalanced
            
            // Minimum brightness in absolute terms
            if (R < 100 && G < 80) return false;
            
            // Additional check: gold tends to have high R and G relative to B
            if (R < B * 1.2 || G < B * 1.1) return false;
            
            return true;
        }

        /// <summary>
        /// Determine if a pixel looks like platinum group metal (PGM).
        /// PGM metals typically have:
        /// - Very low saturation (grayish metallic)
        /// - Moderate to high brightness
        /// - R, G, B channels close to each other (neutral gray)
        /// </summary>
        private static bool LooksLikePgm(byte R, byte G, byte B, double H, double S, double V)
        {
            // PGM has very low saturation (grayish)
            if (S > 0.20) return false;
            
            // Brightness should be moderate to high (metallic sheen)
            if (V < 0.20 || V > 0.95) return false;
            
            // R, G, B should be close (neutral gray)
            int max = Math.Max(R, Math.Max(G, B));
            int min = Math.Min(R, Math.Min(G, B));
            if (max - min > 40) return false; // Too much color variation
            
            // Avoid pure white
            if (max > 250 && min > 240) return false;
            
            // Avoid very dark pixels (likely shadows)
            if (max < 60) return false;
            
            return true;
        }
    }
}