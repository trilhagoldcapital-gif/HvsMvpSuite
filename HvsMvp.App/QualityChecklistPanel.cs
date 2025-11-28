using System;
using System.Drawing;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// Panel that displays quality checklist with FocusScore, Mask score, and Clipping status.
    /// </summary>
    public class QualityChecklistPanel : Panel
    {
        private Label _lblFocusStatus = null!;
        private Label _lblMaskStatus = null!;
        private Label _lblExposureStatus = null!;
        private Label _lblOverallStatus = null!;
        private Label _lblTitle = null!;

        // Thresholds
        private const double FocusGoodThreshold = 0.5;
        private const double FocusWarningThreshold = 0.3;
        private const double MaskGoodThreshold = 0.5;
        private const double MaskWarningThreshold = 0.3;
        private const double ExposureGoodThreshold = 70;
        private const double ExposureWarningThreshold = 50;

        public QualityChecklistPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            BackColor = Color.FromArgb(15, 22, 35);
            BorderStyle = BorderStyle.FixedSingle;
            Size = new Size(200, 140);
            Padding = new Padding(8);

            _lblTitle = new Label
            {
                Text = "📋 Checklist de Qualidade",
                Location = new Point(8, 8),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 210, 230)
            };
            Controls.Add(_lblTitle);

            _lblFocusStatus = CreateStatusLabel(32);
            _lblMaskStatus = CreateStatusLabel(54);
            _lblExposureStatus = CreateStatusLabel(76);
            _lblOverallStatus = CreateStatusLabel(102);
            _lblOverallStatus.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            Controls.Add(_lblFocusStatus);
            Controls.Add(_lblMaskStatus);
            Controls.Add(_lblExposureStatus);
            Controls.Add(_lblOverallStatus);

            // Initialize with empty state
            ClearChecklist();
        }

        private Label CreateStatusLabel(int yPosition)
        {
            return new Label
            {
                Location = new Point(8, yPosition),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.Gainsboro
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
            _lblFocusStatus.Text = $"Foco: {focusStatus.Icon} {focusStatus.Text} ({focusPercent:F1})";
            _lblFocusStatus.ForeColor = focusStatus.Color;

            // Mask status (using foreground fraction)
            double maskFraction = diagnostics.ForegroundFraction;
            var maskStatus = GetMaskStatus(maskFraction);
            _lblMaskStatus.Text = $"Máscara: {maskStatus.Icon} {maskStatus.Text} ({maskFraction:P0})";
            _lblMaskStatus.ForeColor = maskStatus.Color;

            // Exposure status
            double exposureScore = diagnostics.ExposureScore;
            var exposureStatus = GetStatus(exposureScore, ExposureGoodThreshold, ExposureWarningThreshold);
            _lblExposureStatus.Text = $"Exposição: {exposureStatus.Icon} {exposureStatus.Text} ({exposureScore:F1})";
            _lblExposureStatus.ForeColor = exposureStatus.Color;

            // Overall status
            var overallStatus = GetOverallStatus(focusStatus, maskStatus, exposureStatus);
            _lblOverallStatus.Text = $"Geral: {overallStatus.Icon} {diagnostics.QualityStatus} ({diagnostics.QualityIndex:F1})";
            _lblOverallStatus.ForeColor = overallStatus.Color;
        }

        /// <summary>
        /// Clear the checklist (no analysis available).
        /// </summary>
        public void ClearChecklist()
        {
            _lblFocusStatus.Text = "Foco: --";
            _lblFocusStatus.ForeColor = Color.Gray;

            _lblMaskStatus.Text = "Máscara: --";
            _lblMaskStatus.ForeColor = Color.Gray;

            _lblExposureStatus.Text = "Exposição: --";
            _lblExposureStatus.ForeColor = Color.Gray;

            _lblOverallStatus.Text = "Geral: Aguardando análise";
            _lblOverallStatus.ForeColor = Color.Gray;
        }

        private (string Icon, string Text, Color Color) GetStatus(double value, double goodThreshold, double warningThreshold)
        {
            if (value >= goodThreshold)
                return ("✅", "OK", Color.FromArgb(100, 200, 100));
            else if (value >= warningThreshold)
                return ("⚠️", "Atenção", Color.FromArgb(220, 180, 80));
            else
                return ("❌", "Ruim", Color.FromArgb(220, 100, 100));
        }

        private (string Icon, string Text, Color Color) GetMaskStatus(double fraction)
        {
            // Good: 30-80% foreground
            // Warning: <30% or >80%
            // Bad: <10% or >95%
            if (fraction >= 0.3 && fraction <= 0.8)
                return ("✅", "OK", Color.FromArgb(100, 200, 100));
            else if (fraction >= 0.1 && fraction <= 0.95)
                return ("⚠️", "Atenção", Color.FromArgb(220, 180, 80));
            else
                return ("❌", "Ruim", Color.FromArgb(220, 100, 100));
        }

        private (string Icon, string Text, Color Color) GetOverallStatus(
            (string Icon, string Text, Color Color) focus,
            (string Icon, string Text, Color Color) mask,
            (string Icon, string Text, Color Color) exposure)
        {
            // Count issues
            int badCount = 0;
            int warningCount = 0;

            if (focus.Text == "Ruim") badCount++;
            else if (focus.Text == "Atenção") warningCount++;

            if (mask.Text == "Ruim") badCount++;
            else if (mask.Text == "Atenção") warningCount++;

            if (exposure.Text == "Ruim") badCount++;
            else if (exposure.Text == "Atenção") warningCount++;

            if (badCount > 0)
                return ("❌", "Problemas", Color.FromArgb(220, 100, 100));
            else if (warningCount > 0)
                return ("⚠️", "Atenções", Color.FromArgb(220, 180, 80));
            else
                return ("✅", "Bom", Color.FromArgb(100, 200, 100));
        }
    }
}
