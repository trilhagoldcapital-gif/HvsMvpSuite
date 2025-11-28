using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace HvsMvp.App
{
    /// <summary>
    /// Serviço de máscara de amostra (SampleMaskService) – versão robusta e moderna.
    ///
    /// Estratégia:
    /// - Converte para escala de cinza.
    /// - Calcula gradiente (textura) para destacar partículas contra fundo liso.
    /// - Usa limiar adaptativo com base em média e desvio padrão (cinza + gradiente).
    /// - Remove regiões pequenas de ruído (filtros morfológicos por área).
    /// - Gera preview com fundo azul translúcido.
    /// </summary>
    public class SampleMaskService
    {
        /// <summary>
        /// API antiga: retorna matriz e preview por out.
        /// </summary>
        public SampleMaskClass?[,] BuildMaskArray(Bitmap bmp, out Bitmap maskPreview)
        {
            var (mask, preview) = BuildMask(bmp);
            maskPreview = preview;
            return mask;
        }

        /// <summary>
        /// API nova: retorna tupla (máscara, preview).
        /// </summary>
        public (SampleMaskClass?[,] mask, Bitmap maskPreview) BuildMask(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            var mask = new SampleMaskClass?[w, h];

            // 1) Converte para escala de cinza e calcula gradiente simples
            byte[,] gray = new byte[w, h];
            double[,] grad = new double[w, h];

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

                // gray
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;
                        byte B = buf[off + 0];
                        byte G = buf[off + 1];
                        byte R = buf[off + 2];

                        int g = (int)(0.299 * R + 0.587 * G + 0.114 * B);
                        if (g < 0) g = 0;
                        if (g > 255) g = 255;
                        byte gv = (byte)g;
                        gray[x, y] = gv;
                    }
                }

                // gradiente (variação local)
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

                        // índice composto: cinza + gradiente
                        double idx = (255 - gC) * 0.6 + mag * 0.4;
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
                return (mask, fallback);
            }

            double mean = sumG / count;
            double varG = Math.Max(0, (sumG2 / count) - mean * mean);
            double std = Math.Sqrt(varG);

            // 2) Limiar adaptativo para índice (darker + textured)
            double threshold = mean + 0.3 * std;
            if (threshold < 20) threshold = 20;
            if (threshold > 240) threshold = 240;

            bool[,] bin = new bool[w, h];

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    byte gv = gray[x, y];
                    double mag = grad[x, y];

                    double idx = (255 - gv) * 0.6 + mag * 0.4;

                    // Fundo é claro e liso; amostra é mais escura OU mais texturizada.
                    bool isSample = idx >= threshold;

                    bin[x, y] = isSample;
                }
            }

            // 3) Limpeza morfológica por área (remove poeira)
            int minRegionSize = Math.Max(80, (w * h) / 30000); // dinâmico, mas evita grãos falsos muito pequenos
            bool[,] keep = new bool[w, h];
            bool[,] visited = new bool[w, h];

            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            var queue = new Queue<(int x, int y)>();
            var regionPixels = new List<(int x, int y)>();

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
                        foreach (var (rx, ry) in regionPixels)
                            keep[rx, ry] = true;
                    }
                }
            }

            // 4) Preenche SampleMaskClass
            long sampleCount = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!keep[x, y]) continue;

                    mask[x, y] = new SampleMaskClass { IsSample = true };
                    sampleCount++;
                }
            }

            // 5) Gera preview (fundo azul translúcido, amostra original)
            Bitmap maskPreview = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(maskPreview))
            {
                g.DrawImage(bmp, new Rectangle(0, 0, w, h));
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

                            byte fb = 200;
                            byte fg = 90;
                            byte fr = 0;
                            double alpha = 0.65;

                            bufOut[off + 0] = (byte)(b * (1 - alpha) + fb * alpha);
                            bufOut[off + 1] = (byte)(gC * (1 - alpha) + fg * alpha);
                            bufOut[off + 2] = (byte)(r * (1 - alpha) + fr * alpha);
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(bufOut, 0, prData.Scan0, bytesOut);
            }
            finally
            {
                maskPreview.UnlockBits(prData);
            }

            return (mask, maskPreview);
        }
    }
}
