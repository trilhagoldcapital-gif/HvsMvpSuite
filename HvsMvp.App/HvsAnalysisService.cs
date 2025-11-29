﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace HvsMvp.App
{
    /// <summary>
    /// PR16: Confidence levels for material classification.
    /// </summary>
    public enum ConfidenceLevel
    {
        /// <summary>High confidence - clear classification with distinct signatures.</summary>
        High,
        /// <summary>Medium confidence - reasonable classification but some overlap with other materials.</summary>
        Medium,
        /// <summary>Low confidence - near classification threshold, may require verification.</summary>
        Low,
        /// <summary>Indeterminate - cannot reliably classify, prefer not to assign a specific material.</summary>
        Indeterminate
    }

    /// <summary>
    /// Resultado da classificação de um pixel com material identificado.
    /// PR16: Enhanced with confidence level and second-best alternative.
    /// </summary>
    public class PixelClassificationResult
    {
        public string MaterialId { get; set; } = "Unknown";
        public PixelMaterialType MaterialType { get; set; } = PixelMaterialType.None;
        public double Confidence { get; set; }
        
        /// <summary>PR16: Confidence level (High/Medium/Low/Indeterminate).</summary>
        public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Indeterminate;
        
        /// <summary>PR16: Second best material candidate (for analysis).</summary>
        public string? SecondBestMaterialId { get; set; }
        
        /// <summary>PR16: Score of second best candidate.</summary>
        public double SecondBestScore { get; set; }
        
        /// <summary>PR16: Gap between best and second best (higher = more confident).</summary>
        public double ScoreGap { get; set; }
    }

    /// <summary>
    /// Definição de ranges HSV para um material.
    /// </summary>
    public class MaterialHsvRange
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public PixelMaterialType Type { get; set; }
        public double HMin { get; set; }
        public double HMax { get; set; }
        public double SMin { get; set; }
        public double SMax { get; set; }
        public double VMin { get; set; }
        public double VMax { get; set; }
        public double Priority { get; set; } = 1.0; // Higher priority = checked first

        /// <summary>
        /// Verifica se os valores HSV estão dentro do range.
        /// </summary>
        public bool IsInRange(double h, double s, double v)
        {
            // Handle circular Hue range (e.g., red crosses 360)
            bool hInRange;
            if (HMin <= HMax)
            {
                hInRange = h >= HMin && h <= HMax;
            }
            else
            {
                // Range crosses 0/360 (e.g., 350-10 for red)
                hInRange = h >= HMin || h <= HMax;
            }

            bool sInRange = s >= SMin && s <= SMax;
            bool vInRange = v >= VMin && v <= VMax;

            return hInRange && sInRange && vInRange;
        }

        /// <summary>
        /// Calcula score de proximidade ao range (0..1).
        /// </summary>
        public double CalculateScore(double h, double s, double v)
        {
            double hScore = CalculateHueScore(h);
            double sScore = CalculateLinearScore(s, SMin, SMax);
            double vScore = CalculateLinearScore(v, VMin, VMax);

            // Weighted average: Hue 40%, Saturation 30%, Value 30%
            return hScore * 0.4 + sScore * 0.3 + vScore * 0.3;
        }

        private double CalculateHueScore(double h)
        {
            if (HMin <= HMax)
            {
                // Normal range
                if (h >= HMin && h <= HMax) return 1.0;
                double distMin = Math.Abs(h - HMin);
                double distMax = Math.Abs(h - HMax);
                double dist = Math.Min(distMin, distMax);
                return Math.Max(0, 1.0 - dist / 60.0);
            }
            else
            {
                // Circular range (crosses 0/360)
                if (h >= HMin || h <= HMax) return 1.0;
                // Calculate circular distance properly
                double distToMin = h > HMin ? h - HMin : 360 - HMin + h;
                double distFromMax = h < HMax ? HMax - h : 360 - h + HMax;
                double dist = Math.Min(distToMin, distFromMax);
                return Math.Max(0, 1.0 - dist / 60.0);
            }
        }

        private double CalculateLinearScore(double value, double min, double max)
        {
            if (value >= min && value <= max) return 1.0;
            double dist = value < min ? min - value : value - max;
            return Math.Max(0, 1.0 - dist / 0.3);
        }
    }

    public class HvsAnalysisService
    {
        private readonly HvsConfig _config;
        private readonly List<MaterialHsvRange> _materialRanges;

        // Constants for particle segmentation
        private const int MinParticleSizeAbsolute = 20;
        private const int MinParticleSizeDivisor = 50000;

        // Constants for material threshold exemptions (always include regardless of percentage)
        private static readonly HashSet<string> PrimaryMetalsAlwaysIncluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Au", "Pt"
        };
        private const double MinMaterialPercentageThreshold = 0.0001; // 0.01%
        
        // PR18: Confidence thresholds adjusted for better gold detection sensitivity
        // These values were carefully calibrated based on user feedback:
        // - Lower thresholds increase sensitivity (fewer false negatives)
        // - Higher gold boost factor compensates for specificity
        // - Users can fine-tune via MetalConfidenceThreshold in app settings
        private const double HighConfidenceThreshold = 0.68;   // PR18: Relaxed for more Au detection
        private const double MediumConfidenceThreshold = 0.48; // PR18: Adjusted
        private const double LowConfidenceThreshold = 0.35;    // PR18: Adjusted
        private const double IndeterminateThreshold = 0.25;    // PR18: Adjusted
        
        // PR18: Reduced minimum gap for more detections
        private const double MinScoreGapForHighConfidence = 0.10;   // PR18: Relaxed
        private const double MinScoreGapForMediumConfidence = 0.05; // PR18: Adjusted
        
        // PR18: Increased gold-specific confidence boost
        private const double GoldConfidenceBoostFactor = 1.18;
        
        // PR18: PGM vs Gold discrimination threshold - stricter to avoid Au->PGM confusion
        private const double PgmVsGoldMinSaturationGap = 0.12;

        public HvsAnalysisService(HvsConfig config)
        {
            _config = config;
            _materialRanges = BuildMaterialRanges();
        }

        /// <summary>
        /// Constrói lista de ranges HSV para todos os materiais do catálogo.
        /// PR17: Enhanced gold detection with multi-band approach and better Au/PGM discrimination.
        /// </summary>
        private List<MaterialHsvRange> BuildMaterialRanges()
        {
            var ranges = new List<MaterialHsvRange>();

            // ====== METAIS ======
            // PR18: Enhanced gold detection with expanded sub-ranges for different gold appearances
            
            // Au (Ouro) Primary - Classic yellow gold (most common)
            // PR18: Primary gold band - rich yellow/golden, HIGHEST priority - expanded range
            ranges.Add(new MaterialHsvRange
            {
                Id = "Au", Name = "Ouro", Type = PixelMaterialType.Metal,
                HMin = 35, HMax = 68, SMin = 0.25, SMax = 1.0, VMin = 0.40, VMax = 1.0,
                Priority = 1.0
            });
            
            // PR18: Au Secondary - Pale/light gold (lower saturation gold) - expanded
            ranges.Add(new MaterialHsvRange
            {
                Id = "Au", Name = "Ouro", Type = PixelMaterialType.Metal,
                HMin = 32, HMax = 62, SMin = 0.15, SMax = 0.45, VMin = 0.50, VMax = 1.0,
                Priority = 0.95
            });
            
            // PR18: Au Tertiary - Orange-gold (warmer gold tones) - expanded
            ranges.Add(new MaterialHsvRange
            {
                Id = "Au", Name = "Ouro", Type = PixelMaterialType.Metal,
                HMin = 25, HMax = 48, SMin = 0.30, SMax = 1.0, VMin = 0.45, VMax = 1.0,
                Priority = 0.92
            });
            
            // PR18: Au Deep - Deep/dark gold (less bright) - expanded
            ranges.Add(new MaterialHsvRange
            {
                Id = "Au", Name = "Ouro", Type = PixelMaterialType.Metal,
                HMin = 30, HMax = 60, SMin = 0.20, SMax = 0.85, VMin = 0.28, VMax = 0.58,
                Priority = 0.88
            });
            
            // PR18: Au Yellow Bright - Bright yellow gold
            ranges.Add(new MaterialHsvRange
            {
                Id = "Au", Name = "Ouro", Type = PixelMaterialType.Metal,
                HMin = 50, HMax = 75, SMin = 0.28, SMax = 1.0, VMin = 0.50, VMax = 1.0,
                Priority = 0.86
            });

            // Ag (Prata) - branco prateado muito brilhante - MUST be VERY bright and low saturation
            ranges.Add(new MaterialHsvRange
            {
                Id = "Ag", Name = "Prata", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.08, VMin = 0.88, VMax = 1.0,
                Priority = 0.90
            });

            // Pt (Platina) - cinza neutro médio - PR17: stricter saturation to prevent Au confusion
            ranges.Add(new MaterialHsvRange
            {
                Id = "Pt", Name = "Platina", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.10, VMin = 0.45, VMax = 0.82,
                Priority = 0.85
            });

            // Pd (Paládio) - similar a Pt mas um pouco mais claro - PR17: stricter saturation
            ranges.Add(new MaterialHsvRange
            {
                Id = "Pd", Name = "Paládio", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.12, VMin = 0.50, VMax = 0.88,
                Priority = 0.80
            });

            // PR17: Additional PGM metals with stricter saturation limits
            // Rh (Ródio) - very reflective, neutral
            ranges.Add(new MaterialHsvRange
            {
                Id = "Rh", Name = "Ródio", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.12, VMin = 0.55, VMax = 0.92,
                Priority = 0.75
            });
            
            // Ir (Irídio) - darker PGM
            ranges.Add(new MaterialHsvRange
            {
                Id = "Ir", Name = "Irídio", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.15, VMin = 0.30, VMax = 0.75,
                Priority = 0.72
            });
            
            // Ru (Rutênio) - dark gray PGM
            ranges.Add(new MaterialHsvRange
            {
                Id = "Ru", Name = "Rutênio", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.20, VMin = 0.30, VMax = 0.70,
                Priority = 0.70
            });
            
            // Os (Ósmio) - darkest PGM
            ranges.Add(new MaterialHsvRange
            {
                Id = "Os", Name = "Ósmio", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.18, VMin = 0.25, VMax = 0.70,
                Priority = 0.68
            });

            // Cu (Cobre) - avermelhado/laranja - PR17: narrower hue to avoid Au overlap
            ranges.Add(new MaterialHsvRange
            {
                Id = "Cu", Name = "Cobre", Type = PixelMaterialType.Metal,
                HMin = 8, HMax = 28, SMin = 0.50, SMax = 0.95, VMin = 0.45, VMax = 0.92,
                Priority = 0.84
            });

            // Fe (Ferro) - cinza escuro/marrom
            ranges.Add(new MaterialHsvRange
            {
                Id = "Fe", Name = "Ferro", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 25, SMin = 0.0, SMax = 0.35, VMin = 0.18, VMax = 0.55,
                Priority = 0.70
            });

            // Al (Alumínio) - branco/prateado brilhante
            ranges.Add(new MaterialHsvRange
            {
                Id = "Al", Name = "Alumínio", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.10, VMin = 0.80, VMax = 1.0,
                Priority = 0.74
            });

            // Ni (Níquel) - cinza médio - PR17: stricter saturation
            ranges.Add(new MaterialHsvRange
            {
                Id = "Ni", Name = "Níquel", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.15, VMin = 0.52, VMax = 0.82,
                Priority = 0.70
            });

            // Zn (Zinco) - cinza claro
            ranges.Add(new MaterialHsvRange
            {
                Id = "Zn", Name = "Zinco", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.10, VMin = 0.62, VMax = 0.88,
                Priority = 0.66
            });

            // Pb (Chumbo) - cinza escuro
            ranges.Add(new MaterialHsvRange
            {
                Id = "Pb", Name = "Chumbo", Type = PixelMaterialType.Metal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.18, VMin = 0.22, VMax = 0.52,
                Priority = 0.60
            });

            // ====== CRISTAIS ======

            // SiO2 (Quartzo) - transparente/branco
            ranges.Add(new MaterialHsvRange
            {
                Id = "SiO2", Name = "Quartzo", Type = PixelMaterialType.Crystal,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.18, VMin = 0.72, VMax = 1.0,
                Priority = 0.62
            });

            // CaCO3 (Calcita) - branco/creme
            ranges.Add(new MaterialHsvRange
            {
                Id = "CaCO3", Name = "Calcita", Type = PixelMaterialType.Crystal,
                HMin = 0, HMax = 55, SMin = 0.0, SMax = 0.32, VMin = 0.72, VMax = 1.0,
                Priority = 0.58
            });

            // Feldspato - branco/rosa pálido
            ranges.Add(new MaterialHsvRange
            {
                Id = "Feldspato", Name = "Feldspato", Type = PixelMaterialType.Crystal,
                HMin = 0, HMax = 55, SMin = 0.12, SMax = 0.42, VMin = 0.58, VMax = 0.88,
                Priority = 0.52
            });

            // Mica - dourado/bronze
            ranges.Add(new MaterialHsvRange
            {
                Id = "Mica", Name = "Mica", Type = PixelMaterialType.Crystal,
                HMin = 0, HMax = 75, SMin = 0.18, SMax = 0.52, VMin = 0.52, VMax = 0.82,
                Priority = 0.52
            });

            // CaF2 (Fluorita) - roxo/azul
            ranges.Add(new MaterialHsvRange
            {
                Id = "CaF2", Name = "Fluorita", Type = PixelMaterialType.Crystal,
                HMin = 200, HMax = 300, SMin = 0.42, SMax = 1.0, VMin = 0.48, VMax = 0.88,
                Priority = 0.62
            });

            // ====== GEMAS ======

            // Diamante - transparente muito brilhante
            ranges.Add(new MaterialHsvRange
            {
                Id = "C", Name = "Diamante", Type = PixelMaterialType.Gem,
                HMin = 0, HMax = 360, SMin = 0.0, SMax = 0.12, VMin = 0.92, VMax = 1.0,
                Priority = 0.72
            });

            // Safira - azul profundo
            ranges.Add(new MaterialHsvRange
            {
                Id = "Al2O3_blue", Name = "Safira", Type = PixelMaterialType.Gem,
                HMin = 200, HMax = 250, SMin = 0.58, SMax = 1.0, VMin = 0.48, VMax = 0.82,
                Priority = 0.78
            });

            // Rubi - vermelho profundo
            ranges.Add(new MaterialHsvRange
            {
                Id = "Al2O3_red", Name = "Rubi", Type = PixelMaterialType.Gem,
                HMin = 350, HMax = 10, SMin = 0.58, SMax = 1.0, VMin = 0.42, VMax = 0.78,
                Priority = 0.78
            });

            // Esmeralda - verde profundo
            ranges.Add(new MaterialHsvRange
            {
                Id = "Be3Al2Si6O18", Name = "Esmeralda", Type = PixelMaterialType.Gem,
                HMin = 120, HMax = 160, SMin = 0.58, SMax = 1.0, VMin = 0.42, VMax = 0.82,
                Priority = 0.78
            });

            // Ametista - roxo
            ranges.Add(new MaterialHsvRange
            {
                Id = "SiO2_purple", Name = "Ametista", Type = PixelMaterialType.Gem,
                HMin = 270, HMax = 300, SMin = 0.38, SMax = 0.82, VMin = 0.48, VMax = 0.82,
                Priority = 0.72
            });

            // Ordenar por prioridade (maior primeiro)
            ranges.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            return ranges;
        }

        /// <summary>
        /// Classifica um pixel baseado em HSV e RGB.
        /// Retorna o material mais provável e sua confiança.
        /// PR17: Enhanced gold detection with multi-band approach and robust Au/PGM discrimination.
        /// </summary>
        private PixelClassificationResult ClassifyPixel(byte R, byte G, byte B, double H, double S, double V)
        {
            var result = new PixelClassificationResult();
            string bestMaterial = "MetalOther";
            PixelMaterialType bestType = PixelMaterialType.Metal;
            double bestScore = 0;
            string secondBestMaterial = "Unknown";
            double secondBestScore = 0;

            // PR17: Track Au-specific scores across all Au bands
            double maxAuScore = 0;
            bool hasGoldCharacteristics = false;

            // PR17: Track scores for all candidates to determine confidence
            var candidateScores = new List<(string Id, PixelMaterialType Type, double Score)>();
            
            // PR17: Pre-compute gold characteristics for boosting
            hasGoldCharacteristics = EvaluateGoldCharacteristics(R, G, B, H, S, V);

            foreach (var range in _materialRanges)
            {
                double score = range.CalculateScore(H, S, V);
                
                // Apply priority as a multiplier
                score *= range.Priority;

                // PR17: Enhanced RGB-based checks for metals with stricter criteria
                if (range.Type == PixelMaterialType.Metal)
                {
                    // PR17: Enhanced Gold detection with multi-band support
                    if (range.Id == "Au")
                    {
                        double goldScore = EvaluateGoldScore(R, G, B, H, S, V, score);
                        score = goldScore;
                        
                        // Track max Au score across all Au bands
                        if (score > maxAuScore)
                        {
                            maxAuScore = score;
                        }
                        
                        // PR17: Boost if pixel has strong gold characteristics
                        if (hasGoldCharacteristics && score > 0.3)
                        {
                            score *= GoldConfidenceBoostFactor;
                        }
                    }
                    
                    // Copper: R must be significantly higher than G and B (reddish-orange)
                    if (range.Id == "Cu")
                    {
                        score = EvaluateCopperScore(R, G, B, H, S, V, score);
                    }

                    // PR17: All PGM metals - stricter neutrality check
                    if (range.Id == "Pt" || range.Id == "Pd" || range.Id == "Rh" || 
                        range.Id == "Ir" || range.Id == "Ru" || range.Id == "Os")
                    {
                        score = EvaluatePgmScore(R, G, B, H, S, V, score);
                    }
                    
                    // Other neutral metals
                    if (range.Id == "Ni" || range.Id == "Zn" || range.Id == "Al")
                    {
                        int max = Math.Max(R, Math.Max(G, B));
                        int min = Math.Min(R, Math.Min(G, B));
                        if (max - min > 30) score *= 0.45;
                        
                        // PR17: These should not be warm colored
                        if (H >= 30 && H <= 70 && S > 0.15) score *= 0.25;
                    }
                    
                    // Silver: MUST be very bright and neutral
                    if (range.Id == "Ag")
                    {
                        if (V < 0.85) score *= 0.3;
                        if (S > 0.10) score *= 0.35;
                        
                        int max = Math.Max(R, Math.Max(G, B));
                        int min = Math.Min(R, Math.Min(G, B));
                        if (max - min > 18) score *= 0.35;
                    }
                    
                    // Iron: low brightness, low saturation, can have reddish tones
                    if (range.Id == "Fe")
                    {
                        if (V > 0.60) score *= 0.5;
                        // Iron should not be bright yellow
                        if (H >= 35 && H <= 65 && S > 0.25) score *= 0.3;
                    }
                }

                candidateScores.Add((range.Id, range.Type, score));

                if (score > bestScore)
                {
                    secondBestScore = bestScore;
                    secondBestMaterial = bestMaterial;
                    bestScore = score;
                    bestMaterial = range.Id;
                    bestType = range.Type;
                }
                else if (score > secondBestScore)
                {
                    secondBestScore = score;
                    secondBestMaterial = range.Id;
                }
            }

            // PR17: Calculate score gap for confidence assessment
            double scoreGap = bestScore - secondBestScore;
            
            // PR17: Special handling for gold - apply additional boost if it's clearly gold
            if (bestMaterial == "Au" && hasGoldCharacteristics && scoreGap > 0.05)
            {
                // Boost gold confidence when characteristics match
                bestScore = Math.Min(0.95, bestScore * 1.05);
            }

            // PR17: Determine confidence level based on score AND gap
            ConfidenceLevel confidenceLevel;
            
            if (bestScore >= HighConfidenceThreshold && scoreGap >= MinScoreGapForHighConfidence)
            {
                confidenceLevel = ConfidenceLevel.High;
            }
            else if (bestScore >= MediumConfidenceThreshold && scoreGap >= MinScoreGapForMediumConfidence)
            {
                confidenceLevel = ConfidenceLevel.Medium;
            }
            else if (bestScore >= LowConfidenceThreshold)
            {
                confidenceLevel = ConfidenceLevel.Low;
            }
            else
            {
                confidenceLevel = ConfidenceLevel.Indeterminate;
            }

            // PR17: If indeterminate, prefer to mark as "Unknown" rather than wrongly classify
            if (bestScore < IndeterminateThreshold || 
                (confidenceLevel == ConfidenceLevel.Indeterminate && scoreGap < 0.04))
            {
                result.MaterialId = "Indeterminate";
                result.MaterialType = PixelMaterialType.None;
                result.Confidence = bestScore;
                result.ConfidenceLevel = ConfidenceLevel.Indeterminate;
                result.SecondBestMaterialId = bestMaterial; // Store what we would have guessed
                result.SecondBestScore = bestScore;
                result.ScoreGap = 0;
            }
            else
            {
                result.MaterialId = bestMaterial;
                result.MaterialType = bestType;
                result.Confidence = Math.Min(0.95, bestScore);
                result.ConfidenceLevel = confidenceLevel;
                result.SecondBestMaterialId = secondBestMaterial;
                result.SecondBestScore = secondBestScore;
                result.ScoreGap = scoreGap;
            }

            return result;
        }
        
        /// <summary>
        /// PR18: Evaluate if pixel has strong gold characteristics.
        /// Returns true if the pixel has characteristics consistent with gold.
        /// Relaxed thresholds for better sensitivity.
        /// </summary>
        private bool EvaluateGoldCharacteristics(byte R, byte G, byte B, double H, double S, double V)
        {
            // Gold characteristics - PR18: Relaxed thresholds
            // 1. Warm hue (yellow/gold range) - expanded
            bool warmHue = H >= 22 && H <= 78;
            
            // 2. Some saturation (not gray) - relaxed
            bool hasSaturation = S >= 0.12;
            
            // 3. R and G dominant over B - relaxed
            double avgRG = (R + G) / 2.0;
            bool rgDominant = avgRG > B + 10;
            
            // 4. R and G should be similar (yellow, not orange or green) - relaxed
            double diffRG = Math.Abs(R - G);
            bool balancedRG = diffRG < 60;
            
            // 5. Minimum brightness - relaxed
            bool bright = V >= 0.28 && (R >= 85 || G >= 85);
            
            // Count how many characteristics match
            int matches = 0;
            if (warmHue) matches++;
            if (hasSaturation) matches++;
            if (rgDominant) matches++;
            if (balancedRG) matches++;
            if (bright) matches++;
            
            // PR18: At least 3 of 5 characteristics must match (relaxed from 4)
            return matches >= 3;
        }
        
        /// <summary>
        /// PR18: Enhanced gold score evaluation with multi-band support.
        /// Relaxed thresholds for better gold sensitivity.
        /// </summary>
        private double EvaluateGoldScore(byte R, byte G, byte B, double H, double S, double V, double baseScore)
        {
            double score = baseScore;
            
            // Basic gold requirements
            double avgRG = (R + G) / 2.0;
            
            // PR18: R+G must dominate B (gold is yellow, not blue) - relaxed penalties
            if (avgRG <= B + 10)
            {
                score *= 0.15;
            }
            else if (avgRG <= B + 25)
            {
                score *= 0.45;
            }
            else
            {
                // Good R+G dominance - slight boost
                double dominance = (avgRG - B) / 100.0;
                score *= Math.Min(1.18, 1.0 + dominance * 0.18);
            }
            
            // PR18: R and G must be similar (gold is yellow, not orange) - relaxed
            double diffRG = Math.Abs(R - G);
            if (diffRG > 65)
            {
                score *= 0.35;
            }
            else if (diffRG > 45)
            {
                score *= 0.65;
            }
            else if (diffRG < 25)
            {
                // Very balanced - slight boost
                score *= 1.08;
            }
            
            // PR18: Minimum brightness for gold detection - relaxed
            if (R < 85 && G < 85)
            {
                score *= 0.35;
            }
            else if (R < 100 && G < 100)
            {
                score *= 0.60;
            }
            
            // PR18: Gold should have some color saturation (not gray) - relaxed
            if (S < 0.12)
            {
                score *= 0.25;
            }
            else if (S < 0.18)
            {
                score *= 0.55;
            }
            else if (S >= 0.25 && S <= 0.85)
            {
                // Good saturation range for gold - boost
                score *= 1.10;
            }
            
            // PR18: Gold has warm hue in yellow range - expanded
            if (H < 20 || H > 82)
            {
                score *= 0.28;
            }
            else if (H >= 32 && H <= 68)
            {
                // Optimal gold hue range - boost
                score *= 1.12;
            }
            
            return score;
        }
        
        /// <summary>
        /// PR17: Enhanced copper score evaluation.
        /// </summary>
        private double EvaluateCopperScore(byte R, byte G, byte B, double H, double S, double V, double baseScore)
        {
            double score = baseScore;
            
            // Copper must be distinctly reddish
            if (R <= G || R <= B)
            {
                score *= 0.15;
            }
            else if (R < G + 25)
            {
                score *= 0.35;
            }
            
            // Copper requires good saturation
            if (S < 0.40)
            {
                score *= 0.30;
            }
            
            // Copper hue is orange-red (lower than gold)
            if (H > 32)
            {
                score *= 0.50;
            }
            
            return score;
        }
        
        /// <summary>
        /// PR17: Enhanced PGM (Platinum Group Metals) score evaluation.
        /// Stricter neutrality check to prevent confusion with gold.
        /// </summary>
        private double EvaluatePgmScore(byte R, byte G, byte B, double H, double S, double V, double baseScore)
        {
            double score = baseScore;
            
            int max = Math.Max(R, Math.Max(G, B));
            int min = Math.Min(R, Math.Min(G, B));
            int rgbRange = max - min;
            
            // PR17: Stricter neutrality check for PGM
            if (rgbRange > 22)
            {
                score *= 0.25;
            }
            else if (rgbRange > 12)
            {
                score *= 0.55;
            }
            else
            {
                // Very neutral - boost
                score *= 1.08;
            }
            
            // PR17: PGM should NOT have warm yellow tones (distinguish from Au)
            if (H >= 28 && H <= 72 && S > 0.12)
            {
                // This looks like gold territory
                score *= 0.15;
            }
            
            // PR17: PGM must be truly neutral (low saturation)
            if (S > 0.12)
            {
                score *= 0.30;
            }
            else if (S < 0.06)
            {
                // Very low saturation - boost for PGM
                score *= 1.10;
            }
            
            return score;
        }

        public (SampleFullAnalysisResult analysis, SampleMaskClass[,] mask, Bitmap maskPreview)
            RunFullAnalysis(Bitmap bmp, string? imagePath)
        {
            var scene = AnalyzeScene(bmp, imagePath);
            // AnalyzeScene always populates the mask fully, so we can safely convert
            // The nullable type in FullSceneAnalysis is for API flexibility, but internally it's always populated
            int w = scene.Width;
            int h = scene.Height;
            var nullableMask = scene.Mask;
            var mask = new SampleMaskClass[w, h];
            
            // Only iterate if we have a valid mask
            if (nullableMask.GetLength(0) == w && nullableMask.GetLength(1) == h)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        mask[x, y] = nullableMask[x, y] ?? new SampleMaskClass { IsSample = false };
                    }
                }
            }
            
            return (scene.Summary, mask, scene.MaskPreview);
        }

        /// <summary>
        /// BLOCO 2 – Executa análise com reanálise automática para amostras críticas.
        /// - Se QualityStatus == "Invalid", roda mais 2 análises na mesma imagem.
        /// - Compara QualityIndex e %Au nas 3 rodadas.
        /// - Se convergir: QualityStatus = "OfficialRechecked".
        /// - Se divergir:  QualityStatus = "ReviewRequired".
        /// PR16: Updated to accept ROI parameter for consistent sample/background separation.
        /// </summary>
        public SampleFullAnalysisResult RunWithAutoReanalysis(Bitmap bmp, string? imagePath, RoiDefinition? roi = null)
        {
            // 1) Primeira análise normal (PR16: pass ROI)
            var scene1 = AnalyzeScene(bmp, imagePath, roi);
            var r1 = scene1.Summary;

            // Se não for "Invalid", não é amostra crítica: retorna direto
            if (!string.Equals(r1.QualityStatus, "Invalid", StringComparison.OrdinalIgnoreCase))
                return r1;

            // 2) Amostra crítica -> executar mais 2 análises completas na mesma imagem (PR16: pass ROI)
            var scene2 = AnalyzeScene(bmp, imagePath, roi);
            var scene3 = AnalyzeScene(bmp, imagePath, roi);
            var r2 = scene2.Summary;
            var r3 = scene3.Summary;

            double q1 = r1.QualityIndex;
            double q2 = r2.QualityIndex;
            double q3 = r3.QualityIndex;

            double qMin = Math.Min(q1, Math.Min(q2, q3));
            double qMax = Math.Max(q1, Math.Max(q2, q3));
            double qRange = qMax - qMin;

            double GetPctAu(SampleFullAnalysisResult r)
            {
                var au = r.Metals.FirstOrDefault(m => string.Equals(m.Id, "Au", StringComparison.OrdinalIgnoreCase));
                return au?.PctSample ?? 0.0;
            }

            double a1 = GetPctAu(r1);
            double a2 = GetPctAu(r2);
            double a3 = GetPctAu(r3);

            double aMin = Math.Min(a1, Math.Min(a2, a3));
            double aMax = Math.Max(a1, Math.Max(a2, a3));
            double aRange = aMax - aMin;

            bool convergiu = (qRange <= 5.0) && (aRange <= 0.0005);

            r1.QualityStatus = convergiu ? "OfficialRechecked" : "ReviewRequired";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("--- Reanálise automática (BLOCO 2) ---");
            sb.AppendLine($"Rodadas: 3");
            sb.AppendLine($"QualityIndex: {q1:F1}, {q2:F1}, {q3:F1} (range={qRange:F1})");
            sb.AppendLine($"PctAu: {a1:P4}, {a2:P4}, {a3:P4} (range={aRange:P4})");
            sb.AppendLine($"Decisão: {r1.QualityStatus}");

            r1.ShortReport = (r1.ShortReport ?? string.Empty) + sb.ToString();

            return r1;
        }

        /// <summary>
        /// PR16: Analyze scene with optional ROI support for sample/background separation.
        /// </summary>
        public FullSceneAnalysis AnalyzeScene(Bitmap bmp, string? imagePath, RoiDefinition? roi = null)
        {
            using var src24 = Ensure24bpp(bmp);

            var maskService = new SampleMaskService();
            var (maskNullable, maskPreview, maskValidation) = maskService.BuildMaskWithValidation(src24);

            int w = src24.Width, h = src24.Height;
            var mask = new SampleMaskClass[w, h];
            long sampleCount = 0;
            
            // PR16: Apply ROI to mask - pixels outside ROI are marked as background
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isSample = maskNullable[x, y]?.IsSample == true;
                    
                    // PR16: If ROI is defined, further restrict sample area to within ROI
                    if (isSample && roi != null && roi.Shape != RoiShape.None)
                    {
                        isSample = roi.Contains(x, y);
                    }
                    
                    mask[x, y] = new SampleMaskClass { IsSample = isSample };
                    if (isSample) sampleCount++;
                }
            }

            var rect = new Rectangle(0, 0, w, h);
            var data = src24.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            // Material counts dictionary - tracks all materials
            var materialCounts = new Dictionary<string, long>();

            long diagTotal = 0;
            long diagClip = 0;
            double gradSum = 0;

            var labels = new PixelLabel[w, h];

            try
            {
                int stride = data.Stride;
                int bytes = stride * h;
                byte[] buf = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);

                object locker = new object();

                System.Threading.Tasks.Parallel.For(
                    0, h,
                    () => new LocalAccExtended(),
                    (y, loop, acc) =>
                    {
                        int row = y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            acc.Total++;

                            var lbl = new PixelLabel();
                            labels[x, y] = lbl;

                            lbl.IsSample = false;
                            lbl.MaterialType = PixelMaterialType.Background;
                            lbl.MaterialId = null;
                            lbl.MaterialConfidence = 0;
                            lbl.RawScore = 0;

                            if (!mask[x, y].IsSample)
                                continue;

                            lbl.IsSample = true;

                            int off = row + x * 3;
                            byte B = buf[off + 0];
                            byte G = buf[off + 1];
                            byte R = buf[off + 2];

                            // Store RGB values
                            lbl.R = R;
                            lbl.G = G;
                            lbl.B = B;

                            int gray = (int)(0.299 * R + 0.587 * G + 0.114 * B);
                            if (gray < 5 || gray > 250) acc.Clip++;

                            if (x > 0 && x < w - 1 && y > 0 && y < h - 1)
                            {
                                int offL = row + (x - 1) * 3;
                                int offR = row + (x + 1) * 3;
                                int offU = (y - 1) * stride + x * 3;
                                int offD = (y + 1) * stride + x * 3;

                                int gL = (int)(0.299 * buf[offL + 2] + 0.587 * buf[offL + 1] + 0.114 * buf[offL + 0]);
                                int gR2 = (int)(0.299 * buf[offR + 2] + 0.587 * buf[offR + 1] + 0.114 * buf[offR + 0]);
                                int gU = (int)(0.299 * buf[offU + 2] + 0.587 * buf[offU + 1] + 0.114 * buf[offU + 0]);
                                int gD = (int)(0.299 * buf[offD + 2] + 0.587 * buf[offD + 1] + 0.114 * buf[offD + 0]);

                                int gx = gR2 - gL;
                                int gy = gD - gU;
                                acc.GradSum += (gx * gx + gy * gy);
                            }

                            RgbToHsvFast(R, G, B, out double H, out double S, out double V);
                            lbl.H = H;
                            lbl.S = S;
                            lbl.V = V;

                            // Use the new classification system
                            var classification = ClassifyPixel(R, G, B, H, S, V);
                            
                            lbl.MaterialType = classification.MaterialType;
                            lbl.MaterialId = classification.MaterialId;
                            lbl.ScoreHvs = classification.Confidence;
                            lbl.ScoreIa = classification.Confidence; // Stub: same as HVS
                            lbl.RawScore = classification.Confidence;
                            lbl.MaterialConfidence = classification.Confidence;

                            // Track material count
                            if (!acc.MaterialCounts.ContainsKey(classification.MaterialId))
                                acc.MaterialCounts[classification.MaterialId] = 0;
                            acc.MaterialCounts[classification.MaterialId]++;
                        }

                        return acc;
                    },
                    acc =>
                    {
                        lock (locker)
                        {
                            diagTotal += acc.Total;
                            diagClip += acc.Clip;
                            gradSum += acc.GradSum;
                            
                            // Merge material counts
                            foreach (var kvp in acc.MaterialCounts)
                            {
                                if (!materialCounts.ContainsKey(kvp.Key))
                                    materialCounts[kvp.Key] = 0;
                                materialCounts[kvp.Key] += kvp.Value;
                            }
                        }
                    });
            }
            finally
            {
                src24.UnlockBits(data);
            }

            // -----------------------------
            // BLOCO 1 – CHECKLIST DE QUALIDADE
            // -----------------------------

            double focusRaw = 0;
            if (sampleCount > 0)
            {
                focusRaw = gradSum / sampleCount / (255.0 * 255.0);
                focusRaw = Math.Min(1.0, Math.Max(0.0, focusRaw));
            }

            double focusScore = focusRaw * 100.0;
            double clippingFrac = diagTotal > 0 ? (double)diagClip / diagTotal : 0.0;
            double exposureScore = 100.0 * (1.0 - Math.Min(1.0, clippingFrac / 0.2));
            double foregroundFraction = (double)sampleCount / Math.Max(1, w * h);

            double maskScore;
            if (foregroundFraction < 0.3)
            {
                maskScore = 50.0 * foregroundFraction / 0.3;
            }
            else if (foregroundFraction > 0.95)
            {
                maskScore = 60.0;
            }
            else
            {
                maskScore = 80.0 + 20.0 * (1.0 - Math.Abs(foregroundFraction - 0.6) / 0.3);
                maskScore = Math.Min(100.0, Math.Max(0.0, maskScore));
            }

            double qualityIndex =
                0.4 * focusScore +
                0.3 * exposureScore +
                0.3 * maskScore;

            qualityIndex = Math.Max(0.0, Math.Min(100.0, qualityIndex));

            string qualityStatus;
            if (qualityIndex >= 85.0)
                qualityStatus = "Official";
            else if (qualityIndex >= 70.0)
                qualityStatus = "Preliminary";
            else
                qualityStatus = "Invalid";

            var diag = new ImageDiagnosticsResult
            {
                FocusScore = focusRaw,
                SaturationClippingFraction = diagTotal > 0 ? (double)diagClip / diagTotal : 0,
                ForegroundFraction = (double)sampleCount / Math.Max(1, w * h),
                FocusScorePercent = focusScore,
                ExposureScore = exposureScore,
                MaskScore = maskScore,
                QualityIndex = qualityIndex,
                QualityStatus = qualityStatus,
                ForegroundFractionStatus = GetForegroundFractionStatus(foregroundFraction),
                MaskWarnings = maskValidation.Warnings
            };

            var result = new SampleFullAnalysisResult
            {
                Id = Guid.NewGuid(),
                ImagePath = imagePath,
                CaptureDateTimeUtc = DateTime.UtcNow,
                Diagnostics = diag,
                QualityIndex = qualityIndex,
                QualityStatus = qualityStatus
            };

            long denom = Math.Max(1, sampleCount);

            // Build results for ALL materials using materialCounts
            BuildMaterialResults(result, materialCounts, denom);

            // -----------------------------
            // BLOCO 3A + 3B – BANCO DE PARTÍCULAS com segmentação individual
            // -----------------------------

            var particles = SegmentParticles(labels, mask, w, h, result.Id);
            result.Particles.AddRange(particles);

            result.ShortReport = BuildShortReport(result);

            return new FullSceneAnalysis
            {
                Summary = result,
                Labels = labels,
                Mask = mask,
                MaskPreview = (Bitmap)maskPreview.Clone(),
                Width = w,
                Height = h
            };
        }

        /// <summary>
        /// Determina o status da fração de foreground.
        /// </summary>
        private string GetForegroundFractionStatus(double fraction)
        {
            if (fraction < 0.05) return "Muito baixa";
            if (fraction < 0.30) return "Baixa";
            if (fraction > 0.95) return "Muito alta";
            if (fraction > 0.80) return "Alta";
            return "OK";
        }

        /// <summary>
        /// Constrói resultados de materiais a partir das contagens.
        /// </summary>
        private void BuildMaterialResults(SampleFullAnalysisResult result, Dictionary<string, long> materialCounts, long totalSamplePixels)
        {
            // Mapeamento de IDs para nomes e grupos
            var metalInfo = new Dictionary<string, (string name, string group)>
            {
                ["Au"] = ("Ouro", "Nobre"),
                ["Ag"] = ("Prata", "Nobre"),
                ["Pt"] = ("Platina", "PGM"),
                ["Pd"] = ("Paládio", "PGM"),
                ["Rh"] = ("Ródio", "PGM"),
                ["Ir"] = ("Irídio", "PGM"),
                ["Ru"] = ("Rutênio", "PGM"),
                ["Os"] = ("Ósmio", "PGM"),
                ["Cu"] = ("Cobre", "comum"),
                ["Fe"] = ("Ferro", "comum"),
                ["Al"] = ("Alumínio", "comum"),
                ["Ni"] = ("Níquel", "comum"),
                ["Zn"] = ("Zinco", "comum"),
                ["Pb"] = ("Chumbo", "comum"),
                ["MetalOther"] = ("Outros metais", "outros")
            };

            var crystalInfo = new Dictionary<string, string>
            {
                ["SiO2"] = "Quartzo",
                ["CaCO3"] = "Calcita",
                ["Feldspato"] = "Feldspato",
                ["Mica"] = "Mica",
                ["CaF2"] = "Fluorita"
            };

            var gemInfo = new Dictionary<string, string>
            {
                ["C"] = "Diamante",
                ["Al2O3_blue"] = "Safira",
                ["Al2O3_red"] = "Rubi",
                ["Be3Al2Si6O18"] = "Esmeralda",
                ["SiO2_purple"] = "Ametista"
            };

            // Processar cada material encontrado
            foreach (var kvp in materialCounts)
            {
                string matId = kvp.Key;
                long count = kvp.Value;
                double pct = (double)count / totalSamplePixels;

                // Ignorar se a fração for muito baixa, exceto para metais primários
                if (pct < MinMaterialPercentageThreshold && !PrimaryMetalsAlwaysIncluded.Contains(matId))
                    continue;

                if (metalInfo.TryGetValue(matId, out var metal))
                {
                    result.Metals.Add(new MetalResult
                    {
                        Id = matId,
                        Name = metal.name,
                        Group = metal.group,
                        PctSample = pct,
                        PpmEstimated = pct > 0 ? pct * 1_000_000.0 : (double?)null,
                        Score = Math.Min(1.0, 0.6 + pct * 10.0)
                    });
                }
                else if (crystalInfo.TryGetValue(matId, out var crystalName))
                {
                    result.Crystals.Add(new CrystalResult
                    {
                        Id = matId,
                        Name = crystalName,
                        PctSample = pct,
                        Score = Math.Min(1.0, 0.5 + pct * 8.0)
                    });
                }
                else if (gemInfo.TryGetValue(matId, out var gemName))
                {
                    result.Gems.Add(new GemResult
                    {
                        Id = matId,
                        Name = gemName,
                        PctSample = pct,
                        Score = Math.Min(1.0, 0.7 + pct * 5.0)
                    });
                }
            }

            // Ordenar por percentual (maior primeiro)
            result.Metals = result.Metals.OrderByDescending(m => m.PctSample).ToList();
            result.Crystals = result.Crystals.OrderByDescending(c => c.PctSample).ToList();
            result.Gems = result.Gems.OrderByDescending(g => g.PctSample).ToList();
        }

        /// <summary>
        /// Segmenta pixels contíguos da mesma classe em partículas individuais.
        /// </summary>
        private List<ParticleRecord> SegmentParticles(
            PixelLabel[,] labels,
            SampleMaskClass[,] mask,
            int w, int h,
            Guid analysisId)
        {
            var particles = new List<ParticleRecord>();
            var visited = new bool[w, h];
            var aiService = new PixelClassificationAiServiceStub();

            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            var queue = new Queue<(int x, int y)>();
            int minParticleSize = Math.Max(MinParticleSizeAbsolute, (w * h) / MinParticleSizeDivisor);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var lbl = labels[x, y];
                    if (lbl == null || !lbl.IsSample || visited[x, y])
                        continue;

                    // Iniciar BFS para agrupar pixels contíguos
                    var features = new ParticleFeatures();
                    queue.Clear();

                    visited[x, y] = true;
                    queue.Enqueue((x, y));

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        var pixLbl = labels[cx, cy];

                        // Acumular features
                        features.PixelCount++;
                        features.SumX += cx;
                        features.SumY += cy;
                        features.MinX = Math.Min(features.MinX, cx);
                        features.MaxX = Math.Max(features.MaxX, cx);
                        features.MinY = Math.Min(features.MinY, cy);
                        features.MaxY = Math.Max(features.MaxY, cy);
                        features.SumH += pixLbl.H;
                        features.SumS += pixLbl.S;
                        features.SumV += pixLbl.V;
                        features.SumConfidence += pixLbl.MaterialConfidence;
                        features.SumConfidenceSq += pixLbl.MaterialConfidence * pixLbl.MaterialConfidence;
                        features.Pixels.Add((cx, cy));

                        // Contar votos por material
                        string matId = pixLbl.MaterialId ?? "Unknown";
                        if (!features.MaterialVotes.ContainsKey(matId))
                        {
                            features.MaterialVotes[matId] = 0;
                            features.MaterialWeightedVotes[matId] = 0;
                        }
                        features.MaterialVotes[matId]++;
                        features.MaterialWeightedVotes[matId] += pixLbl.MaterialConfidence;

                        // Verificar se é pixel de borda
                        bool isBorder = false;
                        for (int k = 0; k < 8 && !isBorder; k++)
                        {
                            int nx = cx + dx[k];
                            int ny = cy + dy[k];
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            {
                                isBorder = true;
                            }
                            else if (labels[nx, ny] == null || !labels[nx, ny].IsSample)
                            {
                                isBorder = true;
                            }
                        }
                        if (isBorder) features.BorderPixelCount++;

                        // Expandir para vizinhos da mesma classificação
                        for (int k = 0; k < 8; k++)
                        {
                            int nx = cx + dx[k];
                            int ny = cy + dy[k];
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (visited[nx, ny]) continue;

                            var neighLbl = labels[nx, ny];
                            if (neighLbl == null || !neighLbl.IsSample) continue;

                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }

                    // Ignorar partículas muito pequenas
                    if (features.PixelCount < minParticleSize)
                        continue;

                    // Criar ParticleRecord
                    var particle = CreateParticleRecord(features, analysisId, aiService);
                    particles.Add(particle);
                }
            }

            return particles;
        }

        /// <summary>
        /// Cria um ParticleRecord a partir das features acumuladas.
        /// </summary>
        private ParticleRecord CreateParticleRecord(ParticleFeatures features, Guid analysisId, IPixelClassificationAiService aiService)
        {
            var (cx, cy) = features.GetCentroid();
            int n = features.PixelCount;

            // Determinar material dominante por voto ponderado
            string dominantMaterial = "Unknown";
            double maxVote = 0;
            foreach (var (matId, vote) in features.MaterialWeightedVotes)
            {
                if (vote > maxVote)
                {
                    maxVote = vote;
                    dominantMaterial = matId;
                }
            }

            // Calcular confiança média
            double avgConf = n > 0 ? features.SumConfidence / n : 0;
            double variance = n > 1
                ? (features.SumConfidenceSq - features.SumConfidence * features.SumConfidence / n) / (n - 1)
                : 0;
            double stdDev = Math.Sqrt(Math.Max(0, variance));

            // Calcular HSV médio
            double avgH = n > 0 ? features.SumH / n : 0;
            double avgS = n > 0 ? features.SumS / n : 0;
            double avgV = n > 0 ? features.SumV / n : 0;

            // Calcular circularidade (aproximada)
            int perimeter = features.BorderPixelCount;
            double circularity = perimeter > 0
                ? (4 * Math.PI * n) / (perimeter * perimeter)
                : 0;
            circularity = Math.Min(1.0, Math.Max(0, circularity));

            // Calcular aspect ratio do bounding box
            int bbW = features.MaxX - features.MinX + 1;
            int bbH = features.MaxY - features.MinY + 1;
            double aspectRatio = bbH > 0 ? (double)bbW / bbH : 1.0;
            if (aspectRatio < 1) aspectRatio = 1.0 / aspectRatio; // Sempre >= 1

            // Scores HVS e IA
            double scoreHvs = avgConf;
            double scoreIa = avgConf; // Stub: usa HVS

            // Fusão de scores
            var (fusedMat, fusedConf, fusedRaw) = ScoreFusionService.FuseHvsOnly(dominantMaterial, scoreHvs);

            // Calcular composição
            var composition = new Dictionary<string, double>();
            foreach (var (matId, vote) in features.MaterialVotes)
            {
                composition[matId] = (double)vote / n;
            }

            return new ParticleRecord
            {
                AnalysisId = analysisId,
                MaterialId = fusedMat,
                Confidence = fusedConf,
                ApproxAreaPixels = n,
                CenterX = cx,
                CenterY = cy,

                // Forma
                Circularity = circularity,
                AspectRatio = aspectRatio,
                MajorAxisLength = Math.Max(bbW, bbH),
                MinorAxisLength = Math.Min(bbW, bbH),
                Perimeter = perimeter,

                // Bounding box
                BoundingBoxX = features.MinX,
                BoundingBoxY = features.MinY,
                BoundingBoxWidth = bbW,
                BoundingBoxHeight = bbH,

                // HSV
                AvgH = avgH,
                AvgS = avgS,
                AvgV = avgV,

                // Scores
                ScoreHvs = scoreHvs,
                ScoreIa = scoreIa,
                ScoreCombined = fusedRaw,

                // Confiança
                AveragePixelConfidence = avgConf,
                ConfidenceStdDev = stdDev,

                // Composição
                Composition = composition
            };
        }

        private class LocalAccExtended
        {
            public long Total;
            public long Clip;
            public double GradSum;
            public Dictionary<string, long> MaterialCounts = new Dictionary<string, long>();
        }

        private static Bitmap Ensure24bpp(Bitmap src)
        {
            if (src.PixelFormat == PixelFormat.Format24bppRgb)
                return (Bitmap)src.Clone();

            var clone = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(clone);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
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

            if (delta == 0) { h = 0; return; }
            double hue;
            if (max == rd) hue = 60 * (((gd - bd) / delta) % 6);
            else if (max == gd) hue = 60 * (((bd - rd) / delta) + 2);
            else hue = 60 * (((rd - gd) / delta) + 4);
            if (hue < 0) hue += 360;
            h = hue;
        }

        private string BuildShortReport(SampleFullAnalysisResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Resumo rápido da análise");
            sb.AppendLine("------------------------");
            sb.AppendLine($"Data/Hora (UTC): {r.CaptureDateTimeUtc:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Foco (0..1): {r.Diagnostics.FocusScore:F2}");
            sb.AppendLine($"Clipping saturação: {r.Diagnostics.SaturationClippingFraction:P1}");
            sb.AppendLine($"Fração amostra: {r.Diagnostics.ForegroundFraction:P1} ({r.Diagnostics.ForegroundFractionStatus})");
            
            // Mostrar avisos da máscara se houver
            if (r.Diagnostics.MaskWarnings != null && r.Diagnostics.MaskWarnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("⚠️ Avisos da máscara:");
                foreach (var warning in r.Diagnostics.MaskWarnings)
                {
                    sb.AppendLine($"  - {warning}");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("Metais:");
            var topMetals = r.Metals.Where(m => m.PctSample > 0.0001).OrderByDescending(m => m.PctSample).Take(10);
            foreach (var m in topMetals)
            {
                var ppm = m.PpmEstimated.HasValue ? $"{m.PpmEstimated.Value:F1} ppm" : "-";
                sb.AppendLine($" - {m.Name} ({m.Id}): {m.PctSample:P3} · {ppm} · score={m.Score:F2}");
            }
            
            if (r.Crystals.Any(c => c.PctSample > 0.0001))
            {
                sb.AppendLine();
                sb.AppendLine("Cristais:");
                var topCrystals = r.Crystals.Where(c => c.PctSample > 0.0001).OrderByDescending(c => c.PctSample).Take(5);
                foreach (var c in topCrystals)
                {
                    sb.AppendLine($" - {c.Name} ({c.Id}): {c.PctSample:P3} · score={c.Score:F2}");
                }
            }
            
            if (r.Gems.Any(g => g.PctSample > 0.0001))
            {
                sb.AppendLine();
                sb.AppendLine("Gemas:");
                var topGems = r.Gems.Where(g => g.PctSample > 0.0001).OrderByDescending(g => g.PctSample).Take(5);
                foreach (var g in topGems)
                {
                    sb.AppendLine($" - {g.Name} ({g.Id}): {g.PctSample:P3} · score={g.Score:F2}");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine($"QualityIndex: {r.QualityIndex:F1} · Status: {r.QualityStatus}");
            sb.AppendLine($"Partículas (agregadas): {r.Particles.Count}");
            return sb.ToString();
        }

        private bool DetectGoldPixel(byte R, byte G, byte B, double H, double S, double V)
        {
            if (H < 30 || H > 80) return false;
            if (S < 0.18 || V < 0.35) return false;

            double avgRG = (R + G) / 2.0;
            if (avgRG <= B + 10) return false;
            double diffRG = Math.Abs(R - G);
            if (diffRG > 70) return false;
            if (R < 120 && G < 120) return false;

            return true;
        }

        private bool DetectPgmPixel(byte R, byte G, byte B, double H, double S, double V)
        {
            if (S > 0.20) return false;
            if (V < 0.20 || V > 0.92) return false;
            int max = Math.Max(R, Math.Max(G, B));
            int min = Math.Min(R, Math.Min(G, B));
            if (max - min > 35) return false;
            if (max > 245 && min > 220) return false;
            return true;
        }
    }
}