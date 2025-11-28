using System;
using System.Drawing;

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
        FundoMascarado
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
        public static string GetViewModeDescription(ViewMode mode, string? targetMaterial = null)
        {
            return mode switch
            {
                ViewMode.Original => "Visualização: Imagem original",
                ViewMode.MaskGlobal => "Visualização: Máscara da amostra",
                ViewMode.MapaFases => "Visualização: Mapa de Fases",
                ViewMode.HeatmapAlvo => $"Visualização: Heatmap Alvo{(targetMaterial != null ? $" – {targetMaterial}" : "")}",
                ViewMode.SeletivaAlvo => $"Visualização: Análise seletiva{(targetMaterial != null ? $" – Metal: {targetMaterial}" : "")}",
                ViewMode.FundoMascarado => "Visualização: Fundo mascarado",
                _ => "Visualização: Desconhecido"
            };
        }
    }
}