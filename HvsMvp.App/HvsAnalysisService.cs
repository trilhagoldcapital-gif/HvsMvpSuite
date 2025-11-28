﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace HvsMvp.App
{
    public class HvsAnalysisService
    {
        private readonly HvsConfig _config;

        public HvsAnalysisService(HvsConfig config)
        {
            _config = config;
        }

        public (SampleFullAnalysisResult analysis, SampleMaskClass[,] mask, Bitmap maskPreview)
            RunFullAnalysis(Bitmap bmp, string? imagePath)
        {
            var scene = AnalyzeScene(bmp, imagePath);
            var mask = scene.Mask ?? new SampleMaskClass[scene.Width, scene.Height];
            return (scene.Summary, mask, scene.MaskPreview);
        }

        public FullSceneAnalysis AnalyzeScene(Bitmap bmp, string? imagePath)
        {
            using var src24 = Ensure24bpp(bmp);

            var maskService = new SampleMaskService();
            var (maskNullable, maskPreview) = maskService.BuildMask(src24);

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

            long auCount = 0;
            long ptCount = 0;
            long otherMetal = 0;

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
                    () => new LocalAcc(),
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

                            bool looksGold = DetectGoldPixel(R, G, B, H, S, V);
                            bool looksPgm = DetectPgmPixel(R, G, B, H, S, V);

                            if (looksGold)
                            {
                                lbl.MaterialType = PixelMaterialType.Metal;
                                lbl.MaterialId = "Au";
                                lbl.RawScore = 0.95;
                                lbl.MaterialConfidence = 0.95;
                                acc.Au++;
                            }
                            else if (looksPgm)
                            {
                                lbl.MaterialType = PixelMaterialType.Metal;
                                lbl.MaterialId = "Pt";
                                lbl.RawScore = 0.80;
                                lbl.MaterialConfidence = 0.80;
                                acc.Pt++;
                            }
                            else
                            {
                                lbl.MaterialType = PixelMaterialType.None;
                            }
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
                            auCount += acc.Au;
                            ptCount += acc.Pt;
                            otherMetal += acc.Other;
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

            // Foco bruto (0..1) já calculado a partir de gradientes
            double focusRaw = 0;
            if (sampleCount > 0)
            {
                focusRaw = gradSum / sampleCount / (255.0 * 255.0);
                focusRaw = Math.Min(1.0, Math.Max(0.0, focusRaw));
            }

            // Converter foco para escala 0–100
            double focusScore = focusRaw * 100.0;

            // SaturationClippingFraction: fração de pixels muito escuros ou muito claros
            double clippingFrac = diagTotal > 0 ? (double)diagClip / diagTotal : 0.0;

            // Regra simples para exposição (quanto menos clipping, melhor)
            // clipping 0   -> 100 pontos
            // clipping 5%  -> ~75 pontos
            // clipping 10% -> ~50 pontos
            double exposureScore = 100.0 * (1.0 - Math.Min(1.0, clippingFrac / 0.2));

            // Máscara: fração de pixels marcados como amostra
            double foregroundFraction = (double)sampleCount / Math.Max(1, w * h);

            double maskScore;
            if (foregroundFraction < 0.3)
            {
                // Muito pouca amostra
                maskScore = 50.0 * foregroundFraction / 0.3; // sobe até 50
            }
            else if (foregroundFraction > 0.95)
            {
                // Quase a imagem inteira virou amostra (suspeito)
                maskScore = 60.0;
            }
            else
            {
                // Faixa considerada boa
                maskScore = 80.0 + 20.0 * (1.0 - Math.Abs(foregroundFraction - 0.6) / 0.3);
                maskScore = Math.Min(100.0, Math.Max(0.0, maskScore));
            }

            // Índice global de qualidade (0–100)
            double qualityIndex =
                0.4 * focusScore +
                0.3 * exposureScore +
                0.3 * maskScore;

            // Normalizar para 0–100
            qualityIndex = Math.Max(0.0, Math.Min(100.0, qualityIndex));

            // Status do laudo
            string qualityStatus;
            if (qualityIndex >= 85.0)
                qualityStatus = "Official";
            else if (qualityIndex >= 70.0)
                qualityStatus = "Preliminary";
            else
                qualityStatus = "Invalid";

            // -----------------------------
            // DIAGNÓSTICOS E RESULTADO
            // -----------------------------

            var diag = new ImageDiagnosticsResult
            {
                FocusScore = focusRaw,
                SaturationClippingFraction = diagTotal > 0 ? (double)diagClip / diagTotal : 0,
                ForegroundFraction = (double)sampleCount / Math.Max(1, w * h),

                // NOVOS CAMPOS (se quiser expor no objeto de diagnóstico também)
                FocusScorePercent = focusScore,
                ExposureScore = exposureScore,
                MaskScore = maskScore,
                QualityIndex = qualityIndex,
                QualityStatus = qualityStatus
            };

            var result = new SampleFullAnalysisResult
            {
                Id = Guid.NewGuid(),
                ImagePath = imagePath,
                CaptureDateTimeUtc = DateTime.UtcNow,
                Diagnostics = diag,

                // Campo de qualidade direto no resultado resumido
                QualityIndex = qualityIndex,
                QualityStatus = qualityStatus
            };

            long denom = Math.Max(1, sampleCount);

            double pctAu = (double)auCount / denom;
            double pctPt = (double)ptCount / denom;

            result.Metals.Add(new MetalResult
            {
                Id = "Au",
                Name = "Ouro",
                Group = "nobre",
                PctSample = pctAu,
                PpmEstimated = pctAu > 0 ? pctAu * 1_000_000.0 : (double?)null,
                Score = Math.Min(1.0, 0.6 + pctAu * 10.0)
            });

            result.Metals.Add(new MetalResult
            {
                Id = "Pt",
                Name = "Platina",
                Group = "PGM",
                PctSample = pctPt,
                PpmEstimated = pctPt > 0 ? pctPt * 1_000_000.0 : (double?)null,
                Score = Math.Min(1.0, 0.6 + pctPt * 10.0)
            });

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
            public long Au;
            public long Pt;
            public long Other;
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
            sb.AppendLine($"Fraçao amostra: {r.Diagnostics.ForegroundFraction:P1}");
            sb.AppendLine();
            sb.AppendLine("Metais:");
            foreach (var m in r.Metals.OrderByDescending(m => m.PctSample))
            {
                var ppm = m.PpmEstimated.HasValue ? $"{m.PpmEstimated.Value:F1} ppm" : "-";
                sb.AppendLine($" - {m.Name} ({m.Id}): {m.PctSample:P3} · {ppm} · score={m.Score:F2}");
            }
            sb.AppendLine();
            sb.AppendLine($"QualityIndex: {r.QualityIndex:F1} · Status: {r.QualityStatus}");
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