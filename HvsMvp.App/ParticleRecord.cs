using System;

namespace HvsMvp.App
{
    /// <summary>
    /// Representa uma "partícula" ou pequeno cluster de pixels de interesse.
    /// Versão simplificada para BLOCO 3A (banco básico de partículas por laudo).
    /// </summary>
    public class ParticleRecord
    {
        public Guid ParticleId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Tipo de material principal da partícula (ex.: "Au", "Pt", "Sulfeto", "Ganga").
        /// </summary>
        public string MaterialId { get; set; } = "Unknown";

        /// <summary>
        /// Score/confiança média atribuída pelo modelo para este material (0..1).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Área aproximada em número de pixels marcados como pertencentes à partícula.
        /// </summary>
        public int ApproxAreaPixels { get; set; }

        /// <summary>
        /// Posição aproximada (centro de massa) em coordenadas de pixel da imagem.
        /// </summary>
        public int CenterX { get; set; }
        public int CenterY { get; set; }

        /// <summary>
        /// Identificador da análise/amostra à qual esta partícula pertence.
        /// </summary>
        public Guid AnalysisId { get; set; }
    }
}