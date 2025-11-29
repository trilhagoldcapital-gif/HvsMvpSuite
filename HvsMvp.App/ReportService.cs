using System;
using System.IO;
using System.Text;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;

namespace HvsMvp.App
{
    /// <summary>
    /// Service for generating professional reports (TXT and PDF).
    /// PR17: Added localization support for multi-language reports.
    /// </summary>
    public class ReportService
    {
        private readonly AppSettings _settings;
        
        // PR17: Helper to get localized string
        private string L(string key) => LocalizationService.Instance.Get(key);

        public ReportService(AppSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Generate professional TXT report with corporate structure.
        /// </summary>
        public string GenerateProfessionalTxtReport(SampleFullAnalysisResult result, string? sampleName = null, string? clientProject = null)
        {
            var sb = new StringBuilder();
            
            // ═══════════════════════════════════════════
            // HEADER
            // ═══════════════════════════════════════════
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"              {_settings.LabName}");
            sb.AppendLine("              LAUDO DE ANÁLISE MINERALÓGICA");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            
            sb.AppendLine($"  ID da Análise:       {result.Id}");
            if (!string.IsNullOrWhiteSpace(sampleName))
                sb.AppendLine($"  Amostra:             {sampleName}");
            if (!string.IsNullOrWhiteSpace(clientProject))
                sb.AppendLine($"  Cliente/Projeto:     {clientProject}");
            if (!string.IsNullOrWhiteSpace(_settings.DefaultOperator))
                sb.AppendLine($"  Operador:            {_settings.DefaultOperator}");
            sb.AppendLine($"  Data/Hora:           {result.CaptureDateTimeUtc:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"  Versão MicroLab:     v{UpdateService.GetCurrentVersion()}");
            sb.AppendLine();

            // ═══════════════════════════════════════════
            // RESUMO EXECUTIVO
            // ═══════════════════════════════════════════
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine("  RESUMO EXECUTIVO");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            // Main metals
            var topMetals = new System.Collections.Generic.List<string>();
            foreach (var m in result.Metals)
            {
                if (m.PctSample > 0.0001)
                {
                    string ppm = m.PpmEstimated.HasValue ? $"{m.PpmEstimated.Value:F0} ppm" : "-";
                    topMetals.Add($"{m.Name} ({m.Id}): {m.PctSample:P2} / {ppm}");
                }
                if (topMetals.Count >= 3) break;
            }
            
            if (topMetals.Count > 0)
            {
                sb.AppendLine($"  Principais metais: {string.Join(", ", topMetals)}");
            }
            else
            {
                sb.AppendLine("  Principais metais: Nenhum metal detectado acima do limiar.");
            }
            
            // Phases/minerals summary
            int totalMinerals = result.Crystals.Count + result.Gems.Count;
            if (totalMinerals > 0)
            {
                sb.AppendLine($"  Fases/minerais detectados: {totalMinerals} (cristais: {result.Crystals.Count}, gemas: {result.Gems.Count})");
            }
            
            // Quality status
            string qualityEmoji = result.QualityStatus switch
            {
                "Official" => "✅",
                "OfficialRechecked" => "✅✅",
                "Preliminary" => "⚠️",
                "ReviewRequired" => "⚠️⚠️",
                _ => "❌"
            };
            sb.AppendLine($"  Qualidade: {qualityEmoji} {result.QualityStatus} (índice: {result.QualityIndex:F1}/100)");
            sb.AppendLine();

            // ═══════════════════════════════════════════
            // SEÇÃO METAIS
            // ═══════════════════════════════════════════
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine($"  {L("report.metals.detected")}");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            // PR17: Add special gold confidence indicator section
            var goldResult = result.Metals.Find(m => m.Id == "Au");
            if (goldResult != null)
            {
                string goldConfidence = GetGoldConfidenceIndicator(goldResult);
                sb.AppendLine($"  {L("report.gold.indicator")}");
                sb.AppendLine($"     {L("report.table.score")}: {goldResult.Score:F3} | {L("report.table.confidence")}: {goldConfidence}");
                sb.AppendLine($"     Fração: {goldResult.PctSample:P4} | PPM: {goldResult.PpmEstimated?.ToString("F0") ?? "-"}");
                
                // Add detailed confidence message
                if (goldResult.Score >= 0.72)
                {
                    sb.AppendLine($"     {L("report.gold.high.confidence")}");
                }
                else if (goldResult.Score >= 0.52)
                {
                    sb.AppendLine($"     {L("report.gold.medium.confidence")}");
                }
                else if (goldResult.Score >= 0.38)
                {
                    sb.AppendLine($"     {L("report.gold.low.confidence")}");
                }
                else
                {
                    sb.AppendLine($"     {L("report.gold.indeterminate")}");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine($"  {L("report.table.metal").PadRight(14)} | {L("report.table.score").PadLeft(7)} | {L("report.table.confidence").PadLeft(9)} | {L("report.table.sample.pct").PadLeft(9)} | {L("report.table.ppm").PadLeft(10)} | {L("report.table.group")}");
            sb.AppendLine("  ────────────────┼─────────┼───────────┼───────────┼────────────┼──────────");
            
            foreach (var m in result.Metals)
            {
                if (m.PctSample > 0.00001 || m.Id == "Au" || m.Id == "Pt")
                {
                    string name = (m.Name ?? m.Id).PadRight(14);
                    string score = m.Score.ToString("F3").PadLeft(7);
                    string confidence = GetConfidenceLevel(m.Score).PadLeft(9);
                    string pct = m.PctSample.ToString("P4").PadLeft(9);
                    string ppm = (m.PpmEstimated?.ToString("F0") ?? "-").PadLeft(10);
                    string group = (m.Group ?? "-").PadRight(8);
                    sb.AppendLine($"  {name} | {score} | {confidence} | {pct} | {ppm} | {group}");
                }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════
            // SEÇÃO CRISTAIS/GEMAS
            // ═══════════════════════════════════════════
            if (result.Crystals.Count > 0 || result.Gems.Count > 0)
            {
                sb.AppendLine("───────────────────────────────────────────────────────────────────");
                sb.AppendLine("  MINERAIS / CRISTAIS / GEMAS");
                sb.AppendLine("───────────────────────────────────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine("  Material        | Tipo     | Score   | % Amostra");
                sb.AppendLine("  ────────────────┼──────────┼─────────┼───────────");
                
                foreach (var c in result.Crystals)
                {
                    if (c.PctSample > 0.00001)
                    {
                        string name = (c.Name ?? c.Id).PadRight(14);
                        string score = c.Score.ToString("F3").PadLeft(7);
                        string pct = c.PctSample.ToString("P4").PadLeft(9);
                        sb.AppendLine($"  {name} | Cristal  | {score} | {pct}");
                    }
                }
                
                foreach (var g in result.Gems)
                {
                    if (g.PctSample > 0.00001)
                    {
                        string name = (g.Name ?? g.Id).PadRight(14);
                        string score = g.Score.ToString("F3").PadLeft(7);
                        string pct = g.PctSample.ToString("P4").PadLeft(9);
                        sb.AppendLine($"  {name} | Gema     | {score} | {pct}");
                    }
                }
                sb.AppendLine();
            }

            // ═══════════════════════════════════════════
            // SEÇÃO QUALIDADE DE IMAGEM
            // ═══════════════════════════════════════════
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine("  QUALIDADE DA ANÁLISE");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            var d = result.Diagnostics;
            string focusStatus = d.FocusScorePercent >= 50 ? "✅" : (d.FocusScorePercent >= 30 ? "⚠️" : "❌");
            string exposureStatus = d.ExposureScore >= 70 ? "✅" : (d.ExposureScore >= 50 ? "⚠️" : "❌");
            string maskStatus = d.MaskScore >= 70 ? "✅" : (d.MaskScore >= 50 ? "⚠️" : "❌");
            
            sb.AppendLine($"  Foco:             {focusStatus} {d.FocusScorePercent:F1}/100 (bruto: {d.FocusScore:F3})");
            sb.AppendLine($"  Exposição:        {exposureStatus} {d.ExposureScore:F1}/100");
            sb.AppendLine($"  Máscara:          {maskStatus} {d.MaskScore:F1}/100");
            sb.AppendLine($"  Fração amostra:   {d.ForegroundFraction:P1} ({d.ForegroundFractionStatus})");
            sb.AppendLine($"  Clipping:         {d.SaturationClippingFraction:P2}");
            
            if (d.MaskWarnings != null && d.MaskWarnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  Avisos de qualidade:");
                foreach (var w in d.MaskWarnings)
                {
                    sb.AppendLine($"    ⚠️ {w}");
                }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════
            // PARTÍCULAS RESUMO
            // ═══════════════════════════════════════════
            if (result.Particles != null && result.Particles.Count > 0)
            {
                sb.AppendLine("───────────────────────────────────────────────────────────────────");
                sb.AppendLine("  PARTÍCULAS DETECTADAS");
                sb.AppendLine("───────────────────────────────────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine($"  Total de partículas: {result.Particles.Count}");
                
                // Count by material
                var byMaterial = new System.Collections.Generic.Dictionary<string, int>();
                long totalArea = 0;
                foreach (var p in result.Particles)
                {
                    string mat = p.MaterialId ?? "Unknown";
                    if (!byMaterial.ContainsKey(mat))
                        byMaterial[mat] = 0;
                    byMaterial[mat]++;
                    totalArea += p.ApproxAreaPixels;
                }
                
                sb.AppendLine($"  Área total: {totalArea:N0} pixels");
                sb.AppendLine();
                sb.AppendLine("  Por material:");
                foreach (var kvp in byMaterial)
                {
                    sb.AppendLine($"    {kvp.Key}: {kvp.Value} partículas");
                }
                sb.AppendLine();
            }

            // ═══════════════════════════════════════════
            // FOOTER
            // ═══════════════════════════════════════════
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  Gerado por MicroLab HVS-MVP v{UpdateService.GetCurrentVersion()}");
            sb.AppendLine($"  {_settings.LabName}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        /// <summary>
        /// Export professional TXT report to file.
        /// </summary>
        public string ExportTxtReport(SampleFullAnalysisResult result, string? sampleName = null, string? clientProject = null)
        {
            string dir = GetReportsDirectory();
            Directory.CreateDirectory(dir);

            string fileName = $"laudo_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{result.Id:N}.txt";
            string path = Path.Combine(dir, fileName);

            string content = GenerateProfessionalTxtReport(result, sampleName, clientProject);
            File.WriteAllText(path, content, Encoding.UTF8);

            return path;
        }

        /// <summary>
        /// Generate professional PDF report.
        /// </summary>
        public string ExportPdfReport(SampleFullAnalysisResult result, string? sampleName = null, string? clientProject = null)
        {
            string dir = GetReportsDirectory();
            Directory.CreateDirectory(dir);

            string fileName = $"laudo_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{result.Id:N}.pdf";
            string path = Path.Combine(dir, fileName);

            using var document = new PdfDocument();
            document.Info.Title = $"Laudo de Análise - {_settings.LabName}";
            document.Info.Author = _settings.DefaultOperator ?? "MicroLab";
            document.Info.Subject = $"Análise {result.Id}";

            var page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;

            using var gfx = XGraphics.FromPdfPage(page);
            
            // Fonts
            var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 12, XFontStyle.Bold);
            var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);
            var smallFont = new XFont("Arial", 8, XFontStyle.Regular);
            
            double margin = 50;
            double y = margin;
            double pageWidth = page.Width - 2 * margin;

            // Title
            gfx.DrawString(_settings.LabName, titleFont, XBrushes.DarkGoldenrod, 
                new XRect(margin, y, pageWidth, 25), XStringFormats.TopCenter);
            y += 25;
            
            gfx.DrawString("LAUDO DE ANÁLISE MINERALÓGICA", headerFont, XBrushes.Black,
                new XRect(margin, y, pageWidth, 20), XStringFormats.TopCenter);
            y += 30;

            // Line separator
            gfx.DrawLine(XPens.DarkGoldenrod, margin, y, page.Width - margin, y);
            y += 15;

            // Header info
            DrawLabelValue(gfx, bodyFont, margin, ref y, "ID da Análise:", result.Id.ToString());
            if (!string.IsNullOrWhiteSpace(sampleName))
                DrawLabelValue(gfx, bodyFont, margin, ref y, "Amostra:", sampleName);
            if (!string.IsNullOrWhiteSpace(clientProject))
                DrawLabelValue(gfx, bodyFont, margin, ref y, "Cliente/Projeto:", clientProject);
            if (!string.IsNullOrWhiteSpace(_settings.DefaultOperator))
                DrawLabelValue(gfx, bodyFont, margin, ref y, "Operador:", _settings.DefaultOperator);
            DrawLabelValue(gfx, bodyFont, margin, ref y, "Data/Hora:", $"{result.CaptureDateTimeUtc:yyyy-MM-dd HH:mm:ss} UTC");
            DrawLabelValue(gfx, bodyFont, margin, ref y, "Versão:", $"MicroLab v{UpdateService.GetCurrentVersion()}");
            y += 10;

            // Quality status box
            XBrush statusBrush = result.QualityStatus switch
            {
                "Official" or "OfficialRechecked" => XBrushes.DarkGreen,
                "Preliminary" => XBrushes.DarkOrange,
                _ => XBrushes.DarkRed
            };
            gfx.DrawRectangle(statusBrush, margin, y, pageWidth, 25);
            gfx.DrawString($"Qualidade: {result.QualityStatus} ({result.QualityIndex:F1}/100)", 
                new XFont("Arial", 11, XFontStyle.Bold), XBrushes.White,
                new XRect(margin, y + 5, pageWidth, 20), XStringFormats.TopCenter);
            y += 35;

            // Section: Metals
            gfx.DrawString("METAIS DETECTADOS", headerFont, XBrushes.DarkGoldenrod,
                new XRect(margin, y, pageWidth, 15), XStringFormats.TopLeft);
            y += 20;

            // Table header
            double col1 = margin;
            double col2 = margin + 120;
            double col3 = margin + 180;
            double col4 = margin + 260;
            double col5 = margin + 340;

            gfx.DrawString("Metal", smallFont, XBrushes.Black, col1, y);
            gfx.DrawString("Score", smallFont, XBrushes.Black, col2, y);
            gfx.DrawString("% Amostra", smallFont, XBrushes.Black, col3, y);
            gfx.DrawString("PPM", smallFont, XBrushes.Black, col4, y);
            gfx.DrawString("Grupo", smallFont, XBrushes.Black, col5, y);
            y += 15;

            gfx.DrawLine(XPens.Gray, margin, y - 3, page.Width - margin, y - 3);

            int metalCount = 0;
            foreach (var m in result.Metals)
            {
                if (m.PctSample > 0.00001 || m.Id == "Au" || m.Id == "Pt")
                {
                    gfx.DrawString(m.Name ?? m.Id, smallFont, XBrushes.Black, col1, y);
                    gfx.DrawString(m.Score.ToString("F3"), smallFont, XBrushes.Black, col2, y);
                    gfx.DrawString(m.PctSample.ToString("P3"), smallFont, XBrushes.Black, col3, y);
                    gfx.DrawString(m.PpmEstimated?.ToString("F0") ?? "-", smallFont, XBrushes.Black, col4, y);
                    gfx.DrawString(m.Group ?? "-", smallFont, XBrushes.Black, col5, y);
                    y += 12;
                    metalCount++;
                    if (metalCount >= 10 || y > page.Height - 150) break;
                }
            }
            y += 15;

            // Section: Quality
            if (y < page.Height - 150)
            {
                gfx.DrawString("QUALIDADE DA ANÁLISE", headerFont, XBrushes.DarkGoldenrod,
                    new XRect(margin, y, pageWidth, 15), XStringFormats.TopLeft);
                y += 20;

                var d = result.Diagnostics;
                DrawLabelValue(gfx, smallFont, margin, ref y, "Foco:", $"{d.FocusScorePercent:F1}/100");
                DrawLabelValue(gfx, smallFont, margin, ref y, "Exposição:", $"{d.ExposureScore:F1}/100");
                DrawLabelValue(gfx, smallFont, margin, ref y, "Máscara:", $"{d.MaskScore:F1}/100");
                DrawLabelValue(gfx, smallFont, margin, ref y, "Fração amostra:", $"{d.ForegroundFraction:P1}");
                y += 10;
            }

            // Section: Particles summary
            if (result.Particles != null && result.Particles.Count > 0 && y < page.Height - 100)
            {
                gfx.DrawString("PARTÍCULAS", headerFont, XBrushes.DarkGoldenrod,
                    new XRect(margin, y, pageWidth, 15), XStringFormats.TopLeft);
                y += 20;

                DrawLabelValue(gfx, smallFont, margin, ref y, "Total:", $"{result.Particles.Count} partículas");
            }

            // Footer
            y = page.Height - 50;
            gfx.DrawLine(XPens.DarkGoldenrod, margin, y, page.Width - margin, y);
            y += 10;
            gfx.DrawString($"Gerado por MicroLab HVS-MVP v{UpdateService.GetCurrentVersion()} - {_settings.LabName}",
                smallFont, XBrushes.Gray,
                new XRect(margin, y, pageWidth, 15), XStringFormats.TopCenter);

            document.Save(path);
            return path;
        }

        private void DrawLabelValue(XGraphics gfx, XFont font, double margin, ref double y, string label, string value)
        {
            gfx.DrawString(label, font, XBrushes.Gray, margin, y);
            gfx.DrawString(value, font, XBrushes.Black, margin + 100, y);
            y += 14;
        }
        
        /// <summary>
        /// PR17: Get confidence level string for a score (localized).
        /// </summary>
        private string GetConfidenceLevel(double score)
        {
            if (score >= 0.85) return L("report.confidence.very.high");
            if (score >= 0.72) return L("report.confidence.high");
            if (score >= 0.52) return L("report.confidence.medium");
            if (score >= 0.38) return L("report.confidence.low");
            return L("report.confidence.indet");
        }
        
        /// <summary>
        /// PR17: Get detailed gold confidence indicator (localized).
        /// </summary>
        private string GetGoldConfidenceIndicator(MetalResult gold)
        {
            if (gold.Score >= 0.85) return $"🟢 {L("report.confidence.very.high")} (>85%)";
            if (gold.Score >= 0.72) return $"🟢 {L("report.confidence.high")} (72-85%)";
            if (gold.Score >= 0.52) return $"🟡 {L("report.confidence.medium")} (52-72%)";
            if (gold.Score >= 0.38) return $"🟠 {L("report.confidence.low")} (38-52%)";
            return $"🔴 {L("report.confidence.indet")} (<38%)";
        }

        private string GetReportsDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ReportsDirectory))
                return _settings.ReportsDirectory;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports", "reports");
        }
    }
}
