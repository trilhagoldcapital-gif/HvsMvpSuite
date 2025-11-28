using System;

namespace HvsMvp.App
{
    /// <summary>
    /// Classe de máscara por pixel usada em toda a aplicação.
    /// Suporta classificação avançada de pixel (amostra/fundo/borda) com confiança.
    /// </summary>
    public class SampleMaskClass
    {
        /// <summary>
        /// True se o pixel pertence à região de amostra (não é fundo).
        /// </summary>
        public bool IsSample { get; set; }

        /// <summary>
        /// True se o pixel é classificado como fundo (background).
        /// </summary>
        public bool IsBackground { get; set; }

        /// <summary>
        /// True se o pixel está na borda entre amostra e fundo.
        /// </summary>
        public bool IsBorder { get; set; }

        /// <summary>
        /// Confiança da classificação da máscara (0..1).
        /// 1.0 = alta confiança, 0.0 = incerto.
        /// </summary>
        public double MaskConfidence { get; set; } = 1.0;

        /// <summary>
        /// Valor de cinza original do pixel (0-255) para diagnósticos.
        /// </summary>
        public byte GrayValue { get; set; }

        /// <summary>
        /// Magnitude do gradiente local (textura) para diagnósticos.
        /// </summary>
        public double GradientMagnitude { get; set; }
    }
}
