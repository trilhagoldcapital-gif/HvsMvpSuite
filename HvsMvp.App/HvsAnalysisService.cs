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
    /// Núcleo de análise HVS.
    /// - Usa SampleMaskService para separar amostra/fundo.
    /// - Classifica cada pixel da amostra em Metal / Cristal / Gema usando faixas cor_hsv do HvsConfig.
    /// - Gera estatísticas globais (SampleFullAnalysisResult).
    /// - Gera um LabelMap (PixelLabel[,]) com a "verdade" por pixel.
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

        // Limiar anti-ruído e confiança
        private readonly double _minSat = 0.08;
        private readonly double _minVal = 0.06;
        private readonly double _maxVal = 0.97;
        private readonly double _minScorePixel = 0.55;

        public HvsAnalysisService(HvsConfig config)
        {
            _config = config;
            _metals = BuildEntries(_config.Materials?.Metais, 0);
            _crystals = BuildEntries(_config.Materials?.Cristais, 1);
            _gems = BuildEntries(_config.Materials?.Gemas, 2);
        }

        private MatEntry[] BuildEntries(IEnumerable<HvsMaterial>? list, int type)
        {
            if (list == null) return Array.Empty<MatEntry>();
            var result = new List<MatEntry>();

            foreach (var m in list)
            {
                if (string.IsNullOrWhiteSpace(m.Id)) continue;
                if (!TryGetHsvRangeNormalized(m, out var h, out var s, out var v, out bool hWrap)) continue;

                double hMid = MidHue(h.Min, h.Max, hWrap);
                double sHalf = Math.Max(1e-6, (s.Max - s.Min) / 2.0);
                double vHalf = Math.Max(1e-6, (v.Max - v.Min) / 2.0);

                result.Add(new MatEntry(
                    Id: m.Id!,
                    Name: m.Nome ?? m.Id!,
                    Group: m.Grupo ?? "",
                    Type: type,
                    H: h, HWrap: hWrap,
                    S: s, V: v,
                    HMid: hMid, SHalf: sHalf, VHalf: vHalf
                ));
            }

            return result.ToArray();
        }

        public (SampleFullAnalysisResult analysis, SampleMaskClass[,] mask, Bitmap maskPreview)
            RunFullAnalysis(Bitmap bmp, string? imagePath)
        {
            var scene = AnalyzeScene(bmp, imagePath);
            return (scene.Summary, scene.Mask, scene.MaskPreview);
        }

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

                            // Padrão: fundo
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

                            sampleCount++;

                            int off = row + x * 3;
                            byte B = buf[off + 0], G = buf[off + 1], R = buf[off + 2];

                            int gray = (int)(0.299 * R + 0.587 * G + 0.114 * B);
                            if (gray < 5 || gray > 250) acc.Clip++;

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

                            if (S < _minSat || V < _minVal || V > _maxVal)
                                continue;

                            double bestScore = 0;
                            int bestType = -1, bestIndex = -1;

                            EvaluateList(H, S, V, _metals, ref bestScore, ref bestType, ref bestIndex, 0);
                            EvaluateList(H, S, V, _crystals, ref bestScore, ref bestType, ref bestIndex, 1);
                            EvaluateList(H, S, V, _gems, ref bestScore, ref bestType, ref bestIndex, 2);

                            bool goldHeuristic = DetectGoldPixel(R, G, B, H, S, V);
                            bool pgmHeuristic = DetectPgmPixel(R, G, B, H, S, V);

                            // Reforça ouro em cenário ambíguo
                            if (goldHeuristic)
                            {
                                int auIndex = Array.FindIndex(_metals, m => m.Id.Equals("Au", StringComparison.OrdinalIgnoreCase));
                                if (auIndex >= 0)
                                {
                                    if (bestType == 0 && bestIndex != auIndex && bestScore < _minScorePixel + 0.25)
                                    {
                                        bestType = 0;
                                        bestIndex = auIndex;
                                        bestScore = Math.Max(bestScore, _minScorePixel + 0.25);
                                    }
                                    else if (bestScore < _minScorePixel)
                                    {
                                        bestType = 0;
                                        bestIndex = auIndex;
                                        bestScore = _minScorePixel + 0.25;
                                    }
                                }
                            }
                            else if (pgmHeuristic)
                            {
                                int pgIndex = Array.FindIndex(_metals, m =>
                                    !string.IsNullOrEmpty(m.Group) &&
                                    m.Group.ToLower().Contains("pgm"));
                                if (pgIndex >= 0 && bestScore < _minScorePixel + 0.15)
                                {
                                    bestType = 0;
                                    bestIndex = pgIndex;
                                    bestScore = Math.Max(bestScore, _minScorePixel + 0.15);
                                }
                            }

                            if (bestScore >= _minScorePixel && bestIndex >= 0)
                            {
                                lbl.RawScore = bestScore;
                                lbl.MaterialConfidence = Math.Min(1.0, bestScore);

                                if (bestType == 0)
                                {
                                    acc.MetalCounts[bestIndex]++;
                                    lbl.MaterialType = PixelMaterialType.Metal;
                                    lbl.MaterialId = _metals[bestIndex].Id;
                                }
                                else if (bestType == 1)
                                {
                                    acc.CrystalCounts[bestIndex]++;
                                    lbl.MaterialType = PixelMaterialType.Crystal;
                                    lbl.MaterialId = _crystals[bestIndex].Id;
                                }
                                else if (bestType == 2)
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

            double focus = 0;
            if (sampleCount > 0)
            {
                focus = gradSum / sampleCount / (255.0 * 255.0);
                focus = Math.Min(1.0, focus);
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

            for (int i = 0; i < _metals.Length; i++)
            {
                double pct = (double)metalCounts[i] / denom;
                result.Metals.Add(new MetalResult
                {
                    Id = _metals[i].Id,
                    Name = _metals[i].Name,
                    Group = _metals[i].Group,
                    PctSample = pct,
                    PpmEstimated = pct > 0 ? pct * 1_000_000.0 : (double?)null,
                    Score = Math.Max(0, Math.Min(1.0, pct * 10))
                });
            }

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
            sb.AppendLine("Resumo rápido da análise");
            sb.AppendLine("------------------------");
            sb.AppendLine($"Data/Hora (UTC): {r.CaptureDateTimeUtc:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Foco (0..1): {r.Diagnostics.FocusScore:F2}");
            sb.AppendLine($"Clipping saturação: {r.Diagnostics.SaturationClippingFraction:P1}");
            sb.AppendLine($"Fraçao amostra: {r.Diagnostics.ForegroundFraction:P1}");
            sb.AppendLine();
            sb.AppendLine("Metais (top 5):");
            foreach (var m in r.Metals.OrderByDescending(m => m.PctSample).Take(5))
            {
                var ppm = m.PpmEstimated.HasValue ? $"{m.PpmEstimated.Value:F1} ppm" : "-";
                sb.AppendLine($" - {m.Name} ({m.Id}): {m.PctSample:P3} · {ppm} · score={m.Score:F2}");
            }
            sb.AppendLine();
            sb.AppendLine("Cristais (top 3):");
            foreach (var c in r.Crystals.OrderByDescending(c => c.PctSample).Take(3))
            {
                sb.AppendLine($" - {c.Name}: {c.PctSample:P3} · score={c.Score:F2}");
            }
            sb.AppendLine();
            sb.AppendLine("Gemas (top 3):");
            foreach (var g in r.Gems.OrderByDescending(g => g.PctSample).Take(3))
            {
                sb.AppendLine($" - {g.Name}: {g.PctSample:P3} · score={g.Score:F2}");
            }
            return sb.ToString();
        }

        private bool DetectGoldPixel(byte R, byte G, byte B, double H, double S, double V)
        {
            if (H < 38 || H > 72) return false;
            if (S < 0.12 || V < 0.26) return false;
            double avgRG = (R + G) / 2.0;
            if (avgRG <= B + 6) return false;
            double diffRG = Math.Abs(R - G);
            if (diffRG > 50) return false;
            if (R < 110 && G < 110) return false;
            return true;
        }

        private bool DetectPgmPixel(byte R, byte G, byte B, double H, double S, double V)
        {
            if (S > 0.23) return false;
            if (V < 0.18 || V > 0.92) return false;
            int max = Math.Max(R, Math.Max(G, B));
            int min = Math.Min(R, Math.Min(G, B));
            if (max - min > 45) return false;
            if (max > 245 && min > 220) return false;
            return true;
        }
    }
}