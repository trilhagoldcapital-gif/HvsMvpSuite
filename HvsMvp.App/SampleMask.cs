using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace HvsMvp.App
{
    /// <summary>
    /// Represents sample mask classification for a pixel.
    /// </summary>
    public class SampleMaskClass
    {
        /// <summary>Indicates whether this pixel is part of the sample (vs background).</summary>
        public bool IsSample { get; set; }
        
        /// <summary>Composite index used for segmentation.</summary>
        public double CompositeIndex { get; set; }
        
        /// <summary>Local gradient magnitude.</summary>
        public double GradientMagnitude { get; set; }
    }

    /// <summary>
    /// Service for robust sample mask segmentation for light background microscopy slides.
    /// Uses adaptive thresholding with texture/gradient analysis to distinguish metallic grains from background.
    /// </summary>
    public class SampleMaskService
    {
        // ===== CONFIGURABLE PARAMETERS =====
        
        /// <summary>Weight for inverse gray component in composite index (0-1).</summary>
        public double TextureWeight { get; set; } = 0.5;
        
        /// <summary>Weight for gradient component in composite index (0-1).</summary>
        public double GradientWeight { get; set; } = 0.5;
        
        /// <summary>Multiplier for standard deviation in adaptive threshold calculation.</summary>
        public double StdMultiplier { get; set; } = 0.5;
        
        /// <summary>Minimum threshold for composite index (prevents false positives on uniform areas).</summary>
        public double MinThreshold { get; set; } = 30.0;
        
        /// <summary>Maximum threshold for composite index (prevents excessive rejection).</summary>
        public double MaxThreshold { get; set; } = 180.0;
        
        /// <summary>Minimum region size in pixels to keep (removes dust/noise).</summary>
        public int MinRegionSize { get; set; } = 100;
        
        /// <summary>Border band width for background color estimation.</summary>
        public int BorderBand { get; set; } = 12;
        
        /// <summary>Whether to keep only the largest component or multiple large ones.</summary>
        public bool KeepOnlyLargest { get; set; } = false;
        
        /// <summary>Alpha transparency for background overlay in preview (0-1).</summary>
        public double BackgroundOverlayAlpha { get; set; } = 0.5;
        
        /// <summary>Maximum saturation clipping value (pixels with V > this are excluded).</summary>
        public double MaxValueClip { get; set; } = 0.98;
        
        /// <summary>Minimum saturation to consider as potentially sample.</summary>
        public double MinSaturationThreshold { get; set; } = 0.06;

        /// <summary>
        /// Build sample mask and preview for a given bitmap.
        /// Algorithm:
        /// 1. Convert to grayscale
        /// 2. Calculate gradient magnitude (edge detection)
        /// 3. Compute composite index: idx = (255 - gray) * TextureWeight + grad * GradientWeight
        /// 4. Calculate adaptive threshold: threshold = clamp(mean + StdMultiplier * std, MinThreshold, MaxThreshold)
        /// 5. Binarize: isSample = idx >= threshold
        /// 6. Filter by region size (BFS/DFS)
        /// 7. Generate preview with translucent blue overlay on background
        /// </summary>
        public (SampleMaskClass[,] mask, Bitmap preview) BuildMask(Bitmap bmp)
        {
            using var src = Ensure24bpp(bmp);
            
            int w = src.Width, h = src.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            
            byte[] buf;
            int stride;
            
            try
            {
                stride = data.Stride;
                int bytes = stride * h;
                buf = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);
            }
            finally
            {
                src.UnlockBits(data);
            }

            // Step 1: Estimate background color from border region
            double bgR = 0, bgG = 0, bgB = 0;
            long bgCount = 0;
            
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = x < BorderBand || x >= w - BorderBand || 
                                   y < BorderBand || y >= h - BorderBand;
                    if (!isBorder) continue;
                    
                    int off = y * stride + x * 3;
                    bgB += buf[off + 0];
                    bgG += buf[off + 1];
                    bgR += buf[off + 2];
                    bgCount++;
                }
            }
            
            if (bgCount > 0)
            {
                bgR /= bgCount;
                bgG /= bgCount;
                bgB /= bgCount;
            }

            // Step 2: Compute grayscale and gradient arrays
            double[,] gray = new double[w, h];
            double[,] grad = new double[w, h];
            double[,] compositeIdx = new double[w, h];
            
            // First pass: compute grayscale
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int off = row + x * 3;
                    double B = buf[off + 0];
                    double G = buf[off + 1];
                    double R = buf[off + 2];
                    gray[x, y] = 0.299 * R + 0.587 * G + 0.114 * B;
                }
            }
            
            // Second pass: compute gradient magnitude (Sobel-like)
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    double gx = gray[x + 1, y] - gray[x - 1, y];
                    double gy = gray[x, y + 1] - gray[x, y - 1];
                    grad[x, y] = Math.Sqrt(gx * gx + gy * gy);
                }
            }
            
            // Step 3: Compute composite index for each pixel
            // idx = (255 - gray) * TextureWeight + grad * GradientWeight
            double sumIdx = 0;
            double sumIdxSq = 0;
            long pixelCount = 0;
            
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int off = row + x * 3;
                    double R = buf[off + 2];
                    double G = buf[off + 1];
                    double B = buf[off + 0];
                    
                    // Check if pixel might be saturated (white) - skip these
                    RgbToHsvFast((byte)R, (byte)G, (byte)B, out _, out double S, out double V);
                    if (V > MaxValueClip || V < 0.02)
                    {
                        compositeIdx[x, y] = 0; // Force background
                        continue;
                    }
                    
                    double invGray = 255.0 - gray[x, y];
                    double idx = invGray * TextureWeight + grad[x, y] * GradientWeight;
                    
                    // Boost for saturated colors (metals often have distinct colors)
                    if (S > MinSaturationThreshold)
                    {
                        idx += S * 30.0;
                    }
                    
                    // Penalize pixels very close to estimated background
                    double colorDist = Math.Sqrt((R - bgR) * (R - bgR) + (G - bgG) * (G - bgG) + (B - bgB) * (B - bgB));
                    if (colorDist < 20)
                    {
                        idx *= 0.3; // Likely background
                    }
                    else if (colorDist > 60)
                    {
                        idx *= 1.2; // Likely sample
                    }
                    
                    compositeIdx[x, y] = idx;
                    sumIdx += idx;
                    sumIdxSq += idx * idx;
                    pixelCount++;
                }
            }
            
            // Step 4: Calculate adaptive threshold
            double meanIdx = pixelCount > 0 ? sumIdx / pixelCount : 0;
            double variance = pixelCount > 0 ? (sumIdxSq / pixelCount) - (meanIdx * meanIdx) : 0;
            double stdIdx = Math.Sqrt(Math.Max(0, variance));
            
            double threshold = meanIdx + StdMultiplier * stdIdx;
            threshold = Math.Max(MinThreshold, Math.Min(MaxThreshold, threshold));
            
            // Step 5: Binarize into foreground mask
            bool[,] fg = new bool[w, h];
            
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    fg[x, y] = compositeIdx[x, y] >= threshold;
                }
            }
            
            // Step 6: Remove border-touching components
            RemoveBorderTouchingComponents(fg, w, h);
            
            // Step 7: Filter by region size
            FilterSmallRegions(fg, w, h, MinRegionSize, KeepOnlyLargest);
            
            // Step 8: Optional morphological closing to fill small holes
            CloseSmallHoles(fg, w, h, maxHoleSize: 50);
            
            // Step 9: Build final mask and preview
            var mask = new SampleMaskClass[w, h];
            var preview = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            
            var pdata = preview.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                int outStride = pdata.Stride;
                byte[] outBuf = new byte[outStride * h];
                
                // Blue overlay color for non-sample areas
                byte overlayB = 200;
                byte overlayG = 100;
                byte overlayR = 0;
                
                for (int y = 0; y < h; y++)
                {
                    int srcRow = y * stride;
                    int dstRow = y * outStride;
                    
                    for (int x = 0; x < w; x++)
                    {
                        bool isSample = fg[x, y];
                        mask[x, y] = new SampleMaskClass
                        {
                            IsSample = isSample,
                            CompositeIndex = compositeIdx[x, y],
                            GradientMagnitude = grad[x, y]
                        };
                        
                        int srcOff = srcRow + x * 3;
                        int dstOff = dstRow + x * 3;
                        
                        byte srcB = buf[srcOff + 0];
                        byte srcG = buf[srcOff + 1];
                        byte srcR = buf[srcOff + 2];
                        
                        if (isSample)
                        {
                            // Sample pixels: preserve original color
                            outBuf[dstOff + 0] = srcB;
                            outBuf[dstOff + 1] = srcG;
                            outBuf[dstOff + 2] = srcR;
                        }
                        else
                        {
                            // Background pixels: blue translucent overlay
                            double alpha = BackgroundOverlayAlpha;
                            outBuf[dstOff + 0] = (byte)(srcB * (1 - alpha) + overlayB * alpha);
                            outBuf[dstOff + 1] = (byte)(srcG * (1 - alpha) + overlayG * alpha);
                            outBuf[dstOff + 2] = (byte)(srcR * (1 - alpha) + overlayR * alpha);
                        }
                    }
                }
                
                System.Runtime.InteropServices.Marshal.Copy(outBuf, 0, pdata.Scan0, outBuf.Length);
            }
            finally
            {
                preview.UnlockBits(pdata);
            }
            
            return (mask, preview);
        }

        private static Bitmap Ensure24bpp(Bitmap src)
        {
            if (src.PixelFormat == PixelFormat.Format24bppRgb)
                return (Bitmap)src.Clone();
                
            var clone = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(clone))
            {
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
            }
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
            
            if (delta == 0)
            {
                h = 0;
                return;
            }
            
            double hue;
            if (max == rd)
                hue = 60 * (((gd - bd) / delta) % 6);
            else if (max == gd)
                hue = 60 * (((bd - rd) / delta) + 2);
            else
                hue = 60 * (((rd - gd) / delta) + 4);
                
            if (hue < 0) hue += 360;
            h = hue;
        }

        /// <summary>
        /// Remove all components that touch the image border (likely background artifacts).
        /// </summary>
        private static void RemoveBorderTouchingComponents(bool[,] fg, int w, int h)
        {
            var queue = new Queue<(int x, int y)>();
            bool[,] visited = new bool[w, h];
            
            void Enqueue(int x, int y)
            {
                if (x < 0 || x >= w || y < 0 || y >= h) return;
                if (visited[x, y] || !fg[x, y]) return;
                visited[x, y] = true;
                queue.Enqueue((x, y));
            }
            
            // Seed from all border pixels
            for (int x = 0; x < w; x++)
            {
                Enqueue(x, 0);
                Enqueue(x, h - 1);
            }
            for (int y = 0; y < h; y++)
            {
                Enqueue(0, y);
                Enqueue(w - 1, y);
            }
            
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                fg[cx, cy] = false;
                
                for (int k = 0; k < 8; k++)
                {
                    Enqueue(cx + dx[k], cy + dy[k]);
                }
            }
        }

        /// <summary>
        /// Filter out regions smaller than minSize pixels.
        /// If keepOnlyLargest is true, only the largest component is kept.
        /// </summary>
        private static void FilterSmallRegions(bool[,] fg, int w, int h, int minSize, bool keepOnlyLargest)
        {
            bool[,] visited = new bool[w, h];
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            
            var allComponents = new List<List<(int x, int y)>>();
            
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!fg[x, y] || visited[x, y]) continue;
                    
                    // BFS to find component
                    var queue = new Queue<(int, int)>();
                    var component = new List<(int, int)>();
                    
                    visited[x, y] = true;
                    queue.Enqueue((x, y));
                    component.Add((x, y));
                    
                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        
                        for (int k = 0; k < 8; k++)
                        {
                            int nx = cx + dx[k];
                            int ny = cy + dy[k];
                            
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            if (visited[nx, ny] || !fg[nx, ny]) continue;
                            
                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                            component.Add((nx, ny));
                        }
                    }
                    
                    allComponents.Add(component);
                }
            }
            
            // Clear all foreground first
            for (int y2 = 0; y2 < h; y2++)
                for (int x2 = 0; x2 < w; x2++)
                    fg[x2, y2] = false;
            
            if (keepOnlyLargest)
            {
                // Find and keep only the largest component >= minSize
                List<(int, int)>? largest = null;
                int maxSize = 0;
                
                foreach (var comp in allComponents)
                {
                    if (comp.Count > maxSize && comp.Count >= minSize)
                    {
                        maxSize = comp.Count;
                        largest = comp;
                    }
                }
                
                if (largest != null)
                {
                    foreach (var (px, py) in largest)
                        fg[px, py] = true;
                }
            }
            else
            {
                // Keep all components >= minSize
                foreach (var comp in allComponents)
                {
                    if (comp.Count >= minSize)
                    {
                        foreach (var (px, py) in comp)
                            fg[px, py] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Close small holes inside sample regions using morphological closing.
        /// </summary>
        private static void CloseSmallHoles(bool[,] fg, int w, int h, int maxHoleSize)
        {
            // Invert the mask and find holes (background regions completely surrounded by foreground)
            bool[,] inverted = new bool[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    inverted[x, y] = !fg[x, y];
            
            bool[,] visited = new bool[w, h];
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            
            // Mark border-connected background as visited (these are not holes)
            var borderQueue = new Queue<(int, int)>();
            
            for (int x = 0; x < w; x++)
            {
                if (inverted[x, 0] && !visited[x, 0]) { visited[x, 0] = true; borderQueue.Enqueue((x, 0)); }
                if (inverted[x, h - 1] && !visited[x, h - 1]) { visited[x, h - 1] = true; borderQueue.Enqueue((x, h - 1)); }
            }
            for (int y = 0; y < h; y++)
            {
                if (inverted[0, y] && !visited[0, y]) { visited[0, y] = true; borderQueue.Enqueue((0, y)); }
                if (inverted[w - 1, y] && !visited[w - 1, y]) { visited[w - 1, y] = true; borderQueue.Enqueue((w - 1, y)); }
            }
            
            while (borderQueue.Count > 0)
            {
                var (cx, cy) = borderQueue.Dequeue();
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + dx[k], ny = cy + dy[k];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (visited[nx, ny] || !inverted[nx, ny]) continue;
                    visited[nx, ny] = true;
                    borderQueue.Enqueue((nx, ny));
                }
            }
            
            // Now find remaining holes (background not connected to border)
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!inverted[x, y] || visited[x, y]) continue;
                    
                    // Found a hole - measure its size
                    var hole = new List<(int, int)>();
                    var queue = new Queue<(int, int)>();
                    
                    visited[x, y] = true;
                    queue.Enqueue((x, y));
                    hole.Add((x, y));
                    
                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        for (int k = 0; k < 8; k++)
                        {
                            int nx = cx + dx[k], ny = cy + dy[k];
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            if (visited[nx, ny] || !inverted[nx, ny]) continue;
                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                            hole.Add((nx, ny));
                        }
                    }
                    
                    // If hole is small enough, fill it
                    if (hole.Count <= maxHoleSize)
                    {
                        foreach (var (hx, hy) in hole)
                            fg[hx, hy] = true;
                    }
                }
            }
        }
    }
}
