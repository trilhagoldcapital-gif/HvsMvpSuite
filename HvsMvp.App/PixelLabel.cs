using System;

namespace HvsMvp.App
{
    /// <summary>
    /// Rótulo por pixel após análise HVS + IA.
    /// Contém classificação completa do pixel com scores combinados.
    /// </summary>
    public class PixelLabel
    {
        /// <summary>
        /// True se o pixel pertence à amostra (não é fundo).
        /// </summary>
        public bool IsSample { get; set; }

        /// <summary>
        /// ID da partícula à qual este pixel pertence (0 = não atribuído).
        /// </summary>
        public int ParticleId { get; set; }

        /// <summary>
        /// ID do material classificado (ex.: "Au", "Pt", "Ganga").
        /// </summary>
        public string? MaterialId { get; set; }

        /// <summary>
        /// Tipo de material (Metal, Crystal, Gem, Background).
        /// </summary>
        public PixelMaterialType MaterialType { get; set; } = PixelMaterialType.None;

        /// <summary>
        /// Confiança final da classificação do material (0..1).
        /// Resultado da fusão HVS + IA.
        /// </summary>
        public double MaterialConfidence { get; set; }

        /// <summary>
        /// Score bruto combinado (HVS + IA) antes de normalização.
        /// </summary>
        public double RawScore { get; set; }

        /// <summary>
        /// Score da componente HVS/heurística (0..1).
        /// </summary>
        public double ScoreHvs { get; set; }

        /// <summary>
        /// Score da componente IA (0..1). Por enquanto derivado de HVS no stub.
        /// </summary>
        public double ScoreIa { get; set; }

        // ============================================
        // Valores HSV para debug e análise
        // ============================================

        /// <summary>
        /// Hue (matiz) em graus (0-360).
        /// </summary>
        public double H { get; set; }

        /// <summary>
        /// Saturation (saturação) normalizada (0-1).
        /// </summary>
        public double S { get; set; }

        /// <summary>
        /// Value (brilho/luminosidade) normalizado (0-1).
        /// </summary>
        public double V { get; set; }

        // ============================================
        // Valores RGB para referência
        // ============================================

        /// <summary>
        /// Componente vermelho (0-255).
        /// </summary>
        public byte R { get; set; }

        /// <summary>
        /// Componente verde (0-255).
        /// </summary>
        public byte G { get; set; }

        /// <summary>
        /// Componente azul (0-255).
        /// </summary>
        public byte B { get; set; }

        // ============================================
        // Probabilidades por material (para análise avançada)
        // ============================================

        /// <summary>
        /// Probabilidades por material da classificação IA.
        /// Null se não disponível ou stub básico.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, double>? MaterialProbabilities { get; set; }
    }

    /// <summary>
    /// Tipos de material para classificação de pixels.
    /// </summary>
    public enum PixelMaterialType
    {
        /// <summary>
        /// Não classificado.
        /// </summary>
        None = 0,

        /// <summary>
        /// Metal (Au, Pt, PGM, outros metais).
        /// </summary>
        Metal = 1,

        /// <summary>
        /// Cristal.
        /// </summary>
        Crystal = 2,

        /// <summary>
        /// Gema.
        /// </summary>
        Gem = 3,

        /// <summary>
        /// Fundo/background.
        /// </summary>
        Background = 4,

        /// <summary>
        /// Ganga (material não-metal sem valor).
        /// </summary>
        Gangue = 5,

        /// <summary>
        /// Sulfeto.
        /// </summary>
        Sulfide = 6,

        /// <summary>
        /// Silicato.
        /// </summary>
        Silicate = 7
    }
}