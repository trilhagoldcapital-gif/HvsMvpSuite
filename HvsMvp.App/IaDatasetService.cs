using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HvsMvp.App
{
    /// <summary>
    /// Service for exporting structured IA datasets per particle.
    /// </summary>
    public class IaDatasetService
    {
        private readonly AppSettings _settings;
        private const int DefaultCropSize = 64;
        private const int MinParticleAreaPixels = 16;

        public IaDatasetService(AppSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Export full IA dataset for an analysis.
        /// Creates particle image patches and metadata files.
        /// </summary>
        public IaDatasetExportResult ExportDataset(
            FullSceneAnalysis scene,
            Bitmap sourceImage,
            string? operatorName = null)
        {
            var result = new IaDatasetExportResult
            {
                AnalysisId = scene.Summary.Id,
                ExportedAt = DateTime.UtcNow
            };

            if (scene.Summary.Particles == null || scene.Summary.Particles.Count == 0)
            {
                result.Success = false;
                result.Message = "Nenhuma partícula disponível para exportar.";
                return result;
            }

            string baseDir = GetDatasetDirectory();
            string analysisDir = Path.Combine(baseDir, "particles");
            Directory.CreateDirectory(analysisDir);

            // Create CSV index file
            string csvPath = Path.Combine(baseDir, $"particles_index_{scene.Summary.Id:N}.csv");
            var csvSb = new StringBuilder();
            csvSb.AppendLine("ParticleId,AnalysisId,MaterialPredicted,MaterialLabel,AreaPixels,Circularity,AspectRatio,Confidence,AvgPixelConfidence,FocusScore,QualityStatus,AlgorithmVersion,ImagePath,ExportedAt");

            int exportedCount = 0;
            int skippedCount = 0;

            foreach (var particle in scene.Summary.Particles)
            {
                try
                {
                    // Skip very small particles
                    if (particle.ApproxAreaPixels < MinParticleAreaPixels)
                    {
                        skippedCount++;
                        continue;
                    }

                    string material = string.IsNullOrWhiteSpace(particle.MaterialId) ? "Unknown" : particle.MaterialId;
                    string matDir = Path.Combine(analysisDir, material);
                    Directory.CreateDirectory(matDir);

                    // Extract crop
                    var cropResult = ExtractParticleCrop(sourceImage, particle, DefaultCropSize);
                    if (cropResult.Crop == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Generate file names
                    string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                    string fileBase = $"p_{particle.ParticleId:N}_{ts}";
                    string imgPath = Path.Combine(matDir, fileBase + ".png");
                    string jsonPath = Path.Combine(matDir, fileBase + ".json");

                    // Save image
                    cropResult.Crop.Save(imgPath, ImageFormat.Png);
                    cropResult.Crop.Dispose();

                    // Create metadata
                    var meta = new ParticleDatasetMetadata
                    {
                        ParticleId = particle.ParticleId,
                        AnalysisId = scene.Summary.Id,
                        MaterialPredicted = particle.MaterialId,
                        MaterialLabel = null, // To be filled by QA
                        AreaPixels = particle.ApproxAreaPixels,
                        AreaPhysical = particle.AreaPhysical,
                        Circularity = particle.Circularity,
                        AspectRatio = particle.AspectRatio,
                        Perimeter = particle.Perimeter,
                        CenterX = particle.CenterX,
                        CenterY = particle.CenterY,
                        BoundingBox = new int[] { particle.BoundingBoxX, particle.BoundingBoxY, particle.BoundingBoxWidth, particle.BoundingBoxHeight },
                        CropBox = new int[] { cropResult.CropX, cropResult.CropY, cropResult.CropWidth, cropResult.CropHeight },
                        Confidence = particle.Confidence,
                        AvgPixelConfidence = particle.AveragePixelConfidence,
                        ConfidenceStdDev = particle.ConfidenceStdDev,
                        AvgH = particle.AvgH,
                        AvgS = particle.AvgS,
                        AvgV = particle.AvgV,
                        ScoreHvs = particle.ScoreHvs,
                        ScoreIa = particle.ScoreIa,
                        ScoreCombined = particle.ScoreCombined,
                        IsMixed = particle.IsMixed,
                        Composition = particle.Composition,
                        FocusScore = scene.Summary.Diagnostics.FocusScorePercent,
                        QualityStatus = scene.Summary.QualityStatus,
                        QualityIndex = scene.Summary.QualityIndex,
                        AlgorithmVersion = UpdateService.GetCurrentVersion(),
                        ImageRelativePath = Path.GetRelativePath(baseDir, imgPath),
                        ExportedAtUtc = DateTime.UtcNow,
                        ExportedBy = operatorName ?? _settings.DefaultOperator
                    };

                    // Save JSON
                    var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(jsonPath, json, Encoding.UTF8);

                    // Add to CSV
                    string relPath = Path.GetRelativePath(baseDir, imgPath);
                    csvSb.AppendLine(
                        $"\"{particle.ParticleId}\"," +
                        $"\"{scene.Summary.Id}\"," +
                        $"\"{EscapeCsv(particle.MaterialId)}\"," +
                        $"\"\"," + // MaterialLabel - empty until QA
                        $"{particle.ApproxAreaPixels}," +
                        $"{particle.Circularity:F4}," +
                        $"{particle.AspectRatio:F4}," +
                        $"{particle.Confidence:F4}," +
                        $"{particle.AveragePixelConfidence:F4}," +
                        $"{scene.Summary.Diagnostics.FocusScorePercent:F2}," +
                        $"\"{scene.Summary.QualityStatus}\"," +
                        $"\"{UpdateService.GetCurrentVersion()}\"," +
                        $"\"{relPath}\"," +
                        $"\"{DateTime.UtcNow:O}\"");

                    exportedCount++;
                    result.ExportedParticles.Add(particle.ParticleId);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Partícula {particle.ParticleId}: {ex.Message}");
                    skippedCount++;
                }
            }

            // Save CSV index
            File.WriteAllText(csvPath, csvSb.ToString(), Encoding.UTF8);

            result.Success = exportedCount > 0;
            result.ExportedCount = exportedCount;
            result.SkippedCount = skippedCount;
            result.OutputDirectory = baseDir;
            result.CsvIndexPath = csvPath;
            result.Message = $"Exportadas {exportedCount} partículas, {skippedCount} ignoradas.";

            return result;
        }

        /// <summary>
        /// Extract particle crop from source image.
        /// </summary>
        private (Bitmap? Crop, int CropX, int CropY, int CropWidth, int CropHeight) ExtractParticleCrop(
            Bitmap source, ParticleRecord particle, int cropSize)
        {
            int half = cropSize / 2;
            int x1 = Math.Max(0, particle.CenterX - half);
            int y1 = Math.Max(0, particle.CenterY - half);
            int x2 = Math.Min(source.Width, particle.CenterX + half);
            int y2 = Math.Min(source.Height, particle.CenterY + half);

            int w = x2 - x1;
            int h = y2 - y1;

            if (w <= 4 || h <= 4)
                return (null, 0, 0, 0, 0);

            var crop = new Bitmap(w, h);
            using (var g = Graphics.FromImage(crop))
            {
                g.DrawImage(source,
                    new Rectangle(0, 0, w, h),
                    new Rectangle(x1, y1, w, h),
                    GraphicsUnit.Pixel);
            }

            return (crop, x1, y1, w, h);
        }

        /// <summary>
        /// Get the IA dataset directory.
        /// </summary>
        public string GetDatasetDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports", "dataset-ia");
        }

        private static string EscapeCsv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }
    }

    /// <summary>
    /// Result of IA dataset export operation.
    /// </summary>
    public class IaDatasetExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public Guid AnalysisId { get; set; }
        public DateTime ExportedAt { get; set; }
        public int ExportedCount { get; set; }
        public int SkippedCount { get; set; }
        public string OutputDirectory { get; set; } = "";
        public string CsvIndexPath { get; set; } = "";
        public List<Guid> ExportedParticles { get; set; } = new List<Guid>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Metadata structure for particle in IA dataset.
    /// </summary>
    public class ParticleDatasetMetadata
    {
        public Guid ParticleId { get; set; }
        public Guid AnalysisId { get; set; }
        public string? MaterialPredicted { get; set; }
        public string? MaterialLabel { get; set; }
        public int AreaPixels { get; set; }
        public double? AreaPhysical { get; set; }
        public double Circularity { get; set; }
        public double AspectRatio { get; set; }
        public int Perimeter { get; set; }
        public int CenterX { get; set; }
        public int CenterY { get; set; }
        public int[]? BoundingBox { get; set; }
        public int[]? CropBox { get; set; }
        public double Confidence { get; set; }
        public double AvgPixelConfidence { get; set; }
        public double ConfidenceStdDev { get; set; }
        public double AvgH { get; set; }
        public double AvgS { get; set; }
        public double AvgV { get; set; }
        public double ScoreHvs { get; set; }
        public double ScoreIa { get; set; }
        public double ScoreCombined { get; set; }
        public bool IsMixed { get; set; }
        public Dictionary<string, double>? Composition { get; set; }
        public double FocusScore { get; set; }
        public string? QualityStatus { get; set; }
        public double QualityIndex { get; set; }
        public string? AlgorithmVersion { get; set; }
        public string? ImageRelativePath { get; set; }
        public DateTime ExportedAtUtc { get; set; }
        public string? ExportedBy { get; set; }
    }
}
