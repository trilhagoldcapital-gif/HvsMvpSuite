using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HvsMvp.App
{
    /// <summary>
    /// Serviço de exportação de datasets para IA e BI.
    /// </summary>
    public class DatasetExporter
    {
        private readonly string _baseDir;

        public DatasetExporter()
        {
            _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datasets");
        }

        /// <summary>
        /// Exporta análise completa para dataset de IA por partícula.
        /// </summary>
        public int ExportParticleDataset(
            FullSceneAnalysis scene,
            Bitmap sourceImage,
            string? targetSubfolder = null)
        {
            string subDir = targetSubfolder ?? "ia-particles";
            string baseDir = Path.Combine(_baseDir, subDir);
            Directory.CreateDirectory(baseDir);

            int exported = 0;
            const int CROP_SIZE = 64;

            foreach (var particle in scene.Summary.Particles)
            {
                try
                {
                    string material = string.IsNullOrWhiteSpace(particle.MaterialId) ? "Unknown" : particle.MaterialId;
                    string matDir = Path.Combine(baseDir, material);
                    Directory.CreateDirectory(matDir);

                    // Coordenadas do recorte
                    int half = CROP_SIZE / 2;
                    int x1 = Math.Max(0, particle.CenterX - half);
                    int y1 = Math.Max(0, particle.CenterY - half);
                    int x2 = Math.Min(sourceImage.Width, particle.CenterX + half);
                    int y2 = Math.Min(sourceImage.Height, particle.CenterY + half);

                    int w = x2 - x1;
                    int h = y2 - y1;
                    if (w <= 4 || h <= 4) continue;

                    string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                    string fileBase = $"particle_{scene.Summary.Id}_{particle.ParticleId}_{ts}";

                    // Exportar recorte da imagem
                    string imgPath = Path.Combine(matDir, fileBase + ".png");
                    using (var crop = new Bitmap(w, h))
                    using (var g = Graphics.FromImage(crop))
                    {
                        g.DrawImage(sourceImage,
                            new Rectangle(0, 0, w, h),
                            new Rectangle(x1, y1, w, h),
                            GraphicsUnit.Pixel);
                        crop.Save(imgPath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    // Exportar metadados
                    string jsonPath = Path.Combine(matDir, fileBase + ".json");
                    var meta = new
                    {
                        // IDs
                        analysisId = scene.Summary.Id,
                        particleId = particle.ParticleId,

                        // Classificação
                        materialId = particle.MaterialId,
                        confidence = particle.Confidence,
                        averagePixelConfidence = particle.AveragePixelConfidence,

                        // Localização
                        centerX = particle.CenterX,
                        centerY = particle.CenterY,

                        // Área
                        approxAreaPixels = particle.ApproxAreaPixels,
                        areaPhysical = particle.AreaPhysical,

                        // Forma
                        circularity = particle.Circularity,
                        aspectRatio = particle.AspectRatio,
                        perimeter = particle.Perimeter,

                        // Bounding box
                        boundingBox = new
                        {
                            x = particle.BoundingBoxX,
                            y = particle.BoundingBoxY,
                            width = particle.BoundingBoxWidth,
                            height = particle.BoundingBoxHeight
                        },

                        // HSV
                        avgH = particle.AvgH,
                        avgS = particle.AvgS,
                        avgV = particle.AvgV,

                        // Scores
                        scoreHvs = particle.ScoreHvs,
                        scoreIa = particle.ScoreIa,
                        scoreCombined = particle.ScoreCombined,

                        // Composição
                        composition = particle.Composition,
                        isMixed = particle.IsMixed,

                        // Qualidade da análise
                        quality = new
                        {
                            index = scene.Summary.QualityIndex,
                            status = scene.Summary.QualityStatus
                        },

                        // Metadados
                        imageRelativePath = Path.GetRelativePath(baseDir, imgPath),
                        exportedAtUtc = DateTime.UtcNow.ToString("o")
                    };

                    var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(jsonPath, json, Encoding.UTF8);

                    exported++;
                }
                catch
                {
                    // Continuar com próxima partícula
                }
            }

            return exported;
        }

        /// <summary>
        /// Exporta resultados para CSV de BI (análise agregada).
        /// </summary>
        public string ExportBiCsv(SampleFullAnalysisResult result)
        {
            string dir = Path.Combine(_baseDir, "bi");
            Directory.CreateDirectory(dir);

            string day = DateTime.UtcNow.ToString("yyyyMMdd");
            string path = Path.Combine(dir, $"bi_{day}.csv");

            bool fileExists = File.Exists(path);

            var sb = new StringBuilder();
            if (!fileExists)
            {
                // Header completo
                sb.AppendLine(
                    "AnalysisId,DateUtc,QualityIndex,QualityStatus," +
                    "FocusScore,ExposureScore,MaskScore," +
                    "ForegroundFraction,ClippingFraction," +
                    "PctAu,PctPt,PctMetalOther," +
                    "ParticlesCount,TotalAreaPixels," +
                    "AvgParticleConfidence");
            }

            // Extrair dados de metais
            double pctAu = 0, pctPt = 0, pctOther = 0;
            foreach (var m in result.Metals)
            {
                if (string.Equals(m.Id, "Au", StringComparison.OrdinalIgnoreCase))
                    pctAu = m.PctSample;
                else if (string.Equals(m.Id, "Pt", StringComparison.OrdinalIgnoreCase))
                    pctPt = m.PctSample;
                else if (string.Equals(m.Id, "MetalOther", StringComparison.OrdinalIgnoreCase))
                    pctOther = m.PctSample;
            }

            // Calcular métricas de partículas
            int particleCount = result.Particles?.Count ?? 0;
            long totalArea = 0;
            double avgConf = 0;

            if (result.Particles != null && result.Particles.Count > 0)
            {
                double sumConf = 0;
                foreach (var p in result.Particles)
                {
                    totalArea += p.ApproxAreaPixels;
                    sumConf += p.Confidence;
                }
                avgConf = sumConf / result.Particles.Count;
            }

            var d = result.Diagnostics;
            string line =
                $"{result.Id}," +
                $"{result.CaptureDateTimeUtc:O}," +
                $"{result.QualityIndex:F2}," +
                $"{result.QualityStatus}," +
                $"{d.FocusScorePercent:F2}," +
                $"{d.ExposureScore:F2}," +
                $"{d.MaskScore:F2}," +
                $"{d.ForegroundFraction:F6}," +
                $"{d.SaturationClippingFraction:F6}," +
                $"{pctAu:F6}," +
                $"{pctPt:F6}," +
                $"{pctOther:F6}," +
                $"{particleCount}," +
                $"{totalArea}," +
                $"{avgConf:F4}";

            sb.AppendLine(line);

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        /// <summary>
        /// Exporta CSV detalhado de partículas.
        /// </summary>
        public string ExportParticlesCsv(SampleFullAnalysisResult result)
        {
            string dir = Path.Combine(_baseDir, "particles");
            Directory.CreateDirectory(dir);

            string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"particles_{result.Id}_{ts}.csv");

            var sb = new StringBuilder();

            // Header
            sb.AppendLine(
                "AnalysisId,ParticleId,MaterialId,Confidence," +
                "CenterX,CenterY,AreaPixels,AreaPhysical," +
                "Circularity,AspectRatio,Perimeter," +
                "BBoxX,BBoxY,BBoxW,BBoxH," +
                "AvgH,AvgS,AvgV," +
                "ScoreHvs,ScoreIa,ScoreCombined," +
                "IsMixed,AvgPixelConfidence,ConfidenceStdDev");

            // Data
            if (result.Particles != null)
            {
                foreach (var p in result.Particles)
                {
                    sb.AppendLine(
                        $"{result.Id}," +
                        $"{p.ParticleId}," +
                        $"\"{p.MaterialId}\"," +
                        $"{p.Confidence:F4}," +
                        $"{p.CenterX}," +
                        $"{p.CenterY}," +
                        $"{p.ApproxAreaPixels}," +
                        $"{(p.AreaPhysical.HasValue ? p.AreaPhysical.Value.ToString("F4") : "")}," +
                        $"{p.Circularity:F4}," +
                        $"{p.AspectRatio:F4}," +
                        $"{p.Perimeter}," +
                        $"{p.BoundingBoxX}," +
                        $"{p.BoundingBoxY}," +
                        $"{p.BoundingBoxWidth}," +
                        $"{p.BoundingBoxHeight}," +
                        $"{p.AvgH:F2}," +
                        $"{p.AvgS:F4}," +
                        $"{p.AvgV:F4}," +
                        $"{p.ScoreHvs:F4}," +
                        $"{p.ScoreIa:F4}," +
                        $"{p.ScoreCombined:F4}," +
                        $"{(p.IsMixed ? "1" : "0")}," +
                        $"{p.AveragePixelConfidence:F4}," +
                        $"{p.ConfidenceStdDev:F4}");
                }
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        /// <summary>
        /// Exporta resultados.csv compatível com formato anterior.
        /// </summary>
        public string ExportResultadosCsv(SampleFullAnalysisResult result)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "resultados.csv");
            bool fileExists = File.Exists(path);

            var sb = new StringBuilder();
            if (!fileExists)
            {
                // Header compatível + novas colunas
                sb.AppendLine(
                    "Id,DateUtc,ImagePath," +
                    "QualityIndex,QualityStatus," +
                    "FocusScore,ClippingFrac,ForegroundFrac," +
                    "Au_Pct,Au_Ppm,Au_Score," +
                    "Pt_Pct,Pt_Ppm,Pt_Score," +
                    "Other_Pct,Other_Ppm,Other_Score," +
                    "ParticleCount,AvgParticleConf");
            }

            // Extrair dados de metais
            double auPct = 0, auPpm = 0, auScore = 0;
            double ptPct = 0, ptPpm = 0, ptScore = 0;
            double otherPct = 0, otherPpm = 0, otherScore = 0;

            foreach (var m in result.Metals)
            {
                if (string.Equals(m.Id, "Au", StringComparison.OrdinalIgnoreCase))
                {
                    auPct = m.PctSample;
                    auPpm = m.PpmEstimated ?? 0;
                    auScore = m.Score;
                }
                else if (string.Equals(m.Id, "Pt", StringComparison.OrdinalIgnoreCase))
                {
                    ptPct = m.PctSample;
                    ptPpm = m.PpmEstimated ?? 0;
                    ptScore = m.Score;
                }
                else if (string.Equals(m.Id, "MetalOther", StringComparison.OrdinalIgnoreCase))
                {
                    otherPct = m.PctSample;
                    otherPpm = m.PpmEstimated ?? 0;
                    otherScore = m.Score;
                }
            }

            double avgConf = 0;
            if (result.Particles != null && result.Particles.Count > 0)
            {
                double sum = 0;
                foreach (var p in result.Particles)
                    sum += p.Confidence;
                avgConf = sum / result.Particles.Count;
            }

            var d = result.Diagnostics;
            string line =
                $"{result.Id}," +
                $"{result.CaptureDateTimeUtc:O}," +
                $"\"{result.ImagePath ?? ""}\"," +
                $"{result.QualityIndex:F2}," +
                $"{result.QualityStatus}," +
                $"{d.FocusScore:F4}," +
                $"{d.SaturationClippingFraction:F6}," +
                $"{d.ForegroundFraction:F6}," +
                $"{auPct:F6}," +
                $"{auPpm:F2}," +
                $"{auScore:F4}," +
                $"{ptPct:F6}," +
                $"{ptPpm:F2}," +
                $"{ptScore:F4}," +
                $"{otherPct:F6}," +
                $"{otherPpm:F2}," +
                $"{otherScore:F4}," +
                $"{result.Particles?.Count ?? 0}," +
                $"{avgConf:F4}";

            sb.AppendLine(line);

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
    }
}
