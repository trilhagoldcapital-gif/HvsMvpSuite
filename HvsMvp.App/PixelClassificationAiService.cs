using System;
using System.Collections.Generic;

namespace HvsMvp.App
{
    /// <summary>
    /// Resultado da classificação IA para um pixel ou patch.
    /// </summary>
    public class AiClassificationResult
    {
        /// <summary>
        /// ID do material mais provável.
        /// </summary>
        public string MaterialId { get; set; } = "Unknown";

        /// <summary>
        /// Probabilidade/confiança do material mais provável (0..1).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Probabilidades por material (MaterialId -> probabilidade).
        /// </summary>
        public Dictionary<string, double> Probabilities { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Score IA bruto antes de normalização.
        /// </summary>
        public double RawScore { get; set; }
    }

    /// <summary>
    /// Features de entrada para classificação IA de pixel/patch.
    /// </summary>
    public class PixelFeatures
    {
        public double H { get; set; }
        public double S { get; set; }
        public double V { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public double Gradient { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>
        /// Features adicionais do contexto local (vizinhança).
        /// </summary>
        public double[]? NeighborhoodFeatures { get; set; }
    }

    /// <summary>
    /// Interface para serviço de classificação IA de pixels.
    /// Preparada para integração com modelos ONNX/ML.NET no futuro.
    /// </summary>
    public interface IPixelClassificationAiService
    {
        /// <summary>
        /// Classifica um único pixel baseado em suas features.
        /// </summary>
        AiClassificationResult ClassifyPixel(PixelFeatures features);

        /// <summary>
        /// Classifica um batch de pixels (mais eficiente para modelos IA).
        /// </summary>
        AiClassificationResult[] ClassifyBatch(PixelFeatures[] featuresBatch);

        /// <summary>
        /// Indica se o serviço está usando modelo IA real ou stub heurístico.
        /// </summary>
        bool IsUsingRealModel { get; }

        /// <summary>
        /// Nome/versão do modelo em uso.
        /// </summary>
        string ModelInfo { get; }
    }

    /// <summary>
    /// Implementação stub do serviço de classificação IA.
    /// Usa os mesmos scorings HVS como base para manter comportamento consistente.
    /// Preparado para substituição por modelo ONNX/ML.NET real no futuro.
    /// </summary>
    public class PixelClassificationAiServiceStub : IPixelClassificationAiService
    {
        public bool IsUsingRealModel => false;
        public string ModelInfo => "HVS-Heuristic-Stub-v1.0";

        // Material HSV ranges - these should ideally come from configuration
        // but are kept here for the stub implementation
        // Format: (hMin, hMax, sMin, sMax, vMin, vMax)
        private static readonly Dictionary<string, (double hMin, double hMax, double sMin, double sMax, double vMin, double vMax)> MaterialRanges =
            new Dictionary<string, (double, double, double, double, double, double)>
            {
                // Gold: yellow/golden tones
                ["Au"] = (30, 80, 0.18, 1.0, 0.35, 1.0),
                // Platinum: gray/neutral tones with low saturation
                ["Pt"] = (0, 360, 0.0, 0.20, 0.20, 0.92),
                // Sulfides: yellow/orange tones
                ["Sulfeto"] = (20, 60, 0.3, 0.8, 0.2, 0.7),
                // Silicates: blue/cyan tones
                ["Silicato"] = (180, 240, 0.1, 0.5, 0.3, 0.8),
                // Gangue: neutral colors with low saturation
                ["Ganga"] = (0, 360, 0.0, 0.15, 0.4, 0.9),
            };

        public AiClassificationResult ClassifyPixel(PixelFeatures features)
        {
            var result = new AiClassificationResult();
            var probabilities = new Dictionary<string, double>();

            // Calcular scores baseados nas heurísticas HVS para cada material
            double totalScore = 0;

            foreach (var (materialId, range) in MaterialRanges)
            {
                double score = CalculateMaterialScore(features, range);
                probabilities[materialId] = score;
                totalScore += score;
            }

            // Adicionar score para "MetalOther" como fallback
            double otherScore = 0.3;
            probabilities["MetalOther"] = otherScore;
            totalScore += otherScore;

            // Normalizar para probabilidades
            if (totalScore > 0)
            {
                foreach (var key in new List<string>(probabilities.Keys))
                {
                    probabilities[key] /= totalScore;
                }
            }

            // Encontrar material com maior probabilidade
            string bestMaterial = "MetalOther";
            double bestProb = 0;

            foreach (var (materialId, prob) in probabilities)
            {
                if (prob > bestProb)
                {
                    bestProb = prob;
                    bestMaterial = materialId;
                }
            }

            result.MaterialId = bestMaterial;
            result.Confidence = bestProb;
            result.Probabilities = probabilities;
            result.RawScore = bestProb;

            return result;
        }

        public AiClassificationResult[] ClassifyBatch(PixelFeatures[] featuresBatch)
        {
            var results = new AiClassificationResult[featuresBatch.Length];
            for (int i = 0; i < featuresBatch.Length; i++)
            {
                results[i] = ClassifyPixel(featuresBatch[i]);
            }
            return results;
        }

        private double CalculateMaterialScore(PixelFeatures features, (double hMin, double hMax, double sMin, double sMax, double vMin, double vMax) range)
        {
            double score = 0;

            // Score baseado em H (hue) - circular
            double hNorm = features.H;
            bool hInRange;
            if (range.hMin <= range.hMax)
            {
                hInRange = hNorm >= range.hMin && hNorm <= range.hMax;
            }
            else
            {
                // Range que cruza 360 (ex: vermelho)
                hInRange = hNorm >= range.hMin || hNorm <= range.hMax;
            }

            if (hInRange)
            {
                score += 0.4;
            }
            else
            {
                // Penalidade por estar fora do range
                double hDist = Math.Min(
                    Math.Abs(hNorm - range.hMin),
                    Math.Abs(hNorm - range.hMax));
                score += 0.4 * Math.Max(0, 1 - hDist / 60);
            }

            // Score baseado em S (saturation)
            if (features.S >= range.sMin && features.S <= range.sMax)
            {
                score += 0.3;
            }
            else
            {
                double sDist = Math.Min(
                    Math.Abs(features.S - range.sMin),
                    Math.Abs(features.S - range.sMax));
                score += 0.3 * Math.Max(0, 1 - sDist / 0.3);
            }

            // Score baseado em V (value/brightness)
            if (features.V >= range.vMin && features.V <= range.vMax)
            {
                score += 0.3;
            }
            else
            {
                double vDist = Math.Min(
                    Math.Abs(features.V - range.vMin),
                    Math.Abs(features.V - range.vMax));
                score += 0.3 * Math.Max(0, 1 - vDist / 0.3);
            }

            return Math.Max(0, Math.Min(1, score));
        }
    }

    /// <summary>
    /// Serviço de fusão de scores HVS e IA.
    /// </summary>
    public static class ScoreFusionService
    {
        /// <summary>
        /// Pesos padrão para fusão (podem ser ajustados via configuração).
        /// </summary>
        public static double HvsWeight { get; set; } = 0.7;
        public static double AiWeight { get; set; } = 0.3;

        /// <summary>
        /// Funde scores HVS e IA usando média ponderada.
        /// </summary>
        public static (string materialId, double confidence, double rawScore) FuseScores(
            string hvsMaterialId,
            double hvsScore,
            AiClassificationResult aiResult)
        {
            // Estratégia de fusão:
            // 1. Se HVS e IA concordam no material, boost na confiança
            // 2. Se discordam, usar ponderação

            if (string.Equals(hvsMaterialId, aiResult.MaterialId, StringComparison.OrdinalIgnoreCase))
            {
                // Concordância - boost
                double fusedScore = hvsScore * HvsWeight + aiResult.Confidence * AiWeight;
                double boostFactor = 1.1; // 10% boost por concordância
                fusedScore = Math.Min(1.0, fusedScore * boostFactor);

                return (hvsMaterialId, fusedScore, fusedScore);
            }
            else
            {
                // Discordância - escolher o de maior score ponderado
                double hvsWeighted = hvsScore * HvsWeight;
                double aiWeighted = aiResult.Confidence * AiWeight;

                if (hvsWeighted >= aiWeighted)
                {
                    return (hvsMaterialId, hvsWeighted + aiWeighted * 0.5, hvsWeighted);
                }
                else
                {
                    return (aiResult.MaterialId, aiWeighted + hvsWeighted * 0.5, aiWeighted);
                }
            }
        }

        /// <summary>
        /// Fusão simples quando usando apenas HVS (stub).
        /// </summary>
        public static (string materialId, double confidence, double rawScore) FuseHvsOnly(
            string hvsMaterialId,
            double hvsScore)
        {
            return (hvsMaterialId, hvsScore, hvsScore);
        }
    }
}
