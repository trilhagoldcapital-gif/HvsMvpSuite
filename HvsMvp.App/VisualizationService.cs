using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace HvsMvp.App
{
    /// <summary>
    /// Enum para controlar o modo de visualização atual.
    /// </summary>
    public enum ViewMode
    {
        /// <summary>
        /// Imagem original sem sobreposições.
        /// </summary>
        Original,

        /// <summary>
        /// Máscara global da amostra (verde = amostra, azul = fundo).
        /// </summary>
        MaskGlobal,

        /// <summary>
        /// Mapa de fases por material.
        /// </summary>
        MapaFases,

        /// <summary>
        /// Heatmap para um material alvo específico.
        /// </summary>
        HeatmapAlvo,

        /// <summary>
        /// Análise seletiva - filtra e destaca um material específico.
        /// </summary>
        SeletivaAlvo,

        /// <summary>
        /// Fundo mascarado (azul translúcido sobre fundo).
        /// </summary>
        FundoMascarado,

        /// <summary>
        /// PR8: Análise seletiva modo X-ray (fundo cinza, alvo em cor forte).
        /// </summary>
        SeletivaXray,

        /// <summary>
        /// PR8: Análise seletiva combinada Au + PGM.
        /// </summary>
        SeletivaAuPgm
    }

    /// <summary>
    /// PR8: Resultado do cálculo de confiança/incerteza para visualização seletiva.
    /// </summary>
    public class SelectiveConfidenceResult
    {
        /// <summary>
        /// Total de pixels do alvo.
        /// </summary>
        public int TotalTargetPixels { get; set; }

        /// <summary>
        /// Pixels do alvo com alta confiança.
        /// </summary>
        public int HighConfidencePixels { get; set; }

        /// <summary>
        /// Pixels do alvo com baixa confiança (zona de transição).
        /// </summary>
        public int LowConfidencePixels { get; set; }

        /// <summary>
        /// Porcentagem de área com alta confiança (0-100).
        /// </summary>
        public double HighConfidencePercent => TotalTargetPixels > 0 ? (HighConfidencePixels * 100.0 / TotalTargetPixels) : 0;

        /// <summary>
        /// Porcentagem de área com baixa confiança (0-100).
        /// </summary>
        public double LowConfidencePercent => TotalTargetPixels > 0 ? (LowConfidencePixels * 100.0 / TotalTargetPixels) : 0;

        /// <summary>
        /// Fração da amostra ocupada pelo alvo.
        /// </summary>
        public double TargetFractionOfSample { get; set; }

        /// <summary>
        /// PPM estimado do alvo.
        /// </summary>
        public double PpmEstimated { get; set; }

        /// <summary>
        /// Número de partículas do alvo.
        /// </summary>
        public int ParticleCount { get; set; }
    }

    /// <summary>
    /// PR8: Resultado do cálculo combinado Au+PGM.
    /// </summary>
    public class AuPgmCombinedResult
    {
        public double AuFraction { get; set; }
        public double PgmFraction { get; set; }
        public double AuPpm { get; set; }
        public double PgmPpm { get; set; }
        public int AuParticles { get; set; }
        public int PgmParticles { get; set; }
        public int AuPixels { get; set; }
        public int PgmPixels { get; set; }
        public int AuHighConfidence { get; set; }
        public int AuLowConfidence { get; set; }
        public int PgmHighConfidence { get; set; }
        public int PgmLowConfidence { get; set; }
    }

    /// <summary>
    /// PR8: Enum para origem da imagem.
    /// </summary>
    public enum ImageOrigin
    {
        ImageFile,
        CameraLive,
        CameraContinuous,
        CameraFrozen
    }

    /// <summary>
    /// Utilitários para gerar imagens de visualização a partir de máscara e rótulos.
    /// </summary>
    public static class VisualizationService
    {
        /// <summary>
        /// Área mínima de partícula para visualização seletiva (filtra ruído).
        /// Partículas com menos pixels são ignoradas na visualização.
        /// </summary>
        public const int MinPixelsForSelectiveVisualization = 2;

        // Static arrays for 8-connectivity (avoid repeated allocation)
        private static readonly int[] Dx8 = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] Dy8 = { -1, -1, -1, 0, 0, 1, 1, 1 };

        /// <summary>
        /// Gera uma imagem em que:
        /// - Pixels de fundo (IsSample=false) são sobrepostos por azul translúcido.
        /// - Pixels de amostra são mantidos como na imagem base.
        /// </summary>
        public static Bitmap BuildBackgroundMaskedView(
            Bitmap baseImage,
            SampleMaskClass?[,]? mask,
            Bitmap? maskPreview = null)
        {
            if (baseImage == null) throw new ArgumentNullException(nameof(baseImage));

            int w = baseImage.Width;
            int h = baseImage.Height;
            var result = new Bitmap(w, h);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var src = baseImage.GetPixel(x, y);
                    bool isSample = false;

                    if (mask != null &&
                        x >= 0 && y >= 0 &&
                        x < mask.GetLength(0) &&
                        y < mask.GetLength(1))
                    {
                        var m = mask[x, y];
                        if (m != null && m.IsSample)
                            isSample = true;
                    }

                    if (!isSample && maskPreview != null &&
                        x < maskPreview.Width && y < maskPreview.Height)
                    {
                        var mp = maskPreview.GetPixel(x, y);
                        if (mp.G > 80 && mp.G > mp.R + 20 && mp.G > mp.B + 20)
                            isSample = true;
                    }

                    if (!isSample)
                    {
                        Color bg = Color.FromArgb(0, 80, 200);
                        double alpha = 0.6;
                        int r = (int)(src.R * (1 - alpha) + bg.R * alpha);
                        int g = (int)(src.G * (1 - alpha) + bg.G * alpha);
                        int b = (int)(src.B * (1 - alpha) + bg.B * alpha);
                        result.SetPixel(x, y, Color.FromArgb(r, g, b));
                    }
                    else
                    {
                        result.SetPixel(x, y, src);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gera visualização seletiva unificada para um material alvo.
        /// Funciona tanto para imagem estática quanto para frame de câmera/live.
        /// 
        /// A classificação base já foi feita durante a análise - este método apenas
        /// FILTRA e destaca os pixels onde metalPrincipal == alvo, sem reclassificar.
        /// </summary>
        /// <param name="baseImage">Imagem original para sobreposição.</param>
        /// <param name="result">Resultado completo da análise (FullSceneAnalysis).</param>
        /// <param name="targetMaterialId">ID do material a destacar (ex: "Au", "Pt").</param>
        /// <param name="materialType">Tipo do material (0=Metal, 1=Crystal, 2=Gem) para cor do overlay.</param>
        /// <param name="confidenceThreshold">Limiar mínimo de confiança para destacar (padrão 0.5).</param>
        /// <returns>Bitmap com visualização seletiva ou null se houver erro.</returns>
        public static Bitmap? BuildSelectiveView(
            Bitmap baseImage,
            FullSceneAnalysis result,
            string targetMaterialId,
            int materialType = 0,
            double confidenceThreshold = 0.5)
        {
            if (baseImage == null || result == null || result.Labels == null)
                return null;

            if (string.IsNullOrWhiteSpace(targetMaterialId))
                return null;

            int sceneW = result.Width;
            int sceneH = result.Height;

            if (sceneW <= 0 || sceneH <= 0)
                return null;

            // Handle size mismatch by using min dimensions
            int w = Math.Min(baseImage.Width, sceneW);
            int h = Math.Min(baseImage.Height, sceneH);

            if (w <= 0 || h <= 0)
                return null;

            // Determinar cor do overlay baseado no tipo de material
            Color overlayColor = materialType switch
            {
                0 => Color.FromArgb(255, 255, 220, 0),   // Metal: dourado
                1 => Color.FromArgb(255, 0, 255, 0),    // Crystal: verde
                2 => Color.FromArgb(255, 255, 0, 255),  // Gem: magenta
                _ => Color.FromArgb(255, 255, 220, 0)   // Default: dourado
            };

            // Pre-calculate which pixels belong to particles that are large enough
            // This implements the noise filter for small particles
            var validPixels = BuildValidPixelMask(result.Labels, targetMaterialId, w, h, confidenceThreshold);

            var resultBitmap = new Bitmap(w, h);
            int matchCount = 0;

            Color bgOverlay = Color.FromArgb(0, 80, 200);
            double bgAlpha = 0.6;
            double overlayAlpha = 0.6;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var src = baseImage.GetPixel(x, y);

                    // Safe access to labels array
                    PixelLabel? lbl = null;
                    if (x < result.Labels.GetLength(0) && y < result.Labels.GetLength(1))
                    {
                        lbl = result.Labels[x, y];
                    }

                    if (lbl == null)
                    {
                        lbl = new PixelLabel { IsSample = false, MaterialType = PixelMaterialType.Background };
                    }

                    // Background pixels: overlay with blue
                    if (!lbl.IsSample)
                    {
                        int rBg = (int)(src.R * (1 - bgAlpha) + bgOverlay.R * bgAlpha);
                        int gBg = (int)(src.G * (1 - bgAlpha) + bgOverlay.G * bgAlpha);
                        int bBg = (int)(src.B * (1 - bgAlpha) + bgOverlay.B * bgAlpha);
                        resultBitmap.SetPixel(x, y, Color.FromArgb(rBg, gBg, bBg));
                        continue;
                    }

                    // Check if this pixel matches the target AND passes the valid pixel mask (noise filter)
                    bool isTargetMatch = !string.IsNullOrWhiteSpace(lbl.MaterialId) &&
                                         string.Equals(lbl.MaterialId, targetMaterialId, StringComparison.OrdinalIgnoreCase) &&
                                         lbl.MaterialConfidence >= confidenceThreshold &&
                                         validPixels[x, y];

                    if (isTargetMatch)
                    {
                        // Apply overlay color
                        int r = (int)(src.R * (1 - overlayAlpha) + overlayColor.R * overlayAlpha);
                        int g = (int)(src.G * (1 - overlayAlpha) + overlayColor.G * overlayAlpha);
                        int b = (int)(src.B * (1 - overlayAlpha) + overlayColor.B * overlayAlpha);
                        resultBitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                        matchCount++;
                    }
                    else
                    {
                        // Keep original pixel
                        resultBitmap.SetPixel(x, y, src);
                    }
                }
            }

            return resultBitmap;
        }

        /// <summary>
        /// Builds a mask of valid pixels for selective visualization.
        /// Filters out small noise particles (< MinPixelsForSelectiveVisualization).
        /// </summary>
        private static bool[,] BuildValidPixelMask(
            PixelLabel[,] labels,
            string targetMaterialId,
            int w, int h,
            double confidenceThreshold)
        {
            var validPixels = new bool[w, h];
            var visited = new bool[w, h];

            // Use static arrays for 8-connectivity
            var queue = new System.Collections.Generic.Queue<(int x, int y)>();
            var currentCluster = new System.Collections.Generic.List<(int x, int y)>();

            for (int startY = 0; startY < h; startY++)
            {
                for (int startX = 0; startX < w; startX++)
                {
                    if (visited[startX, startY])
                        continue;

                    // Check if this is a target pixel
                    PixelLabel? lbl = null;
                    if (startX < labels.GetLength(0) && startY < labels.GetLength(1))
                    {
                        lbl = labels[startX, startY];
                    }

                    if (lbl == null || !lbl.IsSample ||
                        string.IsNullOrWhiteSpace(lbl.MaterialId) ||
                        !string.Equals(lbl.MaterialId, targetMaterialId, StringComparison.OrdinalIgnoreCase) ||
                        lbl.MaterialConfidence < confidenceThreshold)
                    {
                        visited[startX, startY] = true;
                        continue;
                    }

                    // BFS to find connected target pixels
                    queue.Clear();
                    currentCluster.Clear();

                    visited[startX, startY] = true;
                    queue.Enqueue((startX, startY));

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        currentCluster.Add((cx, cy));

                        for (int k = 0; k < 8; k++)
                        {
                            int nx = cx + Dx8[k];
                            int ny = cy + Dy8[k];

                            if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                                continue;
                            if (visited[nx, ny])
                                continue;

                            PixelLabel? neighLbl = null;
                            if (nx < labels.GetLength(0) && ny < labels.GetLength(1))
                            {
                                neighLbl = labels[nx, ny];
                            }

                            if (neighLbl == null || !neighLbl.IsSample ||
                                string.IsNullOrWhiteSpace(neighLbl.MaterialId) ||
                                !string.Equals(neighLbl.MaterialId, targetMaterialId, StringComparison.OrdinalIgnoreCase) ||
                                neighLbl.MaterialConfidence < confidenceThreshold)
                            {
                                continue;
                            }

                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }

                    // Only mark pixels as valid if cluster is large enough (noise filter)
                    if (currentCluster.Count >= MinPixelsForSelectiveVisualization)
                    {
                        foreach (var (px, py) in currentCluster)
                        {
                            validPixels[px, py] = true;
                        }
                    }
                }
            }

            return validPixels;
        }

        /// <summary>
        /// Retorna descrição textual do modo de visualização para exibição na UI.
        /// </summary>
        public static string GetViewModeDescription(ViewMode mode, string? targetMaterial = null, bool xrayMode = false)
        {
            return mode switch
            {
                ViewMode.Original => "Visualização: Imagem original",
                ViewMode.MaskGlobal => "Visualização: Máscara da amostra",
                ViewMode.MapaFases => "Visualização: Mapa de Fases",
                ViewMode.HeatmapAlvo => $"Visualização: Heatmap Alvo{(targetMaterial != null ? $" – {targetMaterial}" : "")}",
                ViewMode.SeletivaAlvo => $"Visualização: Análise seletiva{(xrayMode ? " X-ray" : "")}{(targetMaterial != null ? $" – Metal: {targetMaterial}" : "")}",
                ViewMode.SeletivaXray => $"Visualização: Análise seletiva X-ray{(targetMaterial != null ? $" – Metal: {targetMaterial}" : "")}",
                ViewMode.SeletivaAuPgm => "Visualização: Análise seletiva – Alvo: Au+PGM",
                ViewMode.FundoMascarado => "Visualização: Fundo mascarado",
                _ => "Visualização: Desconhecido"
            };
        }

        /// <summary>
        /// PR8: Retorna descrição da origem da imagem.
        /// </summary>
        public static string GetImageOriginDescription(ImageOrigin origin)
        {
            return origin switch
            {
                ImageOrigin.ImageFile => "IMAGEM DE ARQUIVO",
                ImageOrigin.CameraLive => "CÂMERA (Live)",
                ImageOrigin.CameraContinuous => "CÂMERA (Contínuo)",
                ImageOrigin.CameraFrozen => "CÂMERA (Congelado)",
                _ => "DESCONHECIDO"
            };
        }

        /// <summary>
        /// PR8: Lista de IDs de metais PGM (Platinum Group Metals).
        /// </summary>
        public static readonly HashSet<string> PgmMetals = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pt", "Pd", "Rh", "Ir", "Ru", "Os"
        };

        /// <summary>
        /// PR8: Verifica se um material é PGM.
        /// </summary>
        public static bool IsPgmMetal(string? materialId)
        {
            return !string.IsNullOrWhiteSpace(materialId) && PgmMetals.Contains(materialId);
        }

        /// <summary>
        /// PR8: Limiar de diferença de confiança para zona de transição (baixa confiança).
        /// Se a diferença entre o melhor e o segundo melhor score for menor que isso, é baixa confiança.
        /// </summary>
        public const double LowConfidenceGapThreshold = 0.05;

        /// <summary>
        /// PR8: Gera visualização seletiva modo X-ray.
        /// Fundo convertido para escala de cinza, apenas o alvo em cor forte.
        /// </summary>
        public static Bitmap? BuildSelectiveXrayView(
            Bitmap baseImage,
            FullSceneAnalysis result,
            string targetMaterialId,
            out SelectiveConfidenceResult? confidenceResult,
            int materialType = 0,
            double confidenceThreshold = 0.5,
            bool showUncertainty = false)
        {
            confidenceResult = null;

            if (baseImage == null || result == null || result.Labels == null)
                return null;

            if (string.IsNullOrWhiteSpace(targetMaterialId))
                return null;

            int sceneW = result.Width;
            int sceneH = result.Height;

            if (sceneW <= 0 || sceneH <= 0)
                return null;

            int w = Math.Min(baseImage.Width, sceneW);
            int h = Math.Min(baseImage.Height, sceneH);

            if (w <= 0 || h <= 0)
                return null;

            // Cores para o alvo baseado no tipo de material
            Color overlayColor = GetMaterialColor(targetMaterialId, materialType);
            Color lowConfidenceColor = Color.FromArgb(
                (int)(overlayColor.R * 0.5 + 128 * 0.5),
                (int)(overlayColor.G * 0.5 + 128 * 0.5),
                (int)(overlayColor.B * 0.5 + 128 * 0.5));

            var validPixels = BuildValidPixelMask(result.Labels, targetMaterialId, w, h, confidenceThreshold);
            var resultBitmap = new Bitmap(w, h);

            int totalTargetPixels = 0;
            int highConfPixels = 0;
            int lowConfPixels = 0;
            int totalSamplePixels = 0;

            double overlayAlphaHigh = 0.85;
            double overlayAlphaLow = 0.45;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var src = baseImage.GetPixel(x, y);

                    // Convert to grayscale for X-ray background
                    int gray = (int)(0.299 * src.R + 0.587 * src.G + 0.114 * src.B);
                    Color grayColor = Color.FromArgb(gray, gray, gray);

                    PixelLabel? lbl = null;
                    if (x < result.Labels.GetLength(0) && y < result.Labels.GetLength(1))
                    {
                        lbl = result.Labels[x, y];
                    }

                    if (lbl == null || !lbl.IsSample)
                    {
                        // Background: darker grayscale
                        int darkGray = (int)(gray * 0.4);
                        resultBitmap.SetPixel(x, y, Color.FromArgb(darkGray, darkGray, darkGray));
                        continue;
                    }

                    totalSamplePixels++;

                    bool isTargetMatch = !string.IsNullOrWhiteSpace(lbl.MaterialId) &&
                                         string.Equals(lbl.MaterialId, targetMaterialId, StringComparison.OrdinalIgnoreCase) &&
                                         lbl.MaterialConfidence >= confidenceThreshold &&
                                         validPixels[x, y];

                    if (isTargetMatch)
                    {
                        totalTargetPixels++;

                        // Determine high vs low confidence based on confidence gap
                        bool isHighConfidence = IsHighConfidencePixel(lbl, LowConfidenceGapThreshold);

                        if (isHighConfidence)
                        {
                            highConfPixels++;
                            // High confidence: strong overlay color
                            int r = (int)(gray * (1 - overlayAlphaHigh) + overlayColor.R * overlayAlphaHigh);
                            int g = (int)(gray * (1 - overlayAlphaHigh) + overlayColor.G * overlayAlphaHigh);
                            int b = (int)(gray * (1 - overlayAlphaHigh) + overlayColor.B * overlayAlphaHigh);
                            resultBitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                        }
                        else
                        {
                            lowConfPixels++;
                            if (showUncertainty)
                            {
                                // Low confidence: lighter/translucent overlay
                                int r = (int)(gray * (1 - overlayAlphaLow) + lowConfidenceColor.R * overlayAlphaLow);
                                int g = (int)(gray * (1 - overlayAlphaLow) + lowConfidenceColor.G * overlayAlphaLow);
                                int b = (int)(gray * (1 - overlayAlphaLow) + lowConfidenceColor.B * overlayAlphaLow);
                                resultBitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                            }
                            else
                            {
                                // Without uncertainty visualization, treat as high confidence
                                int r = (int)(gray * (1 - overlayAlphaHigh) + overlayColor.R * overlayAlphaHigh);
                                int g = (int)(gray * (1 - overlayAlphaHigh) + overlayColor.G * overlayAlphaHigh);
                                int b = (int)(gray * (1 - overlayAlphaHigh) + overlayColor.B * overlayAlphaHigh);
                                resultBitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                            }
                        }
                    }
                    else
                    {
                        // Non-target sample: grayscale
                        resultBitmap.SetPixel(x, y, grayColor);
                    }
                }
            }

            // Build confidence result
            confidenceResult = new SelectiveConfidenceResult
            {
                TotalTargetPixels = totalTargetPixels,
                HighConfidencePixels = highConfPixels,
                LowConfidencePixels = lowConfPixels,
                TargetFractionOfSample = totalSamplePixels > 0 ? (double)totalTargetPixels / totalSamplePixels : 0,
                PpmEstimated = totalSamplePixels > 0 ? ((double)totalTargetPixels / totalSamplePixels) * 1_000_000 : 0,
                ParticleCount = CountParticlesForMaterial(result.Summary, targetMaterialId)
            };

            return resultBitmap;
        }

        /// <summary>
        /// PR8: Gera visualização seletiva combinada Au+PGM.
        /// Ouro em dourado, PGM em roxo/lilás.
        /// </summary>
        public static Bitmap? BuildSelectiveAuPgmView(
            Bitmap baseImage,
            FullSceneAnalysis result,
            bool xrayMode,
            out AuPgmCombinedResult? combinedResult,
            double confidenceThreshold = 0.5,
            bool showUncertainty = false)
        {
            combinedResult = null;

            if (baseImage == null || result == null || result.Labels == null)
                return null;

            int sceneW = result.Width;
            int sceneH = result.Height;

            if (sceneW <= 0 || sceneH <= 0)
                return null;

            int w = Math.Min(baseImage.Width, sceneW);
            int h = Math.Min(baseImage.Height, sceneH);

            if (w <= 0 || h <= 0)
                return null;

            // Cores fixas para Au e PGM
            Color auColor = Color.FromArgb(255, 215, 0);     // Dourado
            Color pgmColor = Color.FromArgb(148, 100, 168);  // Roxo/lilás

            // Build valid pixel masks for Au and all PGM metals
            var validAu = BuildValidPixelMask(result.Labels, "Au", w, h, confidenceThreshold);
            var validPgm = new bool[w, h];

            // Merge valid masks for all PGM metals
            foreach (var pgmId in PgmMetals)
            {
                var pgmMask = BuildValidPixelMask(result.Labels, pgmId, w, h, confidenceThreshold);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (pgmMask[x, y]) validPgm[x, y] = true;
            }

            var resultBitmap = new Bitmap(w, h);

            int auPixels = 0, pgmPixels = 0;
            int auHighConf = 0, auLowConf = 0;
            int pgmHighConf = 0, pgmLowConf = 0;
            int totalSamplePixels = 0;

            Color bgOverlay = Color.FromArgb(0, 80, 200);
            double bgAlpha = 0.6;
            double overlayAlphaHigh = xrayMode ? 0.85 : 0.7;
            double overlayAlphaLow = xrayMode ? 0.45 : 0.4;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var src = baseImage.GetPixel(x, y);
                    int gray = (int)(0.299 * src.R + 0.587 * src.G + 0.114 * src.B);

                    PixelLabel? lbl = null;
                    if (x < result.Labels.GetLength(0) && y < result.Labels.GetLength(1))
                    {
                        lbl = result.Labels[x, y];
                    }

                    if (lbl == null || !lbl.IsSample)
                    {
                        if (xrayMode)
                        {
                            int darkGray = (int)(gray * 0.4);
                            resultBitmap.SetPixel(x, y, Color.FromArgb(darkGray, darkGray, darkGray));
                        }
                        else
                        {
                            int rBg = (int)(src.R * (1 - bgAlpha) + bgOverlay.R * bgAlpha);
                            int gBg = (int)(src.G * (1 - bgAlpha) + bgOverlay.G * bgAlpha);
                            int bBg = (int)(src.B * (1 - bgAlpha) + bgOverlay.B * bgAlpha);
                            resultBitmap.SetPixel(x, y, Color.FromArgb(rBg, gBg, bBg));
                        }
                        continue;
                    }

                    totalSamplePixels++;
                    Color baseColor = xrayMode ? Color.FromArgb(gray, gray, gray) : src;

                    // Check if Au
                    bool isAu = string.Equals(lbl.MaterialId, "Au", StringComparison.OrdinalIgnoreCase) &&
                                lbl.MaterialConfidence >= confidenceThreshold && validAu[x, y];

                    // Check if PGM
                    bool isPgm = IsPgmMetal(lbl.MaterialId) &&
                                 lbl.MaterialConfidence >= confidenceThreshold && validPgm[x, y];

                    if (isAu)
                    {
                        auPixels++;
                        bool highConf = IsHighConfidencePixel(lbl, LowConfidenceGapThreshold);
                        if (highConf) auHighConf++; else auLowConf++;

                        double alpha = (showUncertainty && !highConf) ? overlayAlphaLow : overlayAlphaHigh;
                        int r = (int)(baseColor.R * (1 - alpha) + auColor.R * alpha);
                        int g = (int)(baseColor.G * (1 - alpha) + auColor.G * alpha);
                        int b = (int)(baseColor.B * (1 - alpha) + auColor.B * alpha);
                        resultBitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                    }
                    else if (isPgm)
                    {
                        pgmPixels++;
                        bool highConf = IsHighConfidencePixel(lbl, LowConfidenceGapThreshold);
                        if (highConf) pgmHighConf++; else pgmLowConf++;

                        double alpha = (showUncertainty && !highConf) ? overlayAlphaLow : overlayAlphaHigh;
                        int r = (int)(baseColor.R * (1 - alpha) + pgmColor.R * alpha);
                        int g = (int)(baseColor.G * (1 - alpha) + pgmColor.G * alpha);
                        int b = (int)(baseColor.B * (1 - alpha) + pgmColor.B * alpha);
                        resultBitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                    }
                    else
                    {
                        // Non-target: grayscale in X-ray mode, original otherwise
                        resultBitmap.SetPixel(x, y, baseColor);
                    }
                }
            }

            combinedResult = new AuPgmCombinedResult
            {
                AuFraction = totalSamplePixels > 0 ? (double)auPixels / totalSamplePixels : 0,
                PgmFraction = totalSamplePixels > 0 ? (double)pgmPixels / totalSamplePixels : 0,
                AuPpm = totalSamplePixels > 0 ? ((double)auPixels / totalSamplePixels) * 1_000_000 : 0,
                PgmPpm = totalSamplePixels > 0 ? ((double)pgmPixels / totalSamplePixels) * 1_000_000 : 0,
                AuPixels = auPixels,
                PgmPixels = pgmPixels,
                AuParticles = CountParticlesForMaterial(result.Summary, "Au"),
                PgmParticles = CountPgmParticles(result.Summary),
                AuHighConfidence = auHighConf,
                AuLowConfidence = auLowConf,
                PgmHighConfidence = pgmHighConf,
                PgmLowConfidence = pgmLowConf
            };

            return resultBitmap;
        }

        /// <summary>
        /// PR8: Verifica se um pixel tem alta confiança baseado no gap entre scores.
        /// </summary>
        private static bool IsHighConfidencePixel(PixelLabel lbl, double gapThreshold)
        {
            // Se temos probabilidades por material, usamos a diferença entre os dois maiores
            if (lbl.MaterialProbabilities != null && lbl.MaterialProbabilities.Count >= 2)
            {
                var sorted = new List<double>(lbl.MaterialProbabilities.Values);
                sorted.Sort((a, b) => b.CompareTo(a));
                double gap = sorted[0] - sorted[1];
                return gap >= gapThreshold;
            }

            // Fallback: usar confiança direta como proxy
            // Alta confiança se MaterialConfidence > 0.7
            return lbl.MaterialConfidence >= 0.7;
        }

        /// <summary>
        /// PR8: Retorna cor para um material específico.
        /// </summary>
        public static Color GetMaterialColor(string? materialId, int materialType = 0)
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                return materialType switch
                {
                    0 => Color.FromArgb(255, 255, 220, 0),   // Metal: dourado
                    1 => Color.FromArgb(255, 0, 255, 0),    // Crystal: verde
                    2 => Color.FromArgb(255, 255, 0, 255),  // Gem: magenta
                    _ => Color.FromArgb(255, 255, 220, 0)
                };
            }

            // Use PhaseMapColors for consistency
            return PhaseMapColors.GetColorForMaterial(materialId);
        }

        /// <summary>
        /// PR8: Conta partículas de um material específico.
        /// </summary>
        private static int CountParticlesForMaterial(SampleFullAnalysisResult? summary, string materialId)
        {
            if (summary?.Particles == null) return 0;
            int count = 0;
            foreach (var p in summary.Particles)
            {
                if (string.Equals(p.MaterialId, materialId, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// PR8: Conta partículas PGM (todos os metais do grupo).
        /// </summary>
        private static int CountPgmParticles(SampleFullAnalysisResult? summary)
        {
            if (summary?.Particles == null) return 0;
            int count = 0;
            foreach (var p in summary.Particles)
            {
                if (IsPgmMetal(p.MaterialId))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// PR8: Gera resumo seletivo aprimorado (texto profissional).
        /// </summary>
        public static string BuildSelectiveSummary(
            string targetMaterial,
            SelectiveConfidenceResult confResult,
            ImageOrigin origin,
            bool xrayMode = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Resumo seletivo – {targetMaterial} ({GetImageOriginDescription(origin)})");
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine($"Fração na amostra ({targetMaterial}): {confResult.TargetFractionOfSample:P1}");
            sb.AppendLine($"PPM estimado ({targetMaterial}): {confResult.PpmEstimated:F0} ppm");
            sb.AppendLine($"Partículas {targetMaterial}: {confResult.ParticleCount}");
            sb.AppendLine($"Área alta confiança: {confResult.HighConfidencePercent:F0}% da área {targetMaterial}");
            sb.AppendLine($"Área baixa confiança: {confResult.LowConfidencePercent:F0}% da área {targetMaterial} (zona de transição)");
            sb.AppendLine($"Origem: {GetImageOriginDescription(origin)}");
            if (xrayMode)
                sb.AppendLine("Modo: X-ray");
            return sb.ToString();
        }

        /// <summary>
        /// PR8: Gera resumo combinado Au+PGM.
        /// </summary>
        public static string BuildAuPgmSummary(
            AuPgmCombinedResult result,
            ImageOrigin origin,
            bool xrayMode = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Resumo seletivo – Au + PGM ({GetImageOriginDescription(origin)})");
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine($"Fração na amostra: Au {result.AuFraction:P1} | PGM {result.PgmFraction:P1}");
            sb.AppendLine($"PPM estimado: Au {result.AuPpm:F0} ppm | PGM {result.PgmPpm:F0} ppm");
            sb.AppendLine($"Partículas: Au {result.AuParticles} | PGM {result.PgmParticles}");
            sb.AppendLine();
            sb.AppendLine("Detalhamento de confiança:");
            if (result.AuPixels > 0)
            {
                double auHighPct = result.AuPixels > 0 ? (result.AuHighConfidence * 100.0 / result.AuPixels) : 0;
                double auLowPct = result.AuPixels > 0 ? (result.AuLowConfidence * 100.0 / result.AuPixels) : 0;
                sb.AppendLine($"  Au - Alta confiança: {auHighPct:F0}% | Baixa confiança: {auLowPct:F0}%");
            }
            if (result.PgmPixels > 0)
            {
                double pgmHighPct = result.PgmPixels > 0 ? (result.PgmHighConfidence * 100.0 / result.PgmPixels) : 0;
                double pgmLowPct = result.PgmPixels > 0 ? (result.PgmLowConfidence * 100.0 / result.PgmPixels) : 0;
                sb.AppendLine($"  PGM - Alta confiança: {pgmHighPct:F0}% | Baixa confiança: {pgmLowPct:F0}%");
            }
            sb.AppendLine();
            sb.AppendLine($"Origem: {GetImageOriginDescription(origin)}");
            if (xrayMode)
                sb.AppendLine("Modo: X-ray");
            return sb.ToString();
        }
    }
}