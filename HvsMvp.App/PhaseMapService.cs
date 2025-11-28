using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace HvsMvp.App
{
    /// <summary>
    /// Cores padrão para materiais no mapa de fases.
    /// </summary>
    public static class PhaseMapColors
    {
        // Metais nobres
        public static readonly Color Gold = Color.FromArgb(255, 215, 0);           // Au - Dourado
        public static readonly Color Platinum = Color.FromArgb(148, 100, 168);     // Pt/PGM - Roxo/cinza
        public static readonly Color Silver = Color.FromArgb(192, 192, 192);       // Ag - Prata

        // Sulfetos
        public static readonly Color Sulfide = Color.FromArgb(255, 165, 0);        // Sulfetos - Laranja
        public static readonly Color Pyrite = Color.FromArgb(218, 165, 32);        // Pirita - Dourado escuro

        // Silicatos/Ganga
        public static readonly Color Silicate = Color.FromArgb(0, 191, 255);       // Silicatos - Azul
        public static readonly Color Gangue = Color.FromArgb(100, 149, 237);       // Ganga - Azul claro

        // Outros metais
        public static readonly Color Copper = Color.FromArgb(184, 115, 51);        // Cobre
        public static readonly Color Iron = Color.FromArgb(139, 69, 19);           // Ferro
        public static readonly Color OtherMetal = Color.FromArgb(46, 139, 87);     // Outros - Verde

        // Background
        public static readonly Color Background = Color.FromArgb(30, 30, 40);      // Fundo escuro

        // Desconhecido
        public static readonly Color Unknown = Color.FromArgb(128, 128, 128);      // Cinza

        /// <summary>
        /// Obtém a cor para um material pelo ID.
        /// </summary>
        public static Color GetColorForMaterial(string? materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId))
                return Unknown;

            return materialId.ToUpperInvariant() switch
            {
                "AU" => Gold,
                "OURO" => Gold,
                "GOLD" => Gold,

                "PT" => Platinum,
                "PLATINA" => Platinum,
                "PGM" => Platinum,
                "PLATINUM" => Platinum,

                "AG" => Silver,
                "PRATA" => Silver,
                "SILVER" => Silver,

                "SULFETO" => Sulfide,
                "SULFIDE" => Sulfide,
                "PIRITA" => Pyrite,
                "PYRITE" => Pyrite,

                "SILICATO" => Silicate,
                "SILICATE" => Silicate,
                "GANGA" => Gangue,
                "GANGUE" => Gangue,

                "CU" => Copper,
                "COBRE" => Copper,
                "COPPER" => Copper,

                "FE" => Iron,
                "FERRO" => Iron,
                "IRON" => Iron,

                "METALOTHER" => OtherMetal,
                "OTHER" => OtherMetal,

                _ => Unknown
            };
        }
    }

    /// <summary>
    /// Serviço para geração de mapas de fases e heatmaps.
    /// </summary>
    public class PhaseMapService
    {
        /// <summary>
        /// Gera um mapa de fases onde cada pixel da amostra recebe cor fixa conforme material dominante.
        /// </summary>
        public Bitmap GeneratePhaseMap(FullSceneAnalysis scene)
        {
            int w = scene.Width;
            int h = scene.Height;

            var result = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, w, h);

            var data = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = data.Stride;
                int bytes = stride * h;
                byte[] buf = new byte[bytes];

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;

                        var lbl = scene.Labels[x, y];
                        Color c;

                        if (lbl == null || !lbl.IsSample || lbl.MaterialType == PixelMaterialType.Background)
                        {
                            c = PhaseMapColors.Background;
                        }
                        else
                        {
                            c = PhaseMapColors.GetColorForMaterial(lbl.MaterialId);
                        }

                        buf[off + 0] = c.B;
                        buf[off + 1] = c.G;
                        buf[off + 2] = c.R;
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, bytes);
            }
            finally
            {
                result.UnlockBits(data);
            }

            return result;
        }

        /// <summary>
        /// Gera um heatmap para um material alvo específico.
        /// A intensidade representa a confiança/presença do material.
        /// </summary>
        public Bitmap GenerateTargetHeatmap(
            Bitmap baseImage,
            FullSceneAnalysis scene,
            string targetMaterialId,
            double opacity = 0.6)
        {
            int w = scene.Width;
            int h = scene.Height;

            // Clone da imagem base
            var result = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, w, h);

            // Cor do heatmap para o material alvo
            Color targetColor = PhaseMapColors.GetColorForMaterial(targetMaterialId);

            var srcData = baseImage.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            try
            {
                int stride = srcData.Stride;
                int bytes = stride * h;
                byte[] srcBuf = new byte[bytes];
                byte[] dstBuf = new byte[bytes];

                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcBuf, 0, bytes);

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;

                        byte srcB = srcBuf[off + 0];
                        byte srcG = srcBuf[off + 1];
                        byte srcR = srcBuf[off + 2];

                        var lbl = scene.Labels[x, y];

                        if (lbl != null && lbl.IsSample &&
                            string.Equals(lbl.MaterialId, targetMaterialId, StringComparison.OrdinalIgnoreCase))
                        {
                            // Pixel é do material alvo - aplicar heatmap
                            double conf = lbl.MaterialConfidence;
                            double alpha = opacity * conf; // Opacidade proporcional à confiança

                            dstBuf[off + 0] = (byte)(srcB * (1 - alpha) + targetColor.B * alpha);
                            dstBuf[off + 1] = (byte)(srcG * (1 - alpha) + targetColor.G * alpha);
                            dstBuf[off + 2] = (byte)(srcR * (1 - alpha) + targetColor.R * alpha);
                        }
                        else if (lbl == null || !lbl.IsSample)
                        {
                            // Background - escurecer levemente
                            dstBuf[off + 0] = (byte)(srcB * 0.5);
                            dstBuf[off + 1] = (byte)(srcG * 0.5);
                            dstBuf[off + 2] = (byte)(srcR * 0.5);
                        }
                        else
                        {
                            // Outro material - manter original com leve dessaturação
                            int gray = (int)(0.299 * srcR + 0.587 * srcG + 0.114 * srcB);
                            dstBuf[off + 0] = (byte)((srcB + gray) / 2);
                            dstBuf[off + 1] = (byte)((srcG + gray) / 2);
                            dstBuf[off + 2] = (byte)((srcR + gray) / 2);
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(dstBuf, 0, dstData.Scan0, bytes);
            }
            finally
            {
                baseImage.UnlockBits(srcData);
                result.UnlockBits(dstData);
            }

            return result;
        }

        /// <summary>
        /// Gera heatmap combinado mostrando múltiplos materiais com cores diferentes.
        /// </summary>
        public Bitmap GenerateMultiMaterialHeatmap(
            Bitmap baseImage,
            FullSceneAnalysis scene,
            Dictionary<string, double> materialOpacities)
        {
            int w = scene.Width;
            int h = scene.Height;

            var result = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, w, h);

            var srcData = baseImage.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            try
            {
                int stride = srcData.Stride;
                int bytes = stride * h;
                byte[] srcBuf = new byte[bytes];
                byte[] dstBuf = new byte[bytes];

                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcBuf, 0, bytes);

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;

                        byte srcB = srcBuf[off + 0];
                        byte srcG = srcBuf[off + 1];
                        byte srcR = srcBuf[off + 2];

                        var lbl = scene.Labels[x, y];

                        if (lbl != null && lbl.IsSample && !string.IsNullOrWhiteSpace(lbl.MaterialId))
                        {
                            string matId = lbl.MaterialId.ToUpperInvariant();
                            Color matColor = PhaseMapColors.GetColorForMaterial(lbl.MaterialId);

                            double opacity = 0.5; // Default
                            foreach (var (key, val) in materialOpacities)
                            {
                                if (matId.Contains(key.ToUpperInvariant()))
                                {
                                    opacity = val;
                                    break;
                                }
                            }

                            double alpha = opacity * lbl.MaterialConfidence;

                            dstBuf[off + 0] = (byte)(srcB * (1 - alpha) + matColor.B * alpha);
                            dstBuf[off + 1] = (byte)(srcG * (1 - alpha) + matColor.G * alpha);
                            dstBuf[off + 2] = (byte)(srcR * (1 - alpha) + matColor.R * alpha);
                        }
                        else if (lbl == null || !lbl.IsSample)
                        {
                            // Background
                            dstBuf[off + 0] = (byte)(srcB * 0.4);
                            dstBuf[off + 1] = (byte)(srcG * 0.4);
                            dstBuf[off + 2] = (byte)(srcR * 0.4);
                        }
                        else
                        {
                            // Mantém original
                            dstBuf[off + 0] = srcB;
                            dstBuf[off + 1] = srcG;
                            dstBuf[off + 2] = srcR;
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(dstBuf, 0, dstData.Scan0, bytes);
            }
            finally
            {
                baseImage.UnlockBits(srcData);
                result.UnlockBits(dstData);
            }

            return result;
        }

        /// <summary>
        /// Gera legenda para o mapa de fases.
        /// </summary>
        public Bitmap GenerateLegend(IEnumerable<string> materialIds, int itemHeight = 24)
        {
            var materials = new List<string>(materialIds);
            int width = 150;
            int height = materials.Count * itemHeight + 10;

            var legend = new Bitmap(width, height);
            using (var g = Graphics.FromImage(legend))
            {
                g.Clear(Color.FromArgb(20, 20, 30));

                using var font = new Font("Segoe UI", 9);
                int y = 5;

                foreach (var matId in materials)
                {
                    var color = PhaseMapColors.GetColorForMaterial(matId);

                    // Quadrado de cor
                    using var brush = new SolidBrush(color);
                    g.FillRectangle(brush, 5, y, 20, itemHeight - 4);

                    // Nome do material
                    g.DrawString(matId, font, Brushes.White, 30, y + 2);

                    y += itemHeight;
                }
            }

            return legend;
        }
    }
}
