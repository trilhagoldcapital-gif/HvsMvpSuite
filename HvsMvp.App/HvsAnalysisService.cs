﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace HvsMvp.App
{
    /// <summary>
    /// Resultado da classificação de um pixel com material identificado.
    /// </summary>
    public class PixelClassificationResult
    {
        public string MaterialId { get; set; } = "Unknown";
        public PixelMaterialType MaterialType { get; set; } = PixelMaterialType.None;
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Definição de ranges HSV para um material.
    /// </summary>
    public class MaterialHsvRange
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public PixelMaterialType Type { get; set; }
        public double HMin { get; set; }
        public double HMax { get; set; }
        public double SMin { get; set; }
        public double SMax { get; set; }
        public double VMin { get; set; }
        public double VMax { get; set; }
        public double Priority { get; set; } = 1.0; // Higher priority = checked first

        /// <summary>
        /// Verifica se os valores HSV estão dentro do range.
        /// </summary>
        public bool IsInRange(double h, double s, double v)
        {
            // Handle circular Hue range (e.g., red crosses 360)
            bool hInRange;
            if (HMin <= HMax)
            {
                hInRange = h >= HMin && h <= HMax;
            }
            else
            {
                // Range crosses 0/360 (e.g., 350-10 for red)
                hInRange = h >= HMin || h <= HMax;
            }

            bool sInRange = s >= SMin && s <= SMax;
            bool vInRange = v >= VMin && v <= VMax;

            return hInRange && sInRange && vInRange;
        }

        /// <summary>
        /// Calcula score de proximidade ao range (0..1).
        /// </summary>
        public double CalculateScore(double h, double s, double v)
        {
            double hScore = CalculateHueScore(h);
            double sScore = CalculateLinearScore(s, SMin, SMax);
            double vScore = CalculateLinearScore(v, VMin, VMax);

            // Weighted average: Hue 40%, Saturation 30%, Value 30%
            return hScore * 0.4 + sScore * 0.3 + vScore * 0.3;
        }

        private double CalculateHueScore(double h)
        {
            if (HMin <= HMax)
            {
                // Normal range
                if (h >= HMin && h <= HMax) return 1.0;
                double distMin = Math.Abs(h - HMin);
                double distMax = Math.Abs(h - HMax);
                double dist = Math.Min(distMin, distMax);
                return Math.Max(0, 1.0 - dist / 60.0);
            }
            else
            {
                // Circular range (crosses 0/360)
                if (h >= HMin || h <= HMax) return 1.0;
                // Calculate circular distance properly
                double distToMin = h > HMin ? h - HMin : 360 - HMin + h;
                double distFromMax = h < HMax ? HMax - h : 360 - h + HMax;
                double dist = Math.Min(distToMin, distFromMax);
                return Math.Max(0, 1.0 - dist / 60.0);
            }
        }

        private double CalculateLinearScore(double value, double min, double max)
        {
            if (value >= min && value <= max) return 1.0;
            double dist = value < min ? min - value : value - max;
            return Math.Max(0, 1.0 - dist / 0.3);
        }
    }

    public class HvsAnalysisService
    {
        private readonly HvsConfig _config;
        private readonly List<MaterialHsvRange> _materialRanges;

        // Constants for particle segmentation
        private const int MinParticleSizeAbsolute = 20;
        private const int MinParticleSizeDivisor = 50000;

        // Constants for material threshold exemptions (always include regardless of percentage)
        private static readonly HashSet<string> PrimaryMetalsAlwaysIncluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Au", "Pt"
        };
        private const double MinMaterialPercentageThreshold = 0.0001; // 0.01%

        public HvsAnalysisService(HvsConfig config)
        {
            _config = config;
            _materialRanges = BuildMaterialRanges();
        }

        /// <summary>
        /// Constrói lista de ranges HSV para todos os materiais do catálogo.
        /// </summary>
        private List<MaterialHsvRange> BuildMaterialRanges()
        {
            var ranges = new List<MaterialHsvRange>();

            // ====== METAIS ======

            // Au (Ouro) - dourado/amarelo
            ranges.Add(new MaterialHsvRange
            {
                Id = "Au", Name = "Ouro", Type = PixelMaterialType.Metal,
                HMin = 30, HMax = 80, SMin = 0.18, SMax = 1.0, VMin = 0.35, VMax = 1.0,
                Priority = 1.0
            });

            // Ag (Prata) - branco prateado muito brilhante
            ranges.Add(new MaterialHsvRange
            {
                Id = "Ag", Name = "Prata", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.15, VMin = 0.85, VMax = 1.0,
                Priority = 0.9
            });

            // Pt (Platina) - cinza neutro médio
            ranges.Add(new MaterialHsvRange
            {
                Id = "Pt", Name = "Platina", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.15, VMin = 0.40, VMax = 0.85,
                Priority = 0.85
            });

            // Pd (Paládio) - similar a Pt mas um pouco mais claro
            ranges.Add(new MaterialHsvRange
            {
                Id = "Pd", Name = "Paládio", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.18, VMin = 0.35, VMax = 0.90,
                Priority = 0.8
            });

            // Cu (Cobre) - avermelhado/laranja
            ranges.Add(new MaterialHsvRange
            {
                Id = "Cu", Name = "Cobre", Type = PixelMaterialType.Metal,
                HMin = 15, HMax = 25, SMin = 0.5, SMax = 0.9, VMin = 0.5, VMax = 0.95,
                Priority = 0.85
            });

            // Fe (Ferro) - cinza escuro/marrom
            ranges.Add(new MaterialHsvRange
            {
                Id = "Fe", Name = "Ferro", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 20, SMin = 0.0, SMax = 0.4, VMin = 0.2, VMax = 0.6,
                Priority = 0.7
            });

            // Al (Alumínio) - branco/prateado brilhante
            ranges.Add(new MaterialHsvRange
            {
                Id = "Al", Name = "Alumínio", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.15, VMin = 0.75, VMax = 1.0,
                Priority = 0.75
            });

            // Ni (Níquel) - cinza médio
            ranges.Add(new MaterialHsvRange
            {
                Id = "Ni", Name = "Níquel", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.25, VMin = 0.50, VMax = 0.85,
                Priority = 0.7
            });

            // Zn (Zinco) - cinza claro
            ranges.Add(new MaterialHsvRange
            {
                Id = "Zn", Name = "Zinco", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.15, VMin = 0.60, VMax = 0.90,
                Priority = 0.65
            });

            // Pb (Chumbo) - cinza escuro
            ranges.Add(new MaterialHsvRange
            {
                Id = "Pb", Name = "Chumbo", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.25, VMin = 0.25, VMax = 0.55,
                Priority = 0.6
            });

            // ====== CRISTAIS ======

            // SiO2 (Quartzo) - transparente/branco
            ranges.Add(new MaterialHsvRange
            {
                Id = "SiO2", Name = "Quartzo", Type = PixelMaterialType.Crystal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.20, VMin = 0.70, VMax = 1.0,
                Priority = 0.6
            });

            // CaCO3 (Calcita) - branco/creme
            ranges.Add(new MaterialHsvRange
            {
                Id = "CaCO3", Name = "Calcita", Type = PixelMaterialType.Crystal,
                HMin = 0, HMax = 60, SMin = 0.0, SMax = 0.35, VMin = 0.70, VMax = 1.0,
                Priority = 0.55
            });

            // Feldspato - branco/rosa pálido
            ranges.Add(new MaterialHsvRange
            {
                Id = "Feldspato", Name = "Feldspato", Type = PixelMaterialType.Crystal,
                HMin = 0, HMax = 60, SMin = 0.10, SMax = 0.45, VMin = 0.55, VMax = 0.90,
                Priority = 0.5
            });

            // Mica - dourado/bronze
            ranges.Add(new MaterialHsvRange
            {
                Id = "Mica", Name = "Mica", Type = PixelMaterialType.Crystal,
                HMin = 0, HMax = 80, SMin = 0.15, SMax = 0.55, VMin = 0.50, VMax = 0.85,
                Priority = 0.5
            });

            // CaF2 (Fluorita) - roxo/azul
            ranges.Add(new MaterialHsvRange
            {
                Id = "CaF2", Name = "Fluorita", Type = PixelMaterialType.Crystal,
                HMin = 200, HMax = 300, SMin = 0.40, SMax = 1.0, VMin = 0.45, VMax = 0.90,
                Priority = 0.6
            });

            // ====== GEMAS ======

            // Diamante - transparente muito brilhante
            ranges.Add(new MaterialHsvRange
            {
                Id = "C", Name = "Diamante", Type = PixelMaterialType.Gem,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.15, VMin = 0.90, VMax = 1.0,
                Priority = 0.7
            });

            // Safira - azul profundo
            ranges.Add(new MaterialHsvRange
            {
                Id = "Al2O3_blue", Name = "Safira", Type = PixelMaterialType.Gem,
                HMin = 200, HMax = 250, SMin = 0.55, SMax = 1.0, VMin = 0.45, VMax = 0.85,
                Priority = 0.75
            });

            // Rubi - vermelho profundo
            ranges.Add(new MaterialHsvRange
            {
                Id = "Al2O3_red", Name = "Rubi", Type = PixelMaterialType.Gem,
                HMin = 350, HMax = 10, SMin = 0.55, SMax = 1.0, VMin = 0.40, VMax = 0.80,
                Priority = 0.75
            });

            // Esmeralda - verde profundo
            ranges.Add(new MaterialHsvRange
            {
                Id = "Be3Al2Si6O18", Name = "Esmeralda", Type = PixelMaterialType.Gem,
                HMin = 120, HMax = 160, SMin = 0.55, SMax = 1.0, VMin = 0.40, VMax = 0.85,
                Priority = 0.75
            });

            // Ametista - roxo
            ranges.Add(new MaterialHsvRange
            {
                Id = "SiO2_purple", Name = "Ametista", Type = PixelMaterialType.Gem,
                HMin = 270, HMax = 300, SMin = 0.35, SMax = 0.85, VMin = 0.45, VMax = 0.85,
                Priority = 0.7
            });

            // Ordenar por prioridade (maior primeiro)
            ranges.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            return ranges;
        }

        /// <summary>
        /// Classifica um pixel baseado em HSV e RGB.
        /// Retorna o material mais provável e sua confiança.
        /// </summary>
        private PixelClassificationResult ClassifyPixel(byte R, byte G, byte B, double H, double S, double V)
        {
            var result = new PixelClassificationResult();
            string bestMaterial = "MetalOther";
            PixelMaterialType bestType = PixelMaterialType.Metal;
            double bestScore = 0;

            foreach (var range in _materialRanges)
            {
                double score = range.CalculateScore(H, S, V);
                
                // Apply priority as a multiplier
                score *= range.Priority;

                // Additional RGB-based checks for metals
                if (range.Type == PixelMaterialType.Metal)
                {
                    // Gold: R and G should be similar and higher than B
                    if (range.Id == "Au")
                    {
                        double avgRG = (R + G) / 2.0;
                        if (avgRG <= B + 10) score *= 0.3;
                        double diffRG = Math.Abs(R - G);
                        if (diffRG > 70) score *= 0.5;
                        if (R < 120 && G < 120) score *= 0.4;
                    }
                    
                    // Copper: R should be significantly higher than B
                    if (range.Id == "Cu")
                    {
                        if (R <= G || R <= B) score *= 0.3;
                    }

                    // PGM metals: low saturation, similar RGB values
                    if (range.Id == "Pt" || range.Id == "Pd" || range.Id == "Ni" || range.Id == "Zn")
                    {
                        int max = Math.Max(R, Math.Max(G, B));
                        int min = Math.Min(R, Math.Min(G, B));
                        if (max - min > 35) score *= 0.5;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMaterial = range.Id;
                    bestType = range.Type;
                }
            }

            // Minimum confidence threshold
            if (bestScore < 0.3)
            {
                result.MaterialId = "MetalOther";
                result.MaterialType = PixelMaterialType.Metal;
                result.Confidence = 0.5;
            }
            else
            {
                result.MaterialId = bestMaterial;
                result.MaterialType = bestType;
                result.Confidence = Math.Min(0.95, bestScore);
            }

            return result;
        }

        public (SampleFullAnalysisResult analysis, SampleMaskClass[,] mask, Bitmap maskPreview)
            RunFullAnalysis(Bitmap bmp, string? imagePath)
        {
            var scene = AnalyzeScene(bmp, imagePath);
            // AnalyzeScene always populates the mask fully, so we can safely convert
            // The nullable type in FullSceneAnalysis is for API flexibility, but internally it's always populated
            int w = scene.Width;
            int h = scene.Height;
            var nullableMask = scene.Mask;
            var mask = new SampleMaskClass[w, h];
            
            // Only iterate if we have a valid mask
            if (nullableMask.GetLength(0) == w && nullableMask.GetLength(1) == h)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        mask[x, y] = nullableMask[x, y] ?? new SampleMaskClass { IsSample = false };
                    }
                }
            }
            
            return (scene.Summary, mask, scene.MaskPreview);
        }

        /// <summary>
        /// BLOCO 2 – Executa análise com reanálise automática para amostras críticas.
        /// - Se QualityStatus == "Invalid", roda mais 2 análises na mesma imagem.
        /// - Compara QualityIndex e %Au nas 3 rodadas.
        /// - Se convergir: QualityStatus = "OfficialRechecked".
        /// - Se divergir:  QualityStatus = "ReviewRequired".
        /// </summary>
        public SampleFullAnalysisResult RunWithAutoReanalysis(Bitmap bmp, string? imagePath)
        {
            // 1) Primeira análise normal
            var scene1 = AnalyzeScene(bmp, imagePath);
            var r1 = scene1.Summary;

            // Se não for "Invalid", não é amostra crítica: retorna direto
            if (!string.Equals(r1.QualityStatus, "Invalid", StringComparison.OrdinalIgnoreCase))
                return r1;

            // 2) Amostra crítica -> executar mais 2 análises completas na mesma imagem
            var scene2 = AnalyzeScene(bmp, imagePath);
            var scene3 = AnalyzeScene(bmp, imagePath);
            var r2 = scene2.Summary;
            var r3 = scene3.Summary;

            double q1 = r1.QualityIndex;
            double q2 = r2.QualityIndex;
            double q3 = r3.QualityIndex;

            double qMin = Math.Min(q1, Math.Min(q2, q3));
            double qMax = Math.Max(q1, Math.Max(q2, q3));
            double qRange = qMax - qMin;

            double GetPctAu(SampleFullAnalysisResult r)
            {
                var au = r.Metals.FirstOrDefault(m => string.Equals(m.Id, "Au", StringComparison.OrdinalIgnoreCase));
                return au?.PctSample ?? 0.0;
            }

            double a1 = GetPctAu(r1);
            double a2 = GetPctAu(r2);
            double a3 = GetPctAu(r3);

            double aMin = Math.Min(a1, Math.Min(a2, a3));
            double aMax = Math.Max(a1, Math.Max(a2, a3));
            double aRange = aMax - aMin;

            bool convergiu = (qRange <= 5.0) && (aRange <= 0.0005);

            r1.QualityStatus = convergiu ? "OfficialRechecked" : "ReviewRequired";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("--- Reanálise automática (BLOCO 2) ---");
            sb.AppendLine($"Rodadas: 3");
            sb.AppendLine($"QualityIndex: {q1:F1}, {q2:F1}, {q3:F1} (range={qRange:F1})");
            sb.AppendLine($"PctAu: {a1:P4}, {a2:P4}, {a3:P4} (range={aRange:P4})");
            sb.AppendLine($"Decisão: {r1.QualityStatus}");

            r1.ShortReport = (r1.ShortReport ?? string.Empty) + sb.ToString();

            return r1;
        }

        public FullSceneAnalysis AnalyzeScene(Bitmap bmp, string? imagePath)
        {
            using var src24 = Ensure24bpp(bmp);

            var maskService = new SampleMaskService();
            var (maskNullable, maskPreview, maskValidation) = maskService.BuildMaskWithValidation(src24);

            int w = src24.Width, h = src24.Height;
            var mask = new SampleMaskClass[w, h];
            long sampleCount = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isSample = maskNullable[x, y]?.IsSample == true;
                    mask[x, y] = new SampleMaskClass { IsSample = isSample };
                    if (isSample) sampleCount++;
                }
            }

            var rect = new Rectangle(0, 0, w, h);
            var data = src24.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            // Material counts dictionary - tracks all materials
            var materialCounts = new Dictionary<string, long>();

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

                object locker = new object();

                System.Threading.Tasks.Parallel.For(
                    0, h,
                    () => new LocalAccExtended(),
                    (y, loop, acc) =>
                    {
                        int row = y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            acc.Total++;

                            var lbl = new PixelLabel();
                            labels[x, y] = lbl;

                            lbl.IsSample = false;
                            lbl.MaterialType = PixelMaterialType.Background;
                            lbl.MaterialId = null;
                            lbl.MaterialConfidence = 0;
                            lbl.RawScore = 0;

                            if (!mask[x, y].IsSample)
                                continue;

                            lbl.IsSample = true;

                            int off = row + x * 3;
                            byte B = buf[off + 0];
                            byte G = buf[off + 1];
                            byte R = buf[off + 2];

                            // Store RGB values
                            lbl.R = R;
                            lbl.G = G;
                            lbl.B = B;

                            int gray = (int)(0.299 * R + 0.587 * G + 0.114 * B);
                            if (gray < 5 || gray > 250) acc.Clip++;

                            if (x > 0 && x < w - 1 && y > 0 && y < h - 1)
                            {
                                int offL = row + (x - 1) * 3;
                                int offR = row + (x + 1) * 3;
                                int offU = (y - 1) * stride + x * 3;
                                int offD = (y + 1) * stride + x * 3;

                                int gL = (int)(0.299 * buf[offL + 2] + 0.587 * buf[offL + 1] + 0.114 * buf[offL + 0]);
                                int gR2 = (int)(0.299 * buf[offR + 2] + 0.587 * buf[offR + 1] + 0.114 * buf[offR + 0]);
                                int gU = (int)(0.299 * buf[offU + 2] + 0.587 * buf[offU + 1] + 0.114 * buf[offU + 0]);
                                int gD = (int)(0.299 * buf[offD + 2] + 0.587 * buf[offD + 1] + 0.114 * buf[offD + 0]);

                                int gx = gR2 - gL;
                                int gy = gD - gU;
                                acc.GradSum += (gx * gx + gy * gy);
                            }

                            RgbToHsvFast(R, G, B, out double H, out double S, out double V);
                            lbl.H = H;
                            lbl.S = S;
                            lbl.V = V;

                            // Use the new classification system
                            var classification = ClassifyPixel(R, G, B, H, S, V);
                            
                            lbl.MaterialType = classification.MaterialType;
                            lbl.MaterialId = classification.MaterialId;
                            lbl.ScoreHvs = classification.Confidence;
                            lbl.ScoreIa = classification.Confidence; // Stub: same as HVS
                            lbl.RawScore = classification.Confidence;
                            lbl.MaterialConfidence = classification.Confidence;

                            // Track material count
                            if (!acc.MaterialCounts.ContainsKey(classification.MaterialId))
                                acc.MaterialCounts[classification.MaterialId] = 0;
                            acc.MaterialCounts[classification.MaterialId]++;
                        }

                        return acc;
                    },
                    acc =>
                    {
                        lock (locker)
                        {
                            diagTotal += acc.Total;
                            diagClip += acc.Clip;
                            gradSum += acc.GradSum;
                            
                            // Merge material counts
                            foreach (var kvp in acc.MaterialCounts)
                            {
                                if (!materialCounts.ContainsKey(kvp.Key))
                                    materialCounts[kvp.Key] = 0;
                                materialCounts[kvp.Key] += kvp.Value;
                            }
                        }
                    });
            }
            finally
            {
                src24.UnlockBits(data);
            }

            // -----------------------------
            // BLOCO 1 – CHECKLIST DE QUALIDADE
            // -----------------------------

            double focusRaw = 0;
            if (sampleCount > 0)
            {
                focusRaw = gradSum / sampleCount / (255.0 * 255.0);
                focusRaw = Math.Min(1.0, Math.Max(0.0, focusRaw));
            }

            double focusScore = focusRaw * 100.0;
            double clippingFrac = diagTotal > 0 ? (double)diagClip / diagTotal : 0.0;
            double exposureScore = 100.0 * (1.0 - Math.Min(1.0, clippingFrac / 0.2));
            double foregroundFraction = (double)sampleCount / Math.Max(1, w * h);

            double maskScore;
            if (foregroundFraction < 0.3)
            {
                maskScore = 50.0 * foregroundFraction / 0.3;
            }
            else if (foregroundFraction > 0.95)
            {
                maskScore = 60.0;
            }
            else
            {
                maskScore = 80.0 + 20.0 * (1.0 - Math.Abs(foregroundFraction - 0.6) / 0.3);
                maskScore = Math.Min(100.0, Math.Max(0.0, maskScore));
            }

            double qualityIndex =
                0.4 * focusScore +
                0.3 * exposureScore +
                0.3 * maskScore;

            qualityIndex = Math.Max(0.0, Math.Min(100.0, qualityIndex));

            string qualityStatus;
            if (qualityIndex >= 85.0)
                qualityStatus = "Official";
            else if (qualityIndex >= 70.0)
                qualityStatus = "Preliminary";
            else
                qualityStatus = "Invalid";

            var diag = new ImageDiagnosticsResult
            {
                FocusScore = focusRaw,
                SaturationClippingFraction = diagTotal > 0 ? (double)diagClip / diagTotal : 0,
                ForegroundFraction = (double)sampleCount / Math.Max(1, w * h),
                FocusScorePercent = focusScore,
                ExposureScore = exposureScore,
                MaskScore = maskScore,
                QualityIndex = qualityIndex,
                QualityStatus = qualityStatus,
                ForegroundFractionStatus = GetForegroundFractionStatus(foregroundFraction),
                MaskWarnings = maskValidation.Warnings
            };

            var result = new SampleFullAnalysisResult
            {
                Id = Guid.NewGuid(),
                ImagePath = imagePath,
                CaptureDateTimeUtc = DateTime.UtcNow,
                Diagnostics = diag,
                QualityIndex = qualityIndex,
                QualityStatus = qualityStatus
            };

            long denom = Math.Max(1, sampleCount);

            // Build results for ALL materials using materialCounts
            BuildMaterialResults(result, materialCounts, denom);

            // -----------------------------
            // BLOCO 3A + 3B – BANCO DE PARTÍCULAS com segmentação individual
            // -----------------------------

            var particles = SegmentParticles(labels, mask, w, h, result.Id);
            result.Particles.AddRange(particles);

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
        /// Determina o status da fração de foreground.
        /// </summary>
        private string GetForegroundFractionStatus(double fraction)
        {
            if (fraction < 0.05) return "Muito baixa";
            if (fraction < 0.30) return "Baixa";
            if (fraction > 0.95) return "Muito alta";
            if (fraction > 0.80) return "Alta";
            return "OK";
        }

        /// <summary>
        /// Constrói resultados de materiais a partir das contagens.
        /// </summary>
        private void BuildMaterialResults(SampleFullAnalysisResult result, Dictionary<string, long> materialCounts, long totalSamplePixels)
        {
            // Mapeamento de IDs para nomes e grupos
            var metalInfo = new Dictionary<string, (string name, string group)>
            {
                ["Au"] = ("Ouro", "Nobre"),
                ["Ag"] = ("Prata", "Nobre"),
                ["Pt"] = ("Platina", "PGM"),
                ["Pd"] = ("Paládio", "PGM"),
                ["Rh"] = ("Ródio", "PGM"),
                ["Ir"] = ("Irídio", "PGM"),
                ["Ru"] = ("Rutênio", "PGM"),
                ["Os"] = ("Ósmio", "PGM"),
                ["Cu"] = ("Cobre", "comum"),
                ["Fe"] = ("Ferro", "comum"),
                ["Al"] = ("Alumínio", "comum"),
                ["Ni"] = ("Níquel", "comum"),
                ["Zn"] = ("Zinco", "comum"),
                ["Pb"] = ("Chumbo", "comum"),
                ["MetalOther"] = ("Outros metais", "outros")
            };

            var crystalInfo = new Dictionary<string, string>
            {
                ["SiO2"] = "Quartzo",
                ["CaCO3"] = "Calcita",
                ["Feldspato"] = "Feldspato",
                ["Mica"] = "Mica",
                ["CaF2"] = "Fluorita"
            };

            var gemInfo = new Dictionary<string, string>
            {
                ["C"] = "Diamante",
                ["Al2O3_blue"] = "Safira",
                ["Al2O3_red"] = "Rubi",
                ["Be3Al2Si6O18"] = "Esmeralda",
                ["SiO2_purple"] = "Ametista"
            };

            // Processar cada material encontrado
            foreach (var kvp in materialCounts)
            {
                string matId = kvp.Key;
                long count = kvp.Value;
                double pct = (double)count / totalSamplePixels;

                // Ignorar se a fração for muito baixa, exceto para metais primários
                if (pct < MinMaterialPercentageThreshold && !PrimaryMetalsAlwaysIncluded.Contains(matId))
                    continue;

                if (metalInfo.TryGetValue(matId, out var metal))
                {
                    result.Metals.Add(new MetalResult
                    {
                        Id = matId,
                        Name = metal.name,
                        Group = metal.group,
                        PctSample = pct,
                        PpmEstimated = pct > 0 ? pct * 1_000_000.0 : (double?)null,
                        Score = Math.Min(1.0, 0.6 + pct * 10.0)
                    });
                }
                else if (crystalInfo.TryGetValue(matId, out var crystalName))
                {
                    result.Crystals.Add(new CrystalResult
                    {
                        Id = matId,
                        Name = crystalName,
                        PctSample = pct,
                        Score = Math.Min(1.0, 0.5 + pct * 8.0)
                    });
                }
                else if (gemInfo.TryGetValue(matId, out var gemName))
                {
                    result.Gems.Add(new GemResult
                    {
                        Id = matId,
                        Name = gemName,
                        PctSample = pct,
                        Score = Math.Min(1.0, 0.7 + pct * 5.0)
                    });
                }
            }

            // Ordenar por percentual (maior primeiro)
            result.Metals = result.Metals.OrderByDescending(m => m.PctSample).ToList();
            result.Crystals = result.Crystals.OrderByDescending(c => c.PctSample).ToList();
            result.Gems = result.Gems.OrderByDescending(g => g.PctSample).ToList();
        }

        /// <summary>
        /// Segmenta pixels contíguos da mesma classe em partículas individuais.
        /// </summary>
        private List<ParticleRecord> SegmentParticles(
            PixelLabel[,] labels,
            SampleMaskClass[,] mask,
            int w, int h,
            Guid analysisId)
        {
            var particles = new List<ParticleRecord>();
            var visited = new bool[w, h];
            var aiService = new PixelClassificationAiServiceStub();

            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            var queue = new Queue<(int x, int y)>();
            int minParticleSize = Math.Max(MinParticleSizeAbsolute, (w * h) / MinParticleSizeDivisor);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var lbl = labels[x, y];
                    if (lbl == null || !lbl.IsSample || visited[x, y])
                        continue;

                    // Iniciar BFS para agrupar pixels contíguos
                    var features = new ParticleFeatures();
                    queue.Clear();

                    visited[x, y] = true;
                    queue.Enqueue((x, y));

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        var pixLbl = labels[cx, cy];

                        // Acumular features
                        features.PixelCount++;
                        features.SumX += cx;
                        features.SumY += cy;
                        features.MinX = Math.Min(features.MinX, cx);
                        features.MaxX = Math.Max(features.MaxX, cx);
                        features.MinY = Math.Min(features.MinY, cy);
                        features.MaxY = Math.Max(features.MaxY, cy);
                        features.SumH += pixLbl.H;
                        features.SumS += pixLbl.S;
                        features.SumV += pixLbl.V;
                        features.SumConfidence += pixLbl.MaterialConfidence;
                        features.SumConfidenceSq += pixLbl.MaterialConfidence * pixLbl.MaterialConfidence;
                        features.Pixels.Add((cx, cy));

                        // Contar votos por material
                        string matId = pixLbl.MaterialId ?? "Unknown";
                        if (!features.MaterialVotes.ContainsKey(matId))
                        {
                            features.MaterialVotes[matId] = 0;
                            features.MaterialWeightedVotes[matId] = 0;
                        }
                        features.MaterialVotes[matId]++;
                        features.MaterialWeightedVotes[matId] += pixLbl.MaterialConfidence;

                        // Verificar se é pixel de borda
                        bool isBorder = false;
                        for (int k = 0; k < 8 && !isBorder; k++)
                        {
                            int nx = cx + dx[k];
                            int ny = cy + dy[k];
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            {
                                isBorder = true;
                            }
                            else if (labels[nx, ny] == null || !labels[nx, ny].IsSample)
                            {
                                isBorder = true;
                            }
                        }
                        if (isBorder) features.BorderPixelCount++;

                        // Expandir para vizinhos da mesma classificação
                        for (int k = 0; k < 8; k++)
                        {
                            int nx = cx + dx[k];
                            int ny = cy + dy[k];
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (visited[nx, ny]) continue;

                            var neighLbl = labels[nx, ny];
                            if (neighLbl == null || !neighLbl.IsSample) continue;

                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }

                    // Ignorar partículas muito pequenas
                    if (features.PixelCount < minParticleSize)
                        continue;

                    // Criar ParticleRecord
                    var particle = CreateParticleRecord(features, analysisId, aiService);
                    particles.Add(particle);
                }
            }

            return particles;
        }

        /// <summary>
        /// Cria um ParticleRecord a partir das features acumuladas.
        /// </summary>
        private ParticleRecord CreateParticleRecord(ParticleFeatures features, Guid analysisId, IPixelClassificationAiService aiService)
        {
            var (cx, cy) = features.GetCentroid();
            int n = features.PixelCount;

            // Determinar material dominante por voto ponderado
            string dominantMaterial = "Unknown";
            double maxVote = 0;
            foreach (var (matId, vote) in features.MaterialWeightedVotes)
            {
                if (vote > maxVote)
                {
                    maxVote = vote;
                    dominantMaterial = matId;
                }
            }

            // Calcular confiança média
            double avgConf = n > 0 ? features.SumConfidence / n : 0;
            double variance = n > 1
                ? (features.SumConfidenceSq - features.SumConfidence * features.SumConfidence / n) / (n - 1)
                : 0;
            double stdDev = Math.Sqrt(Math.Max(0, variance));

            // Calcular HSV médio
            double avgH = n > 0 ? features.SumH / n : 0;
            double avgS = n > 0 ? features.SumS / n : 0;
            double avgV = n > 0 ? features.SumV / n : 0;

            // Calcular circularidade (aproximada)
            int perimeter = features.BorderPixelCount;
            double circularity = perimeter > 0
                ? (4 * Math.PI * n) / (perimeter * perimeter)
                : 0;
            circularity = Math.Min(1.0, Math.Max(0, circularity));

            // Calcular aspect ratio do bounding box
            int bbW = features.MaxX - features.MinX + 1;
            int bbH = features.MaxY - features.MinY + 1;
            double aspectRatio = bbH > 0 ? (double)bbW / bbH : 1.0;
            if (aspectRatio < 1) aspectRatio = 1.0 / aspectRatio; // Sempre >= 1

            // Scores HVS e IA
            double scoreHvs = avgConf;
            double scoreIa = avgConf; // Stub: usa HVS

            // Fusão de scores
            var (fusedMat, fusedConf, fusedRaw) = ScoreFusionService.FuseHvsOnly(dominantMaterial, scoreHvs);

            // Calcular composição
            var composition = new Dictionary<string, double>();
            foreach (var (matId, vote) in features.MaterialVotes)
            {
                composition[matId] = (double)vote / n;
            }

            return new ParticleRecord
            {
                AnalysisId = analysisId,
                MaterialId = fusedMat,
                Confidence = fusedConf,
                ApproxAreaPixels = n,
                CenterX = cx,
                CenterY = cy,

                // Forma
                Circularity = circularity,
                AspectRatio = aspectRatio,
                MajorAxisLength = Math.Max(bbW, bbH),
                MinorAxisLength = Math.Min(bbW, bbH),
                Perimeter = perimeter,

                // Bounding box
                BoundingBoxX = features.MinX,
                BoundingBoxY = features.MinY,
                BoundingBoxWidth = bbW,
                BoundingBoxHeight = bbH,

                // HSV
                AvgH = avgH,
                AvgS = avgS,
                AvgV = avgV,

                // Scores
                ScoreHvs = scoreHvs,
                ScoreIa = scoreIa,
                ScoreCombined = fusedRaw,

                // Confiança
                AveragePixelConfidence = avgConf,
                ConfidenceStdDev = stdDev,

                // Composição
                Composition = composition
            };
        }

        private class LocalAccExtended
        {
            public long Total;
            public long Clip;
            public double GradSum;
            public Dictionary<string, long> MaterialCounts = new Dictionary<string, long>();
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

        private string BuildShortReport(SampleFullAnalysisResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Resumo rápido da análise");
            sb.AppendLine("------------------------");
            sb.AppendLine($"Data/Hora (UTC): {r.CaptureDateTimeUtc:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Foco (0..1): {r.Diagnostics.FocusScore:F2}");
            sb.AppendLine($"Clipping saturação: {r.Diagnostics.SaturationClippingFraction:P1}");
            sb.AppendLine($"Fração amostra: {r.Diagnostics.ForegroundFraction:P1} ({r.Diagnostics.ForegroundFractionStatus})");
            
            // Mostrar avisos da máscara se houver
            if (r.Diagnostics.MaskWarnings != null && r.Diagnostics.MaskWarnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("⚠️ Avisos da máscara:");
                foreach (var warning in r.Diagnostics.MaskWarnings)
                {
                    sb.AppendLine($"  - {warning}");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("Metais:");
            var topMetals = r.Metals.Where(m => m.PctSample > 0.0001).OrderByDescending(m => m.PctSample).Take(10);
            foreach (var m in topMetals)
            {
                var ppm = m.PpmEstimated.HasValue ? $"{m.PpmEstimated.Value:F1} ppm" : "-";
                sb.AppendLine($" - {m.Name} ({m.Id}): {m.PctSample:P3} · {ppm} · score={m.Score:F2}");
            }
            
            if (r.Crystals.Any(c => c.PctSample > 0.0001))
            {
                sb.AppendLine();
                sb.AppendLine("Cristais:");
                var topCrystals = r.Crystals.Where(c => c.PctSample > 0.0001).OrderByDescending(c => c.PctSample).Take(5);
                foreach (var c in topCrystals)
                {
                    sb.AppendLine($" - {c.Name} ({c.Id}): {c.PctSample:P3} · score={c.Score:F2}");
                }
            }
            
            if (r.Gems.Any(g => g.PctSample > 0.0001))
            {
                sb.AppendLine();
                sb.AppendLine("Gemas:");
                var topGems = r.Gems.Where(g => g.PctSample > 0.0001).OrderByDescending(g => g.PctSample).Take(5);
                foreach (var g in topGems)
                {
                    sb.AppendLine($" - {g.Name} ({g.Id}): {g.PctSample:P3} · score={g.Score:F2}");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine($"QualityIndex: {r.QualityIndex:F1} · Status: {r.QualityStatus}");
            sb.AppendLine($"Partículas (agregadas): {r.Particles.Count}");
            return sb.ToString();
        }

        private bool DetectGoldPixel(byte R, byte G, byte B, double H, double S, double V)
        {
            if (H < 30 || H > 80) return false;
            if (S < 0.18 || V < 0.35) return false;

            double avgRG = (R + G) / 2.0;
            if (avgRG <= B + 10) return false;
            double diffRG = Math.Abs(R - G);
            if (diffRG > 70) return false;
            if (R < 120 && G < 120) return false;

            return true;
        }

        private bool DetectPgmPixel(byte R, byte G, byte B, double H, double S, double V)
        {
            if (S > 0.20) return false;
            if (V < 0.20 || V > 0.92) return false;
            int max = Math.Max(R, Math.Max(G, B));
            int min = Math.Min(R, Math.Min(G, B));
            if (max - min > 35) return false;
            if (max > 245 && min > 220) return false;
            return true;
        }
    }
}