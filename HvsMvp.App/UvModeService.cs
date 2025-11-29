using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace HvsMvp.App
{
    /// <summary>
    /// PR16: UV mode types.
    /// </summary>
    public enum UvModeType
    {
        /// <summary>Normal visible light mode.</summary>
        Normal,
        /// <summary>UV-A (365-400nm) fluorescence mode.</summary>
        UvA,
        /// <summary>UV-B (280-315nm) mode.</summary>
        UvB,
        /// <summary>UV-C (200-280nm) mode (requires special equipment).</summary>
        UvC,
        /// <summary>Simulated UV mode (applies UV-like visualization to normal images).</summary>
        Simulated
    }

    /// <summary>
    /// PR16: Settings for UV mode visualization.
    /// </summary>
    public class UvModeSettings
    {
        /// <summary>Current UV mode.</summary>
        public UvModeType Mode { get; set; } = UvModeType.Normal;

        /// <summary>Whether UV mode is active.</summary>
        public bool IsActive => Mode != UvModeType.Normal;

        /// <summary>Enhance blue channel for UV visualization.</summary>
        public double BlueEnhancement { get; set; } = 1.3;

        /// <summary>Enhance contrast for UV visualization.</summary>
        public double ContrastBoost { get; set; } = 1.2;

        /// <summary>Apply false color mapping.</summary>
        public bool FalseColorEnabled { get; set; } = false;

        /// <summary>Gamma correction for UV.</summary>
        public double Gamma { get; set; } = 1.1;
    }

    /// <summary>
    /// PR16: Service for UV mode support and visualization.
    /// Handles UV-specific image processing and analysis adjustments.
    /// </summary>
    public class UvModeService
    {
        private UvModeSettings _settings = new UvModeSettings();

        /// <summary>Current UV mode settings.</summary>
        public UvModeSettings Settings => _settings;

        /// <summary>Whether UV mode is currently active.</summary>
        public bool IsUvModeActive => _settings.IsActive;

        /// <summary>Current UV mode type.</summary>
        public UvModeType CurrentMode => _settings.Mode;

        /// <summary>Event raised when UV mode changes.</summary>
        public event EventHandler? ModeChanged;

        /// <summary>
        /// Set the UV mode.
        /// </summary>
        public void SetMode(UvModeType mode)
        {
            if (_settings.Mode != mode)
            {
                _settings.Mode = mode;

                // Apply mode-specific presets
                switch (mode)
                {
                    case UvModeType.Normal:
                        _settings.BlueEnhancement = 1.0;
                        _settings.ContrastBoost = 1.0;
                        _settings.FalseColorEnabled = false;
                        _settings.Gamma = 1.0;
                        break;

                    case UvModeType.UvA:
                        _settings.BlueEnhancement = 1.4;
                        _settings.ContrastBoost = 1.25;
                        _settings.FalseColorEnabled = false;
                        _settings.Gamma = 1.15;
                        break;

                    case UvModeType.UvB:
                        _settings.BlueEnhancement = 1.5;
                        _settings.ContrastBoost = 1.3;
                        _settings.FalseColorEnabled = true;
                        _settings.Gamma = 1.2;
                        break;

                    case UvModeType.Simulated:
                        _settings.BlueEnhancement = 1.6;
                        _settings.ContrastBoost = 1.35;
                        _settings.FalseColorEnabled = true;
                        _settings.Gamma = 1.25;
                        break;
                }

                ModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Toggle UV mode on/off.
        /// </summary>
        public void ToggleUvMode()
        {
            if (_settings.Mode == UvModeType.Normal)
                SetMode(UvModeType.Simulated);
            else
                SetMode(UvModeType.Normal);
        }

        /// <summary>
        /// Apply UV visualization to an image.
        /// </summary>
        public Bitmap ApplyUvVisualization(Bitmap source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!_settings.IsActive) return (Bitmap)source.Clone();

            int w = source.Width;
            int h = source.Height;
            var result = new Bitmap(w, h, PixelFormat.Format24bppRgb);

            var rect = new Rectangle(0, 0, w, h);
            var srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            try
            {
                int stride = srcData.Stride;
                int bytes = stride * h;
                byte[] srcBuf = new byte[bytes];
                byte[] dstBuf = new byte[bytes];

                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcBuf, 0, bytes);

                // Pre-compute gamma LUT
                double gamma = _settings.Gamma;
                byte[] gammaLut = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    gammaLut[i] = (byte)Math.Clamp((int)(255 * Math.Pow(i / 255.0, 1.0 / gamma)), 0, 255);
                }

                double blueEnh = _settings.BlueEnhancement;
                double contrast = _settings.ContrastBoost;
                double contrastOffset = 128 * (1 - contrast);
                bool falseColor = _settings.FalseColorEnabled;

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;
                        byte b = srcBuf[off + 0];
                        byte g = srcBuf[off + 1];
                        byte r = srcBuf[off + 2];

                        // Apply gamma
                        r = gammaLut[r];
                        g = gammaLut[g];
                        b = gammaLut[b];

                        // Enhance blue channel for UV
                        double rD = r * contrast + contrastOffset;
                        double gD = g * contrast + contrastOffset;
                        double bD = b * blueEnh * contrast + contrastOffset;

                        if (falseColor)
                        {
                            // False color mapping: emphasize UV fluorescence
                            // Blue-heavy areas become cyan/white, others become darker
                            double intensity = (rD + gD + bD) / 3.0;
                            double blueRatio = bD / Math.Max(1, intensity);

                            if (blueRatio > 1.2)
                            {
                                // High UV response - show as cyan/white
                                double boost = Math.Min(1.5, blueRatio);
                                rD = intensity * 0.6 * boost;
                                gD = intensity * 1.0 * boost;
                                bD = intensity * 1.2 * boost;
                            }
                            else if (blueRatio > 0.8)
                            {
                                // Medium UV response - show as purple
                                rD = intensity * 0.7;
                                gD = intensity * 0.4;
                                bD = intensity * 1.1;
                            }
                            else
                            {
                                // Low UV response - darken
                                rD *= 0.6;
                                gD *= 0.6;
                                bD *= 0.8;
                            }
                        }

                        dstBuf[off + 0] = (byte)Math.Clamp((int)bD, 0, 255);
                        dstBuf[off + 1] = (byte)Math.Clamp((int)gD, 0, 255);
                        dstBuf[off + 2] = (byte)Math.Clamp((int)rD, 0, 255);
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(dstBuf, 0, dstData.Scan0, bytes);
            }
            finally
            {
                source.UnlockBits(srcData);
                result.UnlockBits(dstData);
            }

            return result;
        }

        /// <summary>
        /// Get UV mode description for display.
        /// </summary>
        public string GetModeDescription()
        {
            return _settings.Mode switch
            {
                UvModeType.Normal => "Luz visível (normal)",
                UvModeType.UvA => "UV-A (365-400nm)",
                UvModeType.UvB => "UV-B (280-315nm)",
                UvModeType.UvC => "UV-C (200-280nm)",
                UvModeType.Simulated => "UV Simulado",
                _ => "Desconhecido"
            };
        }

        /// <summary>
        /// Get UV mode status text for UI.
        /// </summary>
        public string GetStatusText()
        {
            if (!_settings.IsActive)
                return "UV: Desativado";

            return $"UV: {GetModeDescription()}";
        }

        /// <summary>
        /// Check if hardware UV support is available.
        /// This is a stub - actual implementation would check camera capabilities.
        /// </summary>
        public bool IsHardwareUvSupported()
        {
            // TODO: Check actual camera/hardware capabilities
            // For now, return false (use simulated mode)
            return false;
        }

        /// <summary>
        /// Get recommended UV mode based on hardware support.
        /// </summary>
        public UvModeType GetRecommendedMode()
        {
            if (IsHardwareUvSupported())
                return UvModeType.UvA;
            else
                return UvModeType.Simulated;
        }
    }

    /// <summary>
    /// PR16: Session info extension for UV mode logging.
    /// </summary>
    public class UvSessionInfo
    {
        /// <summary>Whether UV mode was active during the session.</summary>
        public bool UvModeActive { get; set; }

        /// <summary>UV mode type used.</summary>
        public UvModeType UvMode { get; set; }

        /// <summary>Whether hardware UV was used (vs simulated).</summary>
        public bool HardwareUv { get; set; }

        /// <summary>UV exposure settings if applicable.</summary>
        public string? UvExposureNotes { get; set; }

        /// <summary>Timestamp when UV mode was activated.</summary>
        public DateTime? UvActivatedAt { get; set; }

        /// <summary>
        /// Convert to log-friendly string.
        /// </summary>
        public override string ToString()
        {
            if (!UvModeActive)
                return "UV: Não utilizado";

            string modeStr = UvMode switch
            {
                UvModeType.UvA => "UV-A",
                UvModeType.UvB => "UV-B",
                UvModeType.UvC => "UV-C",
                UvModeType.Simulated => "Simulado",
                _ => "?"
            };

            string hwStr = HardwareUv ? "Hardware" : "Software";
            return $"UV: {modeStr} ({hwStr}), Ativado: {UvActivatedAt:HH:mm:ss}";
        }
    }
}
