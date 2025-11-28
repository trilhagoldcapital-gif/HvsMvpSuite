using System;
using System.Collections.Generic;

namespace HvsMvp.App
{
    /// <summary>
    /// Severidade de um alerta de consistência.
    /// </summary>
    public enum ConsistencyAlertSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    /// <summary>
    /// Representa um alerta de consistência identificado na análise.
    /// </summary>
    public class ConsistencyAlert
    {
        public ConsistencyAlertSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Recommendation { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Resultado da verificação de consistência.
    /// </summary>
    public class ConsistencyCheckResult
    {
        /// <summary>
        /// Lista de alertas identificados.
        /// </summary>
        public List<ConsistencyAlert> Alerts { get; set; } = new List<ConsistencyAlert>();

        /// <summary>
        /// True se não há alertas críticos ou de erro.
        /// </summary>
        public bool IsConsistent => !HasCriticalAlerts && !HasErrorAlerts;

        /// <summary>
        /// True se há alertas de severidade Critical.
        /// </summary>
        public bool HasCriticalAlerts { get; set; }

        /// <summary>
        /// True se há alertas de severidade Error.
        /// </summary>
        public bool HasErrorAlerts { get; set; }

        /// <summary>
        /// True se há alertas de severidade Warning.
        /// </summary>
        public bool HasWarningAlerts { get; set; }

        /// <summary>
        /// Resumo textual dos alertas.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Qualidade geral sugerida baseada nos alertas.
        /// </summary>
        public string SuggestedQualityStatus { get; set; } = "Official";
    }

    /// <summary>
    /// Serviço de verificação de consistência da análise.
    /// Gera alertas claros sobre problemas na imagem, máscara e resultados.
    /// </summary>
    public class ConsistencyCheckService
    {
        // Thresholds configuráveis
        private const double FocusScoreMinGood = 0.5;
        private const double FocusScoreMinAcceptable = 0.3;
        private const double ClippingMaxGood = 0.05;
        private const double ClippingMaxAcceptable = 0.15;
        private const double ForegroundMinGood = 0.1;
        private const double ForegroundMaxGood = 0.9;
        private const double ForegroundMinAcceptable = 0.03;
        private const double ForegroundMaxAcceptable = 0.97;
        private const double AuPctMaxNormal = 0.1;    // > 10% Au é suspeito
        private const double PtPctMaxNormal = 0.05;   // > 5% Pt é suspeito

        /// <summary>
        /// Executa verificação de consistência completa.
        /// </summary>
        public ConsistencyCheckResult Check(
            ImageDiagnosticsResult diagnostics,
            MaskValidationResult? maskValidation,
            SampleFullAnalysisResult analysisResult)
        {
            var result = new ConsistencyCheckResult();

            // Verificar foco
            CheckFocus(diagnostics, result);

            // Verificar clipping/exposição
            CheckExposure(diagnostics, result);

            // Verificar máscara
            CheckMask(diagnostics, maskValidation, result);

            // Verificar valores extremos de metais
            CheckMetalValues(analysisResult, result);

            // Verificar partículas
            CheckParticles(analysisResult, result);

            // Calcular status geral
            result.HasCriticalAlerts = result.Alerts.Exists(a => a.Severity == ConsistencyAlertSeverity.Critical);
            result.HasErrorAlerts = result.Alerts.Exists(a => a.Severity == ConsistencyAlertSeverity.Error);
            result.HasWarningAlerts = result.Alerts.Exists(a => a.Severity == ConsistencyAlertSeverity.Warning);

            // Determinar status sugerido
            if (result.HasCriticalAlerts)
            {
                result.SuggestedQualityStatus = "Invalid";
            }
            else if (result.HasErrorAlerts)
            {
                result.SuggestedQualityStatus = "Preliminary";
            }
            else if (result.HasWarningAlerts)
            {
                result.SuggestedQualityStatus = "Official"; // Warnings não impedem Official
            }
            else
            {
                result.SuggestedQualityStatus = "Official";
            }

            // Gerar resumo
            result.Summary = GenerateSummary(result);

            return result;
        }

        private void CheckFocus(ImageDiagnosticsResult diagnostics, ConsistencyCheckResult result)
        {
            double focusScore = diagnostics.FocusScore;

            if (focusScore < FocusScoreMinAcceptable)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Error,
                    Code = "FOCUS_CRITICAL",
                    Message = $"Imagem muito desfocada (foco={focusScore:F2}). Resultados podem ser imprecisos.",
                    Recommendation = "Ajuste o foco do microscópio e capture nova imagem."
                });
            }
            else if (focusScore < FocusScoreMinGood)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Warning,
                    Code = "FOCUS_LOW",
                    Message = $"Foco abaixo do ideal (foco={focusScore:F2}). Considere refocar.",
                    Recommendation = "Para melhor precisão, ajuste o foco."
                });
            }
        }

        private void CheckExposure(ImageDiagnosticsResult diagnostics, ConsistencyCheckResult result)
        {
            double clipping = diagnostics.SaturationClippingFraction;

            if (clipping > ClippingMaxAcceptable)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Error,
                    Code = "CLIPPING_HIGH",
                    Message = $"Clipping de saturação muito alto ({clipping:P1}). Perda de informação de cor.",
                    Recommendation = "Reduza a exposição ou ajuste a iluminação do microscópio."
                });
            }
            else if (clipping > ClippingMaxGood)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Warning,
                    Code = "CLIPPING_MODERATE",
                    Message = $"Clipping de saturação moderado ({clipping:P1}). Pode afetar classificação de cores.",
                    Recommendation = "Considere ajustar exposição para melhor resultado."
                });
            }
        }

        private void CheckMask(ImageDiagnosticsResult diagnostics, MaskValidationResult? maskValidation, ConsistencyCheckResult result)
        {
            double foreground = diagnostics.ForegroundFraction;

            // Verificar fração de amostra
            if (foreground < ForegroundMinAcceptable)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Critical,
                    Code = "MASK_NO_SAMPLE",
                    Message = $"Quase nenhuma amostra detectada ({foreground:P1}). A imagem pode estar sem amostra.",
                    Recommendation = "Verifique se há amostra no campo de visão."
                });
            }
            else if (foreground < ForegroundMinGood)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Warning,
                    Code = "MASK_LOW_SAMPLE",
                    Message = $"Pouca amostra detectada ({foreground:P1}). Área analisada pode ser pequena.",
                    Recommendation = "Posicione mais amostra no campo ou ajuste magnificação."
                });
            }
            else if (foreground > ForegroundMaxAcceptable)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Error,
                    Code = "MASK_TOO_MUCH",
                    Message = $"Quase toda imagem é amostra ({foreground:P1}). Problema de segmentação ou fundo.",
                    Recommendation = "Verifique se o fundo está visível e bem iluminado."
                });
            }
            else if (foreground > ForegroundMaxGood)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Warning,
                    Code = "MASK_HIGH_SAMPLE",
                    Message = $"Muita amostra detectada ({foreground:P1}). Fundo pode estar inadequado.",
                    Recommendation = "Garanta que o fundo esteja claro e uniforme."
                });
            }

            // Verificar alertas da validação da máscara
            if (maskValidation != null && maskValidation.HasAnomalies)
            {
                foreach (var warning in maskValidation.Warnings)
                {
                    result.Alerts.Add(new ConsistencyAlert
                    {
                        Severity = ConsistencyAlertSeverity.Warning,
                        Code = "MASK_ANOMALY",
                        Message = warning
                    });
                }
            }
        }

        private void CheckMetalValues(SampleFullAnalysisResult analysisResult, ConsistencyCheckResult result)
        {
            foreach (var metal in analysisResult.Metals)
            {
                if (string.Equals(metal.Id, "Au", StringComparison.OrdinalIgnoreCase))
                {
                    if (metal.PctSample > AuPctMaxNormal)
                    {
                        result.Alerts.Add(new ConsistencyAlert
                        {
                            Severity = ConsistencyAlertSeverity.Warning,
                            Code = "AU_HIGH",
                            Message = $"Concentração de Au muito alta ({metal.PctSample:P2}). Verificar amostra.",
                            Recommendation = "Concentrações altas de Au são raras. Verifique se não há interferência de cor."
                        });
                    }
                }
                else if (string.Equals(metal.Id, "Pt", StringComparison.OrdinalIgnoreCase))
                {
                    if (metal.PctSample > PtPctMaxNormal)
                    {
                        result.Alerts.Add(new ConsistencyAlert
                        {
                            Severity = ConsistencyAlertSeverity.Warning,
                            Code = "PT_HIGH",
                            Message = $"Concentração de Pt muito alta ({metal.PctSample:P2}). Verificar amostra.",
                            Recommendation = "Concentrações altas de PGM são raras. Verifique condições de iluminação."
                        });
                    }
                }
            }
        }

        private void CheckParticles(SampleFullAnalysisResult analysisResult, ConsistencyCheckResult result)
        {
            if (analysisResult.Particles == null || analysisResult.Particles.Count == 0)
            {
                result.Alerts.Add(new ConsistencyAlert
                {
                    Severity = ConsistencyAlertSeverity.Info,
                    Code = "NO_PARTICLES",
                    Message = "Nenhuma partícula individual identificada.",
                    Recommendation = "A análise foi baseada em estatísticas globais."
                });
            }
            else
            {
                // Verificar partículas com baixa confiança
                int lowConfCount = 0;
                foreach (var p in analysisResult.Particles)
                {
                    if (p.Confidence < 0.5)
                        lowConfCount++;
                }

                if (lowConfCount > analysisResult.Particles.Count / 2)
                {
                    result.Alerts.Add(new ConsistencyAlert
                    {
                        Severity = ConsistencyAlertSeverity.Warning,
                        Code = "LOW_CONFIDENCE_PARTICLES",
                        Message = $"Muitas partículas com baixa confiança ({lowConfCount}/{analysisResult.Particles.Count}).",
                        Recommendation = "Considere melhorar condições de imagem ou calibrar o sistema."
                    });
                }
            }
        }

        private string GenerateSummary(ConsistencyCheckResult result)
        {
            if (result.Alerts.Count == 0)
            {
                return "Nenhum problema de consistência detectado. Análise OK.";
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Verificação de consistência: {result.Alerts.Count} alerta(s)");

            int critical = 0, error = 0, warning = 0, info = 0;
            foreach (var a in result.Alerts)
            {
                switch (a.Severity)
                {
                    case ConsistencyAlertSeverity.Critical: critical++; break;
                    case ConsistencyAlertSeverity.Error: error++; break;
                    case ConsistencyAlertSeverity.Warning: warning++; break;
                    case ConsistencyAlertSeverity.Info: info++; break;
                }
            }

            if (critical > 0) sb.AppendLine($"  - Críticos: {critical}");
            if (error > 0) sb.AppendLine($"  - Erros: {error}");
            if (warning > 0) sb.AppendLine($"  - Avisos: {warning}");
            if (info > 0) sb.AppendLine($"  - Informativos: {info}");

            sb.AppendLine($"Status sugerido: {result.SuggestedQualityStatus}");

            return sb.ToString();
        }
    }
}
