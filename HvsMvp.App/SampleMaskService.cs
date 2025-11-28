using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace HvsMvp.App
{
    /// <summary>
    /// Resultado da validação da máscara de amostra.
    /// </summary>
    public class MaskValidationResult
    {
        /// <summary>
        /// Fração da imagem ocupada pela amostra (foreground).
        /// </summary>
        public double ForegroundFraction { get; set; }

        /// <summary>
        /// Número de regiões conectadas (fragmentação).
        /// </summary>
        public int RegionCount { get; set; }

        /// <summary>
        /// True se a máscara apresenta anomalias (muito pouca ou muita amostra, ou fragmentação extrema).
        /// </summary>
        public bool HasAnomalies { get; set; }

        /// <summary>
        /// Lista de mensagens de aviso sobre a máscara.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Qualidade geral da máscara (0..1).
        /// </summary>
        public double MaskQuality { get; set; } = 1.0;
    }

    /// <summary>
    /// Serviço de máscara de amostra (SampleMaskService) – versão híbrida avançada.
    ///
    /// Estratégia em duas fases:
    /// - Fase 1 (clássica/rápida):
    ///   - Converte para escala de cinza e HSV.
    ///   - Calcula gradiente (textura) para destacar partículas contra fundo liso.
    ///   - Usa limiar adaptativo com base em média e desvio padrão (cinza + gradiente).
    ///   - Remove regiões pequenas de ruído (filtros morfológicos por área).
    /// - Fase 2 (refino leve):
    ///   - Detecta bordas entre amostra e fundo.
    ///   - Calcula confiança da classificação.
    ///   - Interface preparada para futuro modelo IA de segmentação.
    /// </summary>
    public class SampleMaskService
    {
        // Thresholds para validação
        private const double MinForegroundFraction = 0.05;  // Mínimo 5% de amostra
        private const double MaxForegroundFraction = 0.95;  // Máximo 95% de amostra
        private const int MaxFragmentationRegions = 100;    // Máximo de regiões antes de alertar

        // RGB to Grayscale weights (ITU-R BT.601 standard)
        private const double GrayWeightR = 0.299;
        private const double GrayWeightG = 0.587;
        private const double GrayWeightB = 0.114;

        // Composite index weights for segmentation
        private const double GrayComponentWeight = 0.5;
        private const double GradientComponentWeight = 0.35;
        private const double SaturationComponentWeight = 0.15;

        /// <summary>
        /// API antiga: retorna matriz e preview por out.
        /// </summary>
        public SampleMaskClass?[,] BuildMaskArray(Bitmap bmp, out Bitmap maskPreview)
        {
            var (mask, preview, _) = BuildMaskWithValidation(bmp);
            maskPreview = preview;
            return mask;
        }

        /// <summary>
        /// API nova: retorna tupla (máscara, preview).
        /// </summary>
        public (SampleMaskClass?[,] mask, Bitmap maskPreview) BuildMask(Bitmap bmp)
        {
            var (mask, preview, _) = BuildMaskWithValidation(bmp);
            return (mask, preview);
        }

        /// <summary>
        /// API completa: retorna máscara, preview e resultado de validação.
        /// </summary>
        public (SampleMaskClass?[,] mask, Bitmap maskPreview, MaskValidationResult validation) BuildMaskWithValidation(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            var mask = new SampleMaskClass?[w, h];
            var validation = new MaskValidationResult();

            // ============================================
            // FASE 1: Segmentação clássica (gray/HSV + gradiente)
            // ============================================

            byte[,] gray = new byte[w, h];
            double[,] grad = new double[w, h];
            double[,] saturation = new double[w, h];

            double sumG = 0;
            double sumG2 = 0;
            long count = 0;

            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                int stride = data.Stride;
                int bytes = stride * h;
                byte[] buf = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);

                // Calcular gray e saturation
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;
                        byte B = buf[off + 0];
                        byte G = buf[off + 1];
                        byte R = buf[off + 2];

                        // Gray value using ITU-R BT.601 weights
                        int g = (int)(GrayWeightR * R + GrayWeightG * G + GrayWeightB * B);
                        g = Math.Max(0, Math.Min(255, g));
                        gray[x, y] = (byte)g;

                        // Saturation (HSV)
                        int max = Math.Max(R, Math.Max(G, B));
                        int min = Math.Min(R, Math.Min(G, B));
                        saturation[x, y] = max > 0 ? (double)(max - min) / max : 0.0;
                    }
                }

                // Calcular gradiente (textura)
                for (int y = 1; y < h - 1; y++)
                {
                    for (int x = 1; x < w - 1; x++)
                    {
                        int gC = gray[x, y];
                        int gL = gray[x - 1, y];
                        int gR = gray[x + 1, y];
                        int gU = gray[x, y - 1];
                        int gD = gray[x, y + 1];

                        int gx = gR - gL;
                        int gy = gD - gU;
                        double mag = Math.Sqrt(gx * gx + gy * gy);

                        grad[x, y] = mag;

                        // Composite index: darker + textured + saturated
                        double idx = (255 - gC) * GrayComponentWeight +
                                     mag * GradientComponentWeight +
                                     saturation[x, y] * 255 * SaturationComponentWeight;
                        sumG += idx;
                        sumG2 += idx * idx;
                        count++;
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            if (count == 0)
            {
                var fallback = (Bitmap)bmp.Clone();
                validation.HasAnomalies = true;
                validation.Warnings.Add("Imagem muito pequena para análise de máscara.");
                return (mask, fallback, validation);
            }

            double mean = sumG / count;
            double varG = Math.Max(0, (sumG2 / count) - mean * mean);
            double std = Math.Sqrt(varG);

            // Limiar adaptativo
            double threshold = mean + 0.3 * std;
            threshold = Math.Max(20, Math.Min(240, threshold));

            bool[,] bin = new bool[w, h];
            double[,] confidence = new double[w, h];

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    byte gv = gray[x, y];
                    double mag = grad[x, y];
                    double sat = saturation[x, y];

                    double idx = (255 - gv) * GrayComponentWeight +
                                 mag * GradientComponentWeight +
                                 sat * 255 * SaturationComponentWeight;
                    bool isSample = idx >= threshold;
                    bin[x, y] = isSample;

                    // Calcular confiança baseada na distância ao threshold
                    double dist = Math.Abs(idx - threshold);
                    confidence[x, y] = Math.Min(1.0, dist / (std + 1.0));
                }
            }

            // Limpeza morfológica por área
            int minRegionSize = Math.Max(80, (w * h) / 30000);
            bool[,] keep = new bool[w, h];
            bool[,] visited = new bool[w, h];

            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            var queue = new Queue<(int x, int y)>();
            var regionPixels = new List<(int x, int y)>();
            int regionCount = 0;

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    if (!bin[x, y] || visited[x, y]) continue;

                    regionPixels.Clear();
                    queue.Clear();

                    visited[x, y] = true;
                    queue.Enqueue((x, y));
                    regionPixels.Add((x, y));

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        for (int k = 0; k < 8; k++)
                        {
                            int nx = cx + dx[k];
                            int ny = cy + dy[k];
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (visited[nx, ny]) continue;
                            if (!bin[nx, ny]) continue;

                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                            regionPixels.Add((nx, ny));
                        }
                    }

                    if (regionPixels.Count >= minRegionSize)
                    {
                        regionCount++;
                        foreach (var (rx, ry) in regionPixels)
                            keep[rx, ry] = true;
                    }
                }
            }

            // ============================================
            // FASE 2: Refino e detecção de bordas
            // ============================================

            bool[,] isBorder = new bool[w, h];

            // Detectar pixels de borda (amostra adjacente a fundo)
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    if (!keep[x, y]) continue;

                    // Verificar se há vizinhos de fundo
                    bool hasBgNeighbor = false;
                    for (int k = 0; k < 8; k++)
                    {
                        int nx = x + dx[k];
                        int ny = y + dy[k];
                        if (!keep[nx, ny])
                        {
                            hasBgNeighbor = true;
                            break;
                        }
                    }
                    isBorder[x, y] = hasBgNeighbor;
                }
            }

            // Preencher SampleMaskClass com informações completas
            long sampleCount = 0;
            long borderCount = 0;
            double totalConfidence = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var mc = new SampleMaskClass
                    {
                        GrayValue = gray[x, y],
                        GradientMagnitude = grad[x, y]
                    };

                    if (keep[x, y])
                    {
                        mc.IsSample = true;
                        mc.IsBackground = false;
                        mc.IsBorder = isBorder[x, y];
                        mc.MaskConfidence = confidence[x, y];

                        sampleCount++;
                        if (isBorder[x, y]) borderCount++;
                        totalConfidence += confidence[x, y];
                    }
                    else
                    {
                        mc.IsSample = false;
                        mc.IsBackground = true;
                        mc.IsBorder = false;
                        mc.MaskConfidence = confidence[x, y];
                    }

                    mask[x, y] = mc;
                }
            }

            // ============================================
            // Validação da máscara
            // ============================================

            long totalPixels = (long)w * h;
            validation.ForegroundFraction = (double)sampleCount / totalPixels;
            validation.RegionCount = regionCount;
            validation.MaskQuality = sampleCount > 0 ? totalConfidence / sampleCount : 0.0;

            // Verificar anomalias
            if (validation.ForegroundFraction < MinForegroundFraction)
            {
                validation.HasAnomalies = true;
                validation.Warnings.Add($"Fração de amostra muito baixa ({validation.ForegroundFraction:P1}). Possível problema de iluminação ou ausência de amostra.");
            }
            else if (validation.ForegroundFraction > MaxForegroundFraction)
            {
                validation.HasAnomalies = true;
                validation.Warnings.Add($"Fração de amostra muito alta ({validation.ForegroundFraction:P1}). Possível problema de fundo ou excesso de material.");
            }

            if (regionCount > MaxFragmentationRegions)
            {
                validation.HasAnomalies = true;
                validation.Warnings.Add($"Fragmentação extrema ({regionCount} regiões). Considere ajustar iluminação ou foco.");
            }

            if (regionCount == 0)
            {
                validation.HasAnomalies = true;
                validation.Warnings.Add("Nenhuma região de amostra detectada.");
            }

            // Gerar preview
            Bitmap maskPreview = GenerateMaskPreview(bmp, keep, isBorder, w, h);

            return (mask, maskPreview, validation);
        }

        /// <summary>
        /// Gera preview da máscara com fundo azul translúcido e bordas destacadas.
        /// </summary>
        private Bitmap GenerateMaskPreview(Bitmap bmp, bool[,] keep, bool[,] isBorder, int w, int h)
        {
            var rect = new Rectangle(0, 0, w, h);
            Bitmap maskPreview = new Bitmap(w, h, PixelFormat.Format24bppRgb);

            using (var g = Graphics.FromImage(maskPreview))
            {
                g.DrawImage(bmp, rect);
            }

            var prData = maskPreview.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                int strideOut = prData.Stride;
                int bytesOut = strideOut * h;
                byte[] bufOut = new byte[bytesOut];
                System.Runtime.InteropServices.Marshal.Copy(prData.Scan0, bufOut, 0, bytesOut);

                for (int y = 0; y < h; y++)
                {
                    int row = y * strideOut;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;

                        if (!keep[x, y])
                        {
                            // Fundo azul translúcido
                            byte b = bufOut[off + 0];
                            byte gC = bufOut[off + 1];
                            byte r = bufOut[off + 2];

                            const byte fb = 200, fg = 90, fr = 0;
                            const double alpha = 0.65;

                            bufOut[off + 0] = (byte)(b * (1 - alpha) + fb * alpha);
                            bufOut[off + 1] = (byte)(gC * (1 - alpha) + fg * alpha);
                            bufOut[off + 2] = (byte)(r * (1 - alpha) + fr * alpha);
                        }
                        else if (isBorder[x, y])
                        {
                            // Borda em amarelo suave
                            byte b = bufOut[off + 0];
                            byte gC = bufOut[off + 1];
                            byte r = bufOut[off + 2];

                            const byte yb = 0, yg = 200, yr = 255;
                            const double alpha = 0.3;

                            bufOut[off + 0] = (byte)(b * (1 - alpha) + yb * alpha);
                            bufOut[off + 1] = (byte)(gC * (1 - alpha) + yg * alpha);
                            bufOut[off + 2] = (byte)(r * (1 - alpha) + yr * alpha);
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(bufOut, 0, prData.Scan0, bytesOut);
            }
            finally
            {
                maskPreview.UnlockBits(prData);
            }

            return maskPreview;
        }

        /// <summary>
        /// Stub para futura integração com modelo IA de segmentação.
        /// Por enquanto, retorna a máscara inalterada.
        /// </summary>
        public SampleMaskClass?[,] RefineWithAI(SampleMaskClass?[,] mask, Bitmap originalImage)
        {
            // TODO: Integrar com modelo ONNX/ML.NET para refino de segmentação
            // Por enquanto, retorna a máscara sem alterações (stub)
            return mask;
        }
    }
}
