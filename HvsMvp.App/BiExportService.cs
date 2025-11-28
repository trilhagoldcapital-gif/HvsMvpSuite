using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HvsMvp.App
{
    /// <summary>
    /// Service for consolidated BI exports (one line per analysis).
    /// </summary>
    public class BiExportService
    {
        private readonly AppSettings _settings;

        // Fixed set of primary metals for BI columns
        private static readonly string[] PrimaryMetals = new[]
        {
            "Au", "Pt", "Ag", "Cu", "Fe", "Ni", "Zn", "Pb", "Pd", "Rh"
        };

        public BiExportService(AppSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Export analysis to consolidated BI CSV.
        /// Appends to daily file if exists, creates new with header if not.
        /// </summary>
        public string ExportToBiCsv(
            SampleFullAnalysisResult result,
            string? sampleName = null,
            string? clientProject = null,
            string? captureMode = null,
            string? reportStatus = null)
        {
            string dir = GetBiDirectory();
            Directory.CreateDirectory(dir);

            string day = DateTime.UtcNow.ToString("yyyyMMdd");
            string path = Path.Combine(dir, $"bi_consolidado_{day}.csv");

            bool fileExists = File.Exists(path);

            var sb = new StringBuilder();
            
            if (!fileExists)
            {
                // Write header
                sb.Append("AnalysisId,DateTimeUtc,");
                sb.Append("Sample,ClientProject,Operator,CaptureMode,ReportStatus,");
                sb.Append("QualityIndex,QualityStatus,FocusScore,ExposureScore,MaskScore,");
                sb.Append("ForegroundFraction,ClippingFraction,ParticleCount,");
                
                // Metal columns
                foreach (var metal in PrimaryMetals)
                {
                    sb.Append($"Pct_{metal},");
                }
                
                sb.Append("TotalMetalPct,TotalCrystalPct,TotalGemPct,");
                sb.Append("AvgParticleConfidence,TotalAreaPixels,MicroLabVersion");
                sb.AppendLine();
            }

            // Build data line
            var d = result.Diagnostics;
            
            sb.Append($"\"{result.Id}\",");
            sb.Append($"\"{result.CaptureDateTimeUtc:O}\",");
            sb.Append($"\"{EscapeCsv(sampleName ?? "")}\",");
            sb.Append($"\"{EscapeCsv(clientProject ?? "")}\",");
            sb.Append($"\"{EscapeCsv(_settings.DefaultOperator)}\",");
            sb.Append($"\"{EscapeCsv(captureMode ?? "Image")}\",");
            sb.Append($"\"{EscapeCsv(reportStatus ?? result.QualityStatus)}\",");
            sb.Append($"{result.QualityIndex:F2},");
            sb.Append($"\"{result.QualityStatus}\",");
            sb.Append($"{d.FocusScorePercent:F2},");
            sb.Append($"{d.ExposureScore:F2},");
            sb.Append($"{d.MaskScore:F2},");
            sb.Append($"{d.ForegroundFraction:F6},");
            sb.Append($"{d.SaturationClippingFraction:F6},");
            sb.Append($"{result.Particles?.Count ?? 0},");

            // Metal percentages
            var metalPcts = new Dictionary<string, double>();
            double totalMetalPct = 0;
            foreach (var m in result.Metals)
            {
                metalPcts[m.Id] = m.PctSample;
                totalMetalPct += m.PctSample;
            }

            foreach (var metal in PrimaryMetals)
            {
                double pct = metalPcts.TryGetValue(metal, out var v) ? v : 0;
                sb.Append($"{pct:F8},");
            }

            // Total percentages
            double totalCrystalPct = 0;
            foreach (var c in result.Crystals)
                totalCrystalPct += c.PctSample;

            double totalGemPct = 0;
            foreach (var g in result.Gems)
                totalGemPct += g.PctSample;

            sb.Append($"{totalMetalPct:F6},");
            sb.Append($"{totalCrystalPct:F6},");
            sb.Append($"{totalGemPct:F6},");

            // Particle stats
            double avgConf = 0;
            long totalArea = 0;
            if (result.Particles != null && result.Particles.Count > 0)
            {
                double sumConf = 0;
                foreach (var p in result.Particles)
                {
                    sumConf += p.Confidence;
                    totalArea += p.ApproxAreaPixels;
                }
                avgConf = sumConf / result.Particles.Count;
            }

            sb.Append($"{avgConf:F4},");
            sb.Append($"{totalArea},");
            sb.Append($"\"{UpdateService.GetCurrentVersion()}\"");
            sb.AppendLine();

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        /// <summary>
        /// Export batch of analyses to BI CSV.
        /// </summary>
        public string ExportBatchToBiCsv(IEnumerable<SampleFullAnalysisResult> results)
        {
            string? lastPath = null;
            foreach (var result in results)
            {
                lastPath = ExportToBiCsv(result);
            }
            return lastPath ?? GetBiDirectory();
        }

        /// <summary>
        /// Get the BI export directory.
        /// </summary>
        public string GetBiDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ReportsDirectory))
                return Path.Combine(_settings.ReportsDirectory, "bi");
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports", "bi");
        }

        /// <summary>
        /// Get CSV header description for Power BI/Excel users.
        /// </summary>
        public string GetBiCsvHeaderDescription()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# MicroLab BI Export - Descrição dos Campos");
            sb.AppendLine("#");
            sb.AppendLine("# AnalysisId: ID único da análise (GUID)");
            sb.AppendLine("# DateTimeUtc: Data/hora UTC da análise (ISO 8601)");
            sb.AppendLine("# Sample: Nome/identificador da amostra");
            sb.AppendLine("# ClientProject: Cliente ou projeto associado");
            sb.AppendLine("# Operator: Operador que realizou a análise");
            sb.AppendLine("# CaptureMode: Modo de captura (Image/Live/Continuous/Selective)");
            sb.AppendLine("# ReportStatus: Status do laudo (Official/Preliminary/Invalid/ReviewRequired)");
            sb.AppendLine("# QualityIndex: Índice de qualidade 0-100");
            sb.AppendLine("# QualityStatus: Status de qualidade");
            sb.AppendLine("# FocusScore: Score de foco 0-100");
            sb.AppendLine("# ExposureScore: Score de exposição 0-100");
            sb.AppendLine("# MaskScore: Score de máscara 0-100");
            sb.AppendLine("# ForegroundFraction: Fração de pixels de amostra (0-1)");
            sb.AppendLine("# ClippingFraction: Fração de pixels saturados (0-1)");
            sb.AppendLine("# ParticleCount: Número de partículas detectadas");
            sb.AppendLine("# Pct_XX: Porcentagem do metal XX na amostra (0-1)");
            sb.AppendLine("# TotalMetalPct: Porcentagem total de metais");
            sb.AppendLine("# TotalCrystalPct: Porcentagem total de cristais");
            sb.AppendLine("# TotalGemPct: Porcentagem total de gemas");
            sb.AppendLine("# AvgParticleConfidence: Confiança média das partículas (0-1)");
            sb.AppendLine("# TotalAreaPixels: Área total das partículas em pixels");
            sb.AppendLine("# MicroLabVersion: Versão do MicroLab");
            return sb.ToString();
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }
    }
}
