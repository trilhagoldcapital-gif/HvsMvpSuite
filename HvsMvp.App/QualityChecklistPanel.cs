using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR10: Enhanced quality checklist panel with futuristic styling.
    /// </summary>
    public class QualityChecklistPanel : Panel
    {
        private Label _lblFocusStatus = null!;
        private Label _lblMaskStatus = null!;
        private Label _lblExposureStatus = null!;
        private Label _lblOverallStatus = null!;
        private Label _lblTitle = null!;
        private Label _lblWarnings = null!;

        // Thresholds
        private const double FocusGoodThreshold = 0.5;
        private const double FocusWarningThreshold = 0.3;
        private const double MaskGoodThreshold = 0.5;
        private const double MaskWarningThreshold = 0.3;
        private const double ExposureGoodThreshold = 70;
        private const double ExposureWarningThreshold = 50;

        // PR10: Color palette
        private readonly Color _bgColor = Color.FromArgb(12, 20, 32);
        private readonly Color _borderColor = Color.FromArgb(40, 55, 75);
        private readonly Color _titleColor = Color.FromArgb(200, 160, 60);
        private readonly Color _textColor = Color.FromArgb(200, 210, 225);
        private readonly Color _warningColor = Color.FromArgb(220, 180, 80);
        private readonly Color _goodColor = Color.FromArgb(100, 200, 100);
        private readonly Color _badColor = Color.FromArgb(220, 100, 100);
        private readonly Color _grayColor = Color.FromArgb(120, 130, 150);

        public QualityChecklistPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            BackColor = _bgColor;
            BorderStyle = BorderStyle.None;
            Size = new Size(220, 145);
            Padding = new Padding(10, 6, 10, 6);

            // Custom paint for border
            Paint += (s, e) =>
            {
                using var pen = new Pen(_borderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            _lblTitle = new Label
            {
                Text = "📋 Checklist de Qualidade",
                Location = new Point(10, 8),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = _titleColor
            };
            Controls.Add(_lblTitle);

            _lblFocusStatus = CreateStatusLabel(32);
            _lblMaskStatus = CreateStatusLabel(52);
            _lblExposureStatus = CreateStatusLabel(72);
            _lblOverallStatus = CreateStatusLabel(96);
            _lblOverallStatus.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            _lblWarnings = new Label
            {
                Location = new Point(10, 118),
                Size = new Size(200, 24),
                Font = new Font("Segoe UI", 8),
                ForeColor = _warningColor,
                AutoEllipsis = true
            };

            Controls.Add(_lblFocusStatus);
            Controls.Add(_lblMaskStatus);
            Controls.Add(_lblExposureStatus);
            Controls.Add(_lblOverallStatus);
            Controls.Add(_lblWarnings);

            // Initialize with empty state
            ClearChecklist();
        }

        private Label CreateStatusLabel(int yPosition)
        {
            return new Label
            {
                Location = new Point(10, yPosition),
                Size = new Size(200, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = _textColor
            };
        }

        /// <summary>
        /// Update the checklist with analysis results.
        /// </summary>
        public void UpdateFromDiagnostics(ImageDiagnosticsResult diagnostics)
        {
            if (diagnostics == null)
            {
                ClearChecklist();
                return;
            }

            // Focus status
            double focusPercent = diagnostics.FocusScorePercent;
            var focusStatus = GetStatus(focusPercent, FocusGoodThreshold * 100, FocusWarningThreshold * 100);
            _lblFocusStatus.Text = $"Foco: {focusStatus.Icon} {focusStatus.Text} ({focusPercent:F0}%)";
            _lblFocusStatus.ForeColor = focusStatus.Color;

            // Mask status (using foreground fraction and status)
            double maskFraction = diagnostics.ForegroundFraction;
            var maskStatus = GetMaskStatus(maskFraction, diagnostics.ForegroundFractionStatus);
            _lblMaskStatus.Text = $"Máscara: {maskStatus.Icon} {maskStatus.Text} ({maskFraction:P0})";
            _lblMaskStatus.ForeColor = maskStatus.Color;

            // Exposure status
            double exposureScore = diagnostics.ExposureScore;
            var exposureStatus = GetStatus(exposureScore, ExposureGoodThreshold, ExposureWarningThreshold);
            _lblExposureStatus.Text = $"Exposição: {exposureStatus.Icon} {exposureStatus.Text} ({exposureScore:F0})";
            _lblExposureStatus.ForeColor = exposureStatus.Color;

            // Overall status
            var overallStatus = GetOverallStatus(focusStatus, maskStatus, exposureStatus);
            _lblOverallStatus.Text = $"Geral: {overallStatus.Icon} {diagnostics.QualityStatus} ({diagnostics.QualityIndex:F1})";
            _lblOverallStatus.ForeColor = overallStatus.Color;

            // Warnings
            if (diagnostics.MaskWarnings != null && diagnostics.MaskWarnings.Count > 0)
            {
                _lblWarnings.Text = "⚠️ " + string.Join("; ", diagnostics.MaskWarnings);
                _lblWarnings.Visible = true;
            }
            else
            {
                _lblWarnings.Text = "";
                _lblWarnings.Visible = false;
            }
        }

        /// <summary>
        /// Clear the checklist (no analysis available).
        /// </summary>
        public void ClearChecklist()
        {
            _lblFocusStatus.Text = "Foco: –";
            _lblFocusStatus.ForeColor = _grayColor;

            _lblMaskStatus.Text = "Máscara: –";
            _lblMaskStatus.ForeColor = _grayColor;

            _lblExposureStatus.Text = "Exposição: –";
            _lblExposureStatus.ForeColor = _grayColor;

            _lblOverallStatus.Text = "Geral: Aguardando análise";
            _lblOverallStatus.ForeColor = _grayColor;

            _lblWarnings.Text = "";
            _lblWarnings.Visible = false;
        }

        private (string Icon, string Text, Color Color) GetStatus(double value, double goodThreshold, double warningThreshold)
        {
            if (value >= goodThreshold)
                return ("✅", "OK", _goodColor);
            else if (value >= warningThreshold)
                return ("⚠️", "Atenção", _warningColor);
            else
                return ("❌", "Ruim", _badColor);
        }

        private (string Icon, string Text, Color Color) GetMaskStatus(double fraction, string? fractionStatus)
        {
            // Use the provided status if available
            if (!string.IsNullOrEmpty(fractionStatus))
            {
                if (fractionStatus == "OK")
                    return ("✅", "OK", _goodColor);
                else if (fractionStatus.Contains("Muito"))
                    return ("❌", fractionStatus, _badColor);
                else
                    return ("⚠️", fractionStatus, _warningColor);
            }

            // Fallback to original logic
            // Good: 30-80% foreground
            // Warning: <30% or >80%
            // Bad: <10% or >95%
            if (fraction >= 0.3 && fraction <= 0.8)
                return ("✅", "OK", _goodColor);
            else if (fraction >= 0.1 && fraction <= 0.95)
                return ("⚠️", "Atenção", _warningColor);
            else
                return ("❌", "Ruim", _badColor);
        }

        private (string Icon, string Text, Color Color) GetOverallStatus(
            (string Icon, string Text, Color Color) focus,
            (string Icon, string Text, Color Color) mask,
            (string Icon, string Text, Color Color) exposure)
        {
            // Count issues
            int badCount = 0;
            int warningCount = 0;

            if (focus.Icon == "❌") badCount++;
            else if (focus.Icon == "⚠️") warningCount++;

            if (mask.Icon == "❌") badCount++;
            else if (mask.Icon == "⚠️") warningCount++;

            if (exposure.Icon == "❌") badCount++;
            else if (exposure.Icon == "⚠️") warningCount++;

            if (badCount > 0)
                return ("❌", "Problemas", _badColor);
            else if (warningCount > 0)
                return ("⚠️", "Atenções", _warningColor);
            else
                return ("✅", "Bom", _goodColor);
        }
    }
}
