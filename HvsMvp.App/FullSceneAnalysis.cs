using System.Drawing;

namespace HvsMvp.App
{
    /// <summary>
    /// Resultado completo de uma análise de cena HVS.
    /// </summary>
    public class FullSceneAnalysis
    {
        /// <summary>
        /// Resumo estatístico (metais/cristais/gemas, diagnósticos).
        /// </summary>
        public SampleFullAnalysisResult Summary { get; set; } = null!;

        /// <summary>
        /// Rótulos por pixel, mesmo tamanho da imagem analisada.
        /// </summary>
        public PixelLabel[,] Labels { get; set; } = null!;

        /// <summary>
        /// Máscara de amostra/fundo.
        /// </summary>
        public SampleMaskClass?[,] Mask { get; set; } = null!;

        /// <summary>
        /// Preview visual da máscara.
        /// </summary>
        public Bitmap MaskPreview { get; set; } = null!;

        public int Width { get; set; }
        public int Height { get; set; }
    }
}