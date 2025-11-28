using System;
using System.Collections.Generic;

namespace HvsMvp.App
{
    /// <summary>
    /// Representa uma "partícula" ou pequeno cluster de pixels de interesse.
    /// Versão expandida com features de forma, composição e métricas de qualidade.
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
        /// Área física estimada (µm²) se escala calibrada disponível.
        /// </summary>
        public double? AreaPhysical { get; set; }

        /// <summary>
        /// Posição aproximada (centro de massa) em coordenadas de pixel da imagem.
        /// </summary>
        public int CenterX { get; set; }
        public int CenterY { get; set; }

        /// <summary>
        /// Identificador da análise/amostra à qual esta partícula pertence.
        /// </summary>
        public Guid AnalysisId { get; set; }

        // ============================================
        // Features de forma
        // ============================================

        /// <summary>
        /// Circularidade da partícula (0..1). 1 = círculo perfeito.
        /// Calculado como 4π * área / perímetro².
        /// </summary>
        public double Circularity { get; set; }

        /// <summary>
        /// Razão de aspecto (eixo maior / eixo menor).
        /// </summary>
        public double AspectRatio { get; set; } = 1.0;

        /// <summary>
        /// Comprimento do eixo maior em pixels.
        /// </summary>
        public double MajorAxisLength { get; set; }

        /// <summary>
        /// Comprimento do eixo menor em pixels.
        /// </summary>
        public double MinorAxisLength { get; set; }

        /// <summary>
        /// Perímetro da partícula em pixels.
        /// </summary>
        public int Perimeter { get; set; }

        /// <summary>
        /// Bounding box: X mínimo.
        /// </summary>
        public int BoundingBoxX { get; set; }

        /// <summary>
        /// Bounding box: Y mínimo.
        /// </summary>
        public int BoundingBoxY { get; set; }

        /// <summary>
        /// Bounding box: largura.
        /// </summary>
        public int BoundingBoxWidth { get; set; }

        /// <summary>
        /// Bounding box: altura.
        /// </summary>
        public int BoundingBoxHeight { get; set; }

        // ============================================
        // Composição (para partículas mistas)
        // ============================================

        /// <summary>
        /// Composição da partícula: fração de cada material dentro dela.
        /// MaterialId -> fração (0..1).
        /// </summary>
        public Dictionary<string, double> Composition { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// True se a partícula contém múltiplos materiais.
        /// </summary>
        public bool IsMixed => Composition.Count > 1;

        // ============================================
        // Métricas de qualidade/confiança
        // ============================================

        /// <summary>
        /// Confiança média ponderada dos PixelLabels dentro da partícula.
        /// </summary>
        public double AveragePixelConfidence { get; set; }

        /// <summary>
        /// Desvio padrão das confianças dos pixels.
        /// </summary>
        public double ConfidenceStdDev { get; set; }

        /// <summary>
        /// Valores HSV médios da partícula (para debug/validação).
        /// </summary>
        public double AvgH { get; set; }
        public double AvgS { get; set; }
        public double AvgV { get; set; }

        /// <summary>
        /// Score HVS da partícula (componente heurístico).
        /// </summary>
        public double ScoreHvs { get; set; }

        /// <summary>
        /// Score IA da partícula (componente modelo, por enquanto stub).
        /// </summary>
        public double ScoreIa { get; set; }

        /// <summary>
        /// Score combinado (fusão HVS + IA).
        /// </summary>
        public double ScoreCombined { get; set; }
    }

    /// <summary>
    /// Features calculadas para uma partícula.
    /// Usado internamente durante análise antes de preencher ParticleRecord.
    /// </summary>
    public class ParticleFeatures
    {
        public int PixelCount { get; set; }
        public long SumX { get; set; }
        public long SumY { get; set; }
        public int MinX { get; set; } = int.MaxValue;
        public int MaxX { get; set; } = int.MinValue;
        public int MinY { get; set; } = int.MaxValue;
        public int MaxY { get; set; } = int.MinValue;
        public int BorderPixelCount { get; set; }

        public double SumH { get; set; }
        public double SumS { get; set; }
        public double SumV { get; set; }
        public double SumConfidence { get; set; }
        public double SumConfidenceSq { get; set; }

        public Dictionary<string, int> MaterialVotes { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, double> MaterialWeightedVotes { get; set; } = new Dictionary<string, double>();

        public List<(int x, int y)> Pixels { get; set; } = new List<(int x, int y)>();

        /// <summary>
        /// Calcula o centro de massa da partícula.
        /// </summary>
        public (int cx, int cy) GetCentroid()
        {
            if (PixelCount == 0) return (0, 0);
            return ((int)(SumX / PixelCount), (int)(SumY / PixelCount));
        }

        /// <summary>
        /// Calcula a área do bounding box.
        /// </summary>
        public int GetBoundingBoxArea()
        {
            return (MaxX - MinX + 1) * (MaxY - MinY + 1);
        }

        /// <summary>
        /// Calcula a extensão (área da partícula / área do bounding box).
        /// </summary>
        public double GetExtent()
        {
            int bbArea = GetBoundingBoxArea();
            return bbArea > 0 ? (double)PixelCount / bbArea : 0;
        }
    }
}