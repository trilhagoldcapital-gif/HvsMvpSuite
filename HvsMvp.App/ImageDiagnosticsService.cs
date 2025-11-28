using System;
using System.Drawing;

namespace HvsMvp.App
{
    public class ImageDiagnosticsService
    {
        public ImageDiagnosticsResult ComputeBasicDiagnostics(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;

            long total = 0;
            long fg = 0;
            long clipping = 0;
            double gradSum = 0;

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    total++;

                    var c = bmp.GetPixel(x, y);
                    int gray = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);

                    if (gray > 5) fg++;
                    if (gray < 5 || gray > 250) clipping++;

                    var cx1 = bmp.GetPixel(x - 1, y);
                    var cx2 = bmp.GetPixel(x + 1, y);
                    var cy1 = bmp.GetPixel(x, y - 1);
                    var cy2 = bmp.GetPixel(x, y + 1);

                    int gx = (int)(0.299 * (cx2.R - cx1.R) + 0.587 * (cx2.G - cx1.G) + 0.114 * (cx2.B - cx1.B));
                    int gy = (int)(0.299 * (cy2.R - cy1.R) + 0.587 * (cy2.G - cy1.G) + 0.114 * (cy2.B - cy1.B));
                    gradSum += gx * gx + gy * gy;
                }
            }

            if (total <= 0) total = 1;
            double focusScore = gradSum / total / (255.0 * 255.0);
            focusScore = Math.Min(1.0, focusScore);

            return new ImageDiagnosticsResult
            {
                FocusScore = focusScore,
                SaturationClippingFraction = (double)clipping / total,
                ForegroundFraction = (double)fg / total
            };
        }
    }
}
