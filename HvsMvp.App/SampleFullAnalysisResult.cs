using System;
using System.Collections.Generic;

namespace HvsMvp.App
{
    /// <summary>
    /// Resultado resumido de uma análise de amostra (laudo).
    /// Centraliza:
    /// - Info básica da imagem
    /// - Diagnósticos de qualidade (BLOCO 1)
    /// - Metais, cristais e gemas
    /// - Índice e status de qualidade
    /// - Banco básico de partículas (BLOCO 3A)
    /// </summary>
    public class SampleFullAnalysisResult
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Caminho da imagem analisada (se disponível).
        /// </summary>
        public string? ImagePath { get; set; }

        /// <summary>
        /// Data/hora UTC da captura/registro da análise.
        /// </summary>
        public DateTime CaptureDateTimeUtc { get; set; }

        /// <summary>
        /// Diagnósticos agregados da imagem (foco, clipping, fração de amostra etc).
        /// Inclui também os campos de qualidade em escala 0–100 (BLOCO 1).
        /// </summary>
        public ImageDiagnosticsResult Diagnostics { get; set; } = new ImageDiagnosticsResult();

        /// <summary>
        /// Metais detectados na amostra.
        /// </summary>
        public List<MetalResult> Metals { get; set; } = new List<MetalResult>();

        /// <summary>
        /// Cristais detectados.
        /// </summary>
        public List<CrystalResult> Crystals { get; set; } = new List<CrystalResult>();

        /// <summary>
        /// Gemas detectadas.
        /// </summary>
        public List<GemResult> Gems { get; set; } = new List<GemResult>();

        /// <summary>
        /// Índice global de qualidade do laudo (0–100). BLOCO 1.
        /// </summary>
        public double QualityIndex { get; set; }

        /// <summary>
        /// Status do laudo:
        /// - Official
        /// - Preliminary
        /// - Invalid
        /// - OfficialRechecked
        /// - ReviewRequired
        /// BLOCO 1 + BLOCO 2.
        /// </summary>
        public string QualityStatus { get; set; } = "Preliminary";

        /// <summary>
        /// Resumo textual pronto para exibição / exportação .txt.
        /// </summary>
        public string ShortReport { get; set; } = string.Empty;

        /// <summary>
        /// BLOCO 3A – Banco básico de partículas:
        /// lista de partículas/clusteres para esta análise.
        /// </summary>
        public List<ParticleRecord> Particles { get; set; } = new List<ParticleRecord>();
    }

    /// <summary>
    /// Resultados agregados de diagnóstico de imagem.
    /// </summary>
    public class ImageDiagnosticsResult
    {
        /// <summary>
        /// Foco em escala 0..1 (métrica bruta a partir de gradiente).
        /// </summary>
        public double FocusScore { get; set; }

        /// <summary>
        /// Fração de pixels saturados (muito escuros ou muito claros).
        /// </summary>
        public double SaturationClippingFraction { get; set; }

        /// <summary>
        /// Fração de pixels pertencentes à amostra (máscara).
        /// </summary>
        public double ForegroundFraction { get; set; }

        /// <summary>
        /// Foco em escala 0..100 (BLOCO 1).
        /// </summary>
        public double FocusScorePercent { get; set; }

        /// <summary>
        /// Exposição em escala 0..100 (BLOCO 1).
        /// </summary>
        public double ExposureScore { get; set; }

        /// <summary>
        /// Qualidade da máscara em escala 0..100 (BLOCO 1).
        /// </summary>
        public double MaskScore { get; set; }

        /// <summary>
        /// Índice global de qualidade (0..100) (BLOCO 1).
        /// </summary>
        public double QualityIndex { get; set; }

        /// <summary>
        /// Status da qualidade (idem SampleFullAnalysisResult.QualityStatus, mas aqui por conveniência).
        /// </summary>
        public string QualityStatus { get; set; } = "Preliminary";
    }

    /// <summary>
    /// Resultado agregado para um metal.
    /// </summary>
    public class MetalResult
    {
        public string Id { get; set; } = string.Empty;      // Ex.: "Au"
        public string Name { get; set; } = string.Empty;    // Ex.: "Ouro"
        public string? Group { get; set; }                  // Ex.: "nobre", "PGM"
            = null;

        /// <summary>
        /// Fração da amostra ocupada por este metal (0..1).
        /// </summary>
        public double PctSample { get; set; }

        /// <summary>
        /// Concentração estimada em ppm (se aplicável).
        /// </summary>
        public double? PpmEstimated { get; set; }

        /// <summary>
        /// Score combinado (0..1) de indicação/presença.
        /// </summary>
        public double Score { get; set; }
    }

    /// <summary>
    /// Resultado agregado para um cristal.
    /// </summary>
    public class CrystalResult
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Fração da amostra ocupada (0..1).
        /// </summary>
        public double PctSample { get; set; }

        /// <summary>
        /// Score combinado (0..1).
        /// </summary>
        public double Score { get; set; }
    }

    /// <summary>
    /// Resultado agregado para uma gema.
    /// </summary>
    public class GemResult
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Fração da amostra ocupada (0..1).
        /// </summary>
        public double PctSample { get; set; }

        /// <summary>
        /// Score combinado (0..1).
        /// </summary>
        public double Score { get; set; }
    }
}