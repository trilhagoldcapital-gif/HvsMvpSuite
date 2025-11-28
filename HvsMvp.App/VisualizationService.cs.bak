using System;
using System.Drawing;

namespace HvsMvp.App
{
    /// <summary>
    /// Utilitários para gerar imagens de visualização a partir de máscara e rótulos.
    /// </summary>
    public static class VisualizationService
    {
        /// <summary>
        /// Gera uma imagem em que:
        /// - Pixels de fundo (IsSample=false) são sobrepostos por azul translúcido.
        /// - Pixels de amostra são mantidos como na imagem base.
        /// </summary>
        public static Bitmap BuildBackgroundMaskedView(
            Bitmap baseImage,
            SampleMaskClass?[,]? mask,
            Bitmap? maskPreview = null)
        {
            if (baseImage == null) throw new ArgumentNullException(nameof(baseImage));

            int w = baseImage.Width;
            int h = baseImage.Height;
            var result = new Bitmap(w, h);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var src = baseImage.GetPixel(x, y);
                    bool isSample = false;

                    if (mask != null &&
                        x >= 0 && y >= 0 &&
                        x < mask.GetLength(0) &&
                        y < mask.GetLength(1))
                    {
                        var m = mask[x, y];
                        if (m != null && m.IsSample)
                            isSample = true;
                    }

                    if (!isSample && maskPreview != null &&
                        x < maskPreview.Width && y < maskPreview.Height)
                    {
                        var mp = maskPreview.GetPixel(x, y);
                        if (mp.G > 80 && mp.G > mp.R + 20 && mp.G > mp.B + 20)
                            isSample = true;
                    }

                    if (!isSample)
                    {
                        Color bg = Color.FromArgb(0, 80, 200);
                        double alpha = 0.6;
                        int r = (int)(src.R * (1 - alpha) + bg.R * alpha);
                        int g = (int)(src.G * (1 - alpha) + bg.G * alpha);
                        int b = (int)(src.B * (1 - alpha) + bg.B * alpha);
                        result.SetPixel(x, y, Color.FromArgb(r, g, b));
                    }
                    else
                    {
                        result.SetPixel(x, y, src);
                    }
                }
            }

            return result;
        }
    }
}