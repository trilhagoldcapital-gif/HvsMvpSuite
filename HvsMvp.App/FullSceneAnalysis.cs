using System.Drawing;

namespace HvsMvp.App
{
    /// <summary>
    /// Complete HVS scene analysis result.
    /// </summary>
    public class FullSceneAnalysis
    {
        /// <summary>
        /// Statistical summary (metals/crystals/gems, diagnostics).
        /// </summary>
        public SampleFullAnalysisResult Summary { get; set; } = new SampleFullAnalysisResult();

        /// <summary>
        /// Per-pixel labels, same size as analyzed image.
        /// </summary>
        public PixelLabel[,] Labels { get; set; } = new PixelLabel[0, 0];

        /// <summary>
        /// Sample/background mask.
        /// </summary>
        public SampleMaskClass?[,] Mask { get; set; } = new SampleMaskClass?[0, 0];

        /// <summary>
        /// Visual mask preview.
        /// </summary>
        public Bitmap MaskPreview { get; set; } = new Bitmap(1, 1);

        public int Width { get; set; }
        public int Height { get; set; }
    }
}