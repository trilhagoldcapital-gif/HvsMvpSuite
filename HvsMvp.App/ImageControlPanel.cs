using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR16: Image adjustment presets for quick configuration.
    /// </summary>
    public enum ImagePreset
    {
        /// <summary>Default settings with no adjustments.</summary>
        Standard,
        /// <summary>Higher brightness for dim samples.</summary>
        HighBrightness,
        /// <summary>Lower noise with reduced contrast.</summary>
        LowNoise,
        /// <summary>Enhanced contrast for better detail visibility.</summary>
        HighContrast,
        /// <summary>UV-optimized settings with enhanced blue channel.</summary>
        UVMode,
        /// <summary>Custom user-defined settings.</summary>
        Custom
    }

    /// <summary>
    /// PR16: Panel with image adjustment controls for brightness, contrast, gamma, and saturation.
    /// Provides real-time preview and preset configurations.
    /// </summary>
    public class ImageControlPanel : Panel
    {
        // Controls
        private TrackBar _trackBrightness = null!;
        private TrackBar _trackContrast = null!;
        private TrackBar _trackGamma = null!;
        private TrackBar _trackSaturation = null!;
        private Label _lblBrightnessValue = null!;
        private Label _lblContrastValue = null!;
        private Label _lblGammaValue = null!;
        private Label _lblSaturationValue = null!;
        private ComboBox _cboPreset = null!;
        private Button _btnReset = null!;
        private Button _btnApply = null!;
        private CheckBox _chkAutoApply = null!;

        // Current values (normalized)
        private double _brightness = 0;     // -1.0 to +1.0
        private double _contrast = 0;       // -1.0 to +1.0
        private double _gamma = 1.0;        // 0.1 to 3.0
        private double _saturation = 0;     // -1.0 to +1.0

        // Events
        public event EventHandler<ImageAdjustmentEventArgs>? AdjustmentChanged;
        public event EventHandler? ApplyRequested;

        // Colors
        private readonly Color _bgColor = Color.FromArgb(12, 22, 38);
        private readonly Color _labelColor = Color.FromArgb(180, 190, 210);
        private readonly Color _valueColor = Color.FromArgb(220, 230, 245);
        private readonly Color _accentColor = Color.FromArgb(200, 160, 60);

        public ImageControlPanel()
        {
            InitializeLayout();
            ApplyPreset(ImagePreset.Standard);
        }

        /// <summary>Current brightness adjustment (-1.0 to +1.0).</summary>
        public double Brightness => _brightness;

        /// <summary>Current contrast adjustment (-1.0 to +1.0).</summary>
        public double Contrast => _contrast;

        /// <summary>Current gamma adjustment (0.1 to 3.0).</summary>
        public double Gamma => _gamma;

        /// <summary>Current saturation adjustment (-1.0 to +1.0).</summary>
        public double Saturation => _saturation;

        /// <summary>Whether to auto-apply changes.</summary>
        public bool AutoApply => _chkAutoApply?.Checked ?? false;

        /// <summary>Current preset.</summary>
        public ImagePreset CurrentPreset { get; private set; } = ImagePreset.Standard;

        private void InitializeLayout()
        {
            BackColor = _bgColor;
            Padding = new Padding(8);
            AutoScroll = true;

            // Title
            var lblTitle = new Label
            {
                Text = "🎨 Controles de Imagem",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = _accentColor,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(lblTitle);

            // Main container
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 6,
                Padding = new Padding(0, 8, 0, 0)
            };
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));

            // Row 0: Preset selector
            var lblPreset = CreateLabel("Preset:");
            container.Controls.Add(lblPreset, 0, 0);

            _cboPreset = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(25, 35, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 2, 0, 4)
            };
            _cboPreset.Items.AddRange(new object[] {
                "Padrão",
                "Alto Brilho",
                "Baixo Ruído",
                "Alto Contraste",
                "Modo UV",
                "Personalizado"
            });
            _cboPreset.SelectedIndex = 0;
            _cboPreset.SelectedIndexChanged += CboPreset_SelectedIndexChanged;
            container.Controls.Add(_cboPreset, 1, 0);
            container.SetColumnSpan(_cboPreset, 2);

            // Row 1: Brightness
            container.Controls.Add(CreateLabel("Brilho:"), 0, 1);
            _trackBrightness = CreateTrackBar(-100, 100, 0);
            _trackBrightness.ValueChanged += TrackBrightness_ValueChanged;
            container.Controls.Add(_trackBrightness, 1, 1);
            _lblBrightnessValue = CreateValueLabel("0");
            container.Controls.Add(_lblBrightnessValue, 2, 1);

            // Row 2: Contrast
            container.Controls.Add(CreateLabel("Contraste:"), 0, 2);
            _trackContrast = CreateTrackBar(-100, 100, 0);
            _trackContrast.ValueChanged += TrackContrast_ValueChanged;
            container.Controls.Add(_trackContrast, 1, 2);
            _lblContrastValue = CreateValueLabel("0");
            container.Controls.Add(_lblContrastValue, 2, 2);

            // Row 3: Gamma
            container.Controls.Add(CreateLabel("Gamma:"), 0, 3);
            _trackGamma = CreateTrackBar(10, 300, 100);
            _trackGamma.ValueChanged += TrackGamma_ValueChanged;
            container.Controls.Add(_trackGamma, 1, 3);
            _lblGammaValue = CreateValueLabel("1.00");
            container.Controls.Add(_lblGammaValue, 2, 3);

            // Row 4: Saturation
            container.Controls.Add(CreateLabel("Saturação:"), 0, 4);
            _trackSaturation = CreateTrackBar(-100, 100, 0);
            _trackSaturation.ValueChanged += TrackSaturation_ValueChanged;
            container.Controls.Add(_trackSaturation, 1, 4);
            _lblSaturationValue = CreateValueLabel("0");
            container.Controls.Add(_lblSaturationValue, 2, 4);

            // Row 5: Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0)
            };

            _chkAutoApply = new CheckBox
            {
                Text = "Auto",
                AutoSize = true,
                Checked = true,
                ForeColor = _labelColor,
                Font = new Font("Segoe UI", 8),
                Margin = new Padding(0, 4, 8, 0)
            };
            buttonPanel.Controls.Add(_chkAutoApply);

            _btnReset = CreateButton("↺ Reset");
            _btnReset.Click += BtnReset_Click;
            buttonPanel.Controls.Add(_btnReset);

            _btnApply = CreateButton("✓ Aplicar");
            _btnApply.Click += BtnApply_Click;
            buttonPanel.Controls.Add(_btnApply);

            container.Controls.Add(buttonPanel, 0, 5);
            container.SetColumnSpan(buttonPanel, 3);

            Controls.Add(container);
            container.BringToFront();
        }

        private Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = _labelColor,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 6, 4, 0)
            };
        }

        private Label CreateValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = _valueColor,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(4, 6, 0, 0)
            };
        }

        private TrackBar CreateTrackBar(int min, int max, int value)
        {
            return new TrackBar
            {
                Dock = DockStyle.Fill,
                Minimum = min,
                Maximum = max,
                Value = value,
                TickFrequency = (max - min) / 10,
                SmallChange = 1,
                LargeChange = 10,
                Height = 28,
                Margin = new Padding(0, 0, 0, 2)
            };
        }

        private Button CreateButton(string text)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(70, 26),
                BackColor = Color.FromArgb(30, 50, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8),
                Margin = new Padding(0, 0, 6, 0)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(50, 70, 100);
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private void CboPreset_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var preset = (ImagePreset)_cboPreset.SelectedIndex;
            ApplyPreset(preset);
        }

        public void ApplyPreset(ImagePreset preset)
        {
            CurrentPreset = preset;

            switch (preset)
            {
                case ImagePreset.Standard:
                    SetValues(0, 0, 1.0, 0);
                    break;
                case ImagePreset.HighBrightness:
                    SetValues(0.25, 0.1, 1.1, 0);
                    break;
                case ImagePreset.LowNoise:
                    SetValues(0.05, -0.15, 0.95, -0.1);
                    break;
                case ImagePreset.HighContrast:
                    SetValues(0, 0.35, 1.15, 0.1);
                    break;
                case ImagePreset.UVMode:
                    SetValues(0.15, 0.2, 1.2, 0.15);
                    break;
                case ImagePreset.Custom:
                    // Don't change values for custom
                    break;
            }

            UpdateTrackBarsFromValues();
            OnAdjustmentChanged();
        }

        private void SetValues(double brightness, double contrast, double gamma, double saturation)
        {
            _brightness = Math.Clamp(brightness, -1.0, 1.0);
            _contrast = Math.Clamp(contrast, -1.0, 1.0);
            _gamma = Math.Clamp(gamma, 0.1, 3.0);
            _saturation = Math.Clamp(saturation, -1.0, 1.0);
        }

        private void UpdateTrackBarsFromValues()
        {
            _trackBrightness.ValueChanged -= TrackBrightness_ValueChanged;
            _trackContrast.ValueChanged -= TrackContrast_ValueChanged;
            _trackGamma.ValueChanged -= TrackGamma_ValueChanged;
            _trackSaturation.ValueChanged -= TrackSaturation_ValueChanged;

            _trackBrightness.Value = (int)(_brightness * 100);
            _trackContrast.Value = (int)(_contrast * 100);
            _trackGamma.Value = (int)(_gamma * 100);
            _trackSaturation.Value = (int)(_saturation * 100);

            UpdateValueLabels();

            _trackBrightness.ValueChanged += TrackBrightness_ValueChanged;
            _trackContrast.ValueChanged += TrackContrast_ValueChanged;
            _trackGamma.ValueChanged += TrackGamma_ValueChanged;
            _trackSaturation.ValueChanged += TrackSaturation_ValueChanged;
        }

        private void UpdateValueLabels()
        {
            _lblBrightnessValue.Text = _trackBrightness.Value.ToString();
            _lblContrastValue.Text = _trackContrast.Value.ToString();
            _lblGammaValue.Text = (_trackGamma.Value / 100.0).ToString("F2");
            _lblSaturationValue.Text = _trackSaturation.Value.ToString();
        }

        private void TrackBrightness_ValueChanged(object? sender, EventArgs e)
        {
            _brightness = _trackBrightness.Value / 100.0;
            _lblBrightnessValue.Text = _trackBrightness.Value.ToString();
            MarkAsCustom();
            OnAdjustmentChanged();
        }

        private void TrackContrast_ValueChanged(object? sender, EventArgs e)
        {
            _contrast = _trackContrast.Value / 100.0;
            _lblContrastValue.Text = _trackContrast.Value.ToString();
            MarkAsCustom();
            OnAdjustmentChanged();
        }

        private void TrackGamma_ValueChanged(object? sender, EventArgs e)
        {
            _gamma = _trackGamma.Value / 100.0;
            _lblGammaValue.Text = _gamma.ToString("F2");
            MarkAsCustom();
            OnAdjustmentChanged();
        }

        private void TrackSaturation_ValueChanged(object? sender, EventArgs e)
        {
            _saturation = _trackSaturation.Value / 100.0;
            _lblSaturationValue.Text = _trackSaturation.Value.ToString();
            MarkAsCustom();
            OnAdjustmentChanged();
        }

        private void MarkAsCustom()
        {
            if (CurrentPreset != ImagePreset.Custom)
            {
                CurrentPreset = ImagePreset.Custom;
                _cboPreset.SelectedIndexChanged -= CboPreset_SelectedIndexChanged;
                _cboPreset.SelectedIndex = (int)ImagePreset.Custom;
                _cboPreset.SelectedIndexChanged += CboPreset_SelectedIndexChanged;
            }
        }

        private void BtnReset_Click(object? sender, EventArgs e)
        {
            ApplyPreset(ImagePreset.Standard);
            _cboPreset.SelectedIndex = 0;
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            ApplyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnAdjustmentChanged()
        {
            if (_chkAutoApply?.Checked == true)
            {
                AdjustmentChanged?.Invoke(this, new ImageAdjustmentEventArgs
                {
                    Brightness = _brightness,
                    Contrast = _contrast,
                    Gamma = _gamma,
                    Saturation = _saturation,
                    Preset = CurrentPreset
                });
            }
        }

        /// <summary>
        /// Apply current adjustments to an image.
        /// </summary>
        public Bitmap ApplyAdjustments(Bitmap source)
        {
            return ImageAdjustmentService.Apply(source, _brightness, _contrast, _gamma, _saturation);
        }
    }

    /// <summary>
    /// PR16: Event args for image adjustment changes.
    /// </summary>
    public class ImageAdjustmentEventArgs : EventArgs
    {
        public double Brightness { get; set; }
        public double Contrast { get; set; }
        public double Gamma { get; set; }
        public double Saturation { get; set; }
        public ImagePreset Preset { get; set; }
    }

    /// <summary>
    /// PR16: Service for applying image adjustments (brightness, contrast, gamma, saturation).
    /// </summary>
    public static class ImageAdjustmentService
    {
        /// <summary>
        /// Apply brightness, contrast, gamma, and saturation adjustments to an image.
        /// </summary>
        public static Bitmap Apply(Bitmap source, double brightness, double contrast, double gamma, double saturation)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

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

                // Pre-compute gamma lookup table
                byte[] gammaLut = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    gammaLut[i] = (byte)Math.Clamp((int)(255 * Math.Pow(i / 255.0, 1.0 / gamma)), 0, 255);
                }

                // Pre-compute brightness/contrast factors
                double brightFactor = brightness * 255;
                double contrastFactor = 1.0 + contrast;
                double contrastOffset = 128 * (1 - contrastFactor);

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

                        // Apply brightness and contrast
                        double rD = r * contrastFactor + contrastOffset + brightFactor;
                        double gD = g * contrastFactor + contrastOffset + brightFactor;
                        double bD = b * contrastFactor + contrastOffset + brightFactor;

                        // Apply saturation if needed
                        if (Math.Abs(saturation) > 0.01)
                        {
                            double gray = 0.299 * rD + 0.587 * gD + 0.114 * bD;
                            double satFactor = 1.0 + saturation;
                            rD = gray + satFactor * (rD - gray);
                            gD = gray + satFactor * (gD - gray);
                            bD = gray + satFactor * (bD - gray);
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
    }
}
