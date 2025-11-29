using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace HvsMvp.App
{
    /// <summary>
    /// PR14: Service for generating intelligent selective masks with "bright points" overlay.
    /// Creates professional-looking particle highlights over target materials.
    /// </summary>
    public class BrightPointsMaskService
    {
        // Default settings for bright point visualization
        private const int DefaultPointRadius = 4;
        private const double DefaultGlowRadius = 8;
        private const double DefaultOpacity = 0.85;
        private const double MinParticleConfidence = 0.5;
        private const int MinParticlePixels = 10;

        // Visualization colors per material group
        private static readonly Dictionary<string, Color> MaterialGroupColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            // Noble metals
            ["Au"] = Color.FromArgb(255, 215, 0),      // Gold
            ["Ag"] = Color.FromArgb(220, 220, 220),    // Silver
            
            // PGM (Platinum Group Metals)
            ["Pt"] = Color.FromArgb(148, 100, 168),    // Platinum - Purple
            ["Pd"] = Color.FromArgb(160, 140, 180),    // Palladium - Light lilac
            ["Rh"] = Color.FromArgb(200, 200, 230),    // Rhodium - White blue
            ["Ir"] = Color.FromArgb(130, 130, 160),    // Iridium - Blue gray
            ["Ru"] = Color.FromArgb(150, 150, 160),    // Ruthenium - Gray
            ["Os"] = Color.FromArgb(120, 120, 140),    // Osmium - Dark gray
            
            // Common metals
            ["Cu"] = Color.FromArgb(184, 115, 51),     // Copper
            ["Fe"] = Color.FromArgb(139, 69, 19),      // Iron
            ["Al"] = Color.FromArgb(200, 200, 210),    // Aluminum
            ["Ni"] = Color.FromArgb(170, 170, 185),    // Nickel
            ["Zn"] = Color.FromArgb(180, 180, 195),    // Zinc
            ["Pb"] = Color.FromArgb(90, 90, 105),      // Lead
            
            // Crystals
            ["SiO2"] = Color.FromArgb(0, 200, 255),    // Quartz - Cyan
            ["CaCO3"] = Color.FromArgb(255, 240, 200), // Calcite - Cream
            ["CaF2"] = Color.FromArgb(148, 0, 211),    // Fluorite - Violet
            
            // Gems
            ["C"] = Color.FromArgb(255, 255, 255),     // Diamond - White
            ["Al2O3_blue"] = Color.FromArgb(30, 100, 200), // Sapphire - Blue
            ["Al2O3_red"] = Color.FromArgb(200, 30, 50),   // Ruby - Red
            ["Be3Al2Si6O18"] = Color.FromArgb(0, 200, 120), // Emerald - Green
            ["SiO2_purple"] = Color.FromArgb(153, 102, 204) // Amethyst - Purple
        };

        /// <summary>
        /// Gets the visualization color for a material.
        /// </summary>
        public static Color GetMaterialColor(string? materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId))
                return Color.FromArgb(255, 255, 0); // Default yellow

            if (MaterialGroupColors.TryGetValue(materialId, out var color))
                return color;

            // Check for group matches
            string upper = materialId.ToUpperInvariant();
            
            // Check if PGM
            if (VisualizationService.IsPgmMetal(materialId))
                return Color.FromArgb(148, 100, 168); // Purple for PGM

            return Color.FromArgb(50, 200, 50); // Default green for unknown
        }

        /// <summary>
        /// PR14: Generates an intelligent bright points overlay showing target particle locations.
        /// Each particle is highlighted with a glowing bright point at its center.
        /// </summary>
        /// <param name="baseImage">Original image to overlay on.</param>
        /// <param name="scene">Analysis scene with particle data.</param>
        /// <param name="targetMaterialIds">Material IDs to highlight (null = all materials).</param>
        /// <param name="options">Visualization options.</param>
        /// <returns>Image with bright points overlay.</returns>
        public Bitmap GenerateBrightPointsOverlay(
            Bitmap baseImage,
            FullSceneAnalysis scene,
            string[]? targetMaterialIds = null,
            BrightPointsOptions? options = null)
        {
            if (baseImage == null || scene == null)
                throw new ArgumentNullException(baseImage == null ? nameof(baseImage) : nameof(scene));

            options ??= new BrightPointsOptions();

            int w = Math.Min(baseImage.Width, scene.Width);
            int h = Math.Min(baseImage.Height, scene.Height);

            // Create result bitmap
            var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Draw base image
                g.DrawImage(baseImage, new Rectangle(0, 0, w, h));

                // Apply background dimming if requested
                if (options.DimBackground)
                {
                    using var dimBrush = new SolidBrush(Color.FromArgb(
                        (int)(options.BackgroundDimOpacity * 255), 0, 0, 0));
                    g.FillRectangle(dimBrush, 0, 0, w, h);
                }

                // Collect and filter particles
                var particles = FilterParticles(scene, targetMaterialIds, options);

                // Draw bright points for each particle
                foreach (var particle in particles)
                {
                    DrawBrightPoint(g, particle, options);
                }
            }

            return result;
        }

        /// <summary>
        /// PR14: Generates a selective mask with smooth gradient glow around particles.
        /// </summary>
        public Bitmap GenerateGlowMask(
            Bitmap baseImage,
            FullSceneAnalysis scene,
            string targetMaterialId,
            BrightPointsOptions? options = null)
        {
            options ??= new BrightPointsOptions();

            int w = Math.Min(baseImage.Width, scene.Width);
            int h = Math.Min(baseImage.Height, scene.Height);

            var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var glowColor = GetMaterialColor(targetMaterialId);

            using (var g = Graphics.FromImage(result))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw base image first
                g.DrawImage(baseImage, new Rectangle(0, 0, w, h));

                // Get target pixels and build a glow effect
                var targetPixels = GetTargetPixels(scene, targetMaterialId, options.MinConfidence);

                // Create glow layer
                using var glowBitmap = CreateGlowLayer(targetPixels, w, h, glowColor, (int)options.GlowRadius);
                g.DrawImage(glowBitmap, 0, 0);
            }

            return result;
        }

        /// <summary>
        /// PR14: Creates a combined Au + PGM visualization with different colors.
        /// </summary>
        public Bitmap GenerateAuPgmBrightPoints(
            Bitmap baseImage,
            FullSceneAnalysis scene,
            BrightPointsOptions? options = null)
        {
            options ??= new BrightPointsOptions();

            int w = Math.Min(baseImage.Width, scene.Width);
            int h = Math.Min(baseImage.Height, scene.Height);

            var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(result))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw base image
                g.DrawImage(baseImage, new Rectangle(0, 0, w, h));

                // Apply background dimming
                if (options.DimBackground)
                {
                    using var dimBrush = new SolidBrush(Color.FromArgb(
                        (int)(options.BackgroundDimOpacity * 255), 0, 0, 0));
                    g.FillRectangle(dimBrush, 0, 0, w, h);
                }

                // Draw Au particles first (gold color)
                var auParticles = FilterParticles(scene, new[] { "Au" }, options);
                foreach (var particle in auParticles)
                {
                    DrawBrightPoint(g, particle, options, Color.FromArgb(255, 215, 0));
                }

                // Draw PGM particles (purple color) - use VisualizationService.PgmMetals for consistency
                var pgmIds = VisualizationService.PgmMetals.ToArray();
                var pgmParticles = FilterParticles(scene, pgmIds, options);
                foreach (var particle in pgmParticles)
                {
                    DrawBrightPoint(g, particle, options, Color.FromArgb(148, 100, 168));
                }
            }

            return result;
        }

        /// <summary>
        /// PR14: Creates a particle centroids overlay with labels.
        /// </summary>
        public Bitmap GenerateLabeledParticleOverlay(
            Bitmap baseImage,
            FullSceneAnalysis scene,
            BrightPointsOptions? options = null)
        {
            options ??= new BrightPointsOptions();

            int w = Math.Min(baseImage.Width, scene.Width);
            int h = Math.Min(baseImage.Height, scene.Height);

            var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(result))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw base image
                g.DrawImage(baseImage, new Rectangle(0, 0, w, h));

                // Draw points and labels for each particle
                using var font = new Font("Segoe UI", 7, FontStyle.Bold);
                var particles = scene.Summary?.Particles;
                if (particles == null) return result;

                int index = 1;
                foreach (var p in particles)
                {
                    if (p.Confidence < options.MinConfidence) continue;
                    if (p.ApproxAreaPixels < options.MinParticlePixels) continue;

                    var color = GetMaterialColor(p.MaterialId);

                    // Draw bright point
                    int x = p.CenterX;
                    int y = p.CenterY;

                    if (x >= 0 && x < w && y >= 0 && y < h)
                    {
                        DrawBrightPointAt(g, x, y, color, options);

                        // Draw label if enabled
                        if (options.ShowLabels)
                        {
                            string label = $"{p.MaterialId}";
                            var labelSize = g.MeasureString(label, font);
                            int labelX = x - (int)(labelSize.Width / 2);
                            int labelY = y - options.PointRadius - (int)labelSize.Height - 2;

                            // Background for label
                            using var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
                            g.FillRectangle(bgBrush, labelX - 2, labelY - 1,
                                labelSize.Width + 4, labelSize.Height + 2);

                            // Label text
                            using var textBrush = new SolidBrush(color);
                            g.DrawString(label, font, textBrush, labelX, labelY);
                        }
                    }

                    index++;
                }
            }

            return result;
        }

        private List<ParticleRecord> FilterParticles(
            FullSceneAnalysis scene,
            string[]? targetMaterialIds,
            BrightPointsOptions options)
        {
            var result = new List<ParticleRecord>();
            var particles = scene.Summary?.Particles;

            if (particles == null) return result;

            var targetSet = targetMaterialIds != null
                ? new HashSet<string>(targetMaterialIds, StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (var p in particles)
            {
                // Filter by confidence
                if (p.Confidence < options.MinConfidence) continue;

                // Filter by size
                if (p.ApproxAreaPixels < options.MinParticlePixels) continue;

                // Filter by material
                if (targetSet != null && !string.IsNullOrWhiteSpace(p.MaterialId))
                {
                    if (!targetSet.Contains(p.MaterialId)) continue;
                }

                result.Add(p);
            }

            return result;
        }

        private void DrawBrightPoint(Graphics g, ParticleRecord particle, BrightPointsOptions options, Color? overrideColor = null)
        {
            var color = overrideColor ?? GetMaterialColor(particle.MaterialId);
            int x = particle.CenterX;
            int y = particle.CenterY;

            DrawBrightPointAt(g, x, y, color, options);
        }

        private void DrawBrightPointAt(Graphics g, int x, int y, Color color, BrightPointsOptions options)
        {
            int radius = options.PointRadius;
            int glowRadius = (int)options.GlowRadius;

            // Draw outer glow (multiple passes for smooth gradient)
            for (int r = glowRadius; r > radius; r -= 2)
            {
                int alpha = (int)(60 * (1.0 - (double)(r - radius) / (glowRadius - radius)));
                using var brush = new SolidBrush(Color.FromArgb(alpha, color));
                g.FillEllipse(brush, x - r, y - r, r * 2, r * 2);
            }

            // Draw bright center point
            using var centerBrush = new SolidBrush(Color.FromArgb(230, color));
            g.FillEllipse(centerBrush, x - radius, y - radius, radius * 2, radius * 2);

            // Draw white core highlight
            int coreRadius = Math.Max(1, radius / 2);
            using var coreBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
            g.FillEllipse(coreBrush, x - coreRadius, y - coreRadius, coreRadius * 2, coreRadius * 2);
        }

        private HashSet<(int x, int y)> GetTargetPixels(FullSceneAnalysis scene, string targetMaterialId, double minConfidence)
        {
            var result = new HashSet<(int x, int y)>();

            if (scene?.Labels == null) return result;

            int w = scene.Width;
            int h = scene.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var lbl = scene.Labels[x, y];
                    if (lbl == null || !lbl.IsSample) continue;
                    if (lbl.MaterialConfidence < minConfidence) continue;
                    if (!string.Equals(lbl.MaterialId, targetMaterialId, StringComparison.OrdinalIgnoreCase)) continue;

                    result.Add((x, y));
                }
            }

            return result;
        }

        private Bitmap CreateGlowLayer(HashSet<(int x, int y)> targetPixels, int w, int h, Color glowColor, int glowRadius)
        {
            var layer = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            // Fast pixel manipulation
            var rect = new Rectangle(0, 0, w, h);
            var data = layer.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int bytes = data.Stride * h;
                byte[] buffer = new byte[bytes];

                // Initialize to transparent
                for (int i = 0; i < bytes; i += 4)
                {
                    buffer[i] = 0;     // B
                    buffer[i + 1] = 0; // G
                    buffer[i + 2] = 0; // R
                    buffer[i + 3] = 0; // A
                }

                // Apply glow around each target pixel
                foreach (var (px, py) in targetPixels)
                {
                    // Draw a small glow around each pixel
                    for (int dy = -glowRadius; dy <= glowRadius; dy++)
                    {
                        for (int dx = -glowRadius; dx <= glowRadius; dx++)
                        {
                            int nx = px + dx;
                            int ny = py + dy;

                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                            double dist = Math.Sqrt(dx * dx + dy * dy);
                            if (dist > glowRadius) continue;

                            double intensity = 1.0 - dist / glowRadius;
                            int alpha = (int)(intensity * 180);

                            int offset = ny * data.Stride + nx * 4;

                            // Blend with existing value (max operation)
                            int existingAlpha = buffer[offset + 3];
                            if (alpha > existingAlpha)
                            {
                                buffer[offset] = glowColor.B;
                                buffer[offset + 1] = glowColor.G;
                                buffer[offset + 2] = glowColor.R;
                                buffer[offset + 3] = (byte)alpha;
                            }
                        }
                    }
                }

                Marshal.Copy(buffer, 0, data.Scan0, bytes);
            }
            finally
            {
                layer.UnlockBits(data);
            }

            return layer;
        }
    }

    /// <summary>
    /// Options for bright points visualization.
    /// </summary>
    public class BrightPointsOptions
    {
        /// <summary>
        /// Radius of the bright point center in pixels.
        /// </summary>
        public int PointRadius { get; set; } = 4;

        /// <summary>
        /// Radius of the glow effect around the point.
        /// </summary>
        public double GlowRadius { get; set; } = 12;

        /// <summary>
        /// Opacity of the bright point (0-1).
        /// </summary>
        public double Opacity { get; set; } = 0.85;

        /// <summary>
        /// Minimum confidence threshold for including particles.
        /// </summary>
        public double MinConfidence { get; set; } = 0.5;

        /// <summary>
        /// Minimum particle size in pixels.
        /// </summary>
        public int MinParticlePixels { get; set; } = 10;

        /// <summary>
        /// Whether to dim the background.
        /// </summary>
        public bool DimBackground { get; set; } = true;

        /// <summary>
        /// Background dimming opacity (0-1).
        /// </summary>
        public double BackgroundDimOpacity { get; set; } = 0.3;

        /// <summary>
        /// Whether to show material labels.
        /// </summary>
        public bool ShowLabels { get; set; } = false;

        /// <summary>
        /// Use pulse/animation effect (for live mode).
        /// </summary>
        public bool AnimateGlow { get; set; } = false;
    }
}
