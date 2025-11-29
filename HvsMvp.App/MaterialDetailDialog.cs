using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR13: Dialog for showing detailed information about a material from the analysis.
    /// Restores the ability to see detailed metal/crystal/gem information.
    /// </summary>
    public class MaterialDetailDialog : Form
    {
        private readonly SampleFullAnalysisResult? _analysis;
        private readonly object? _materialResult;
        private readonly string _materialType;

        // Color palette (consistent with app theme)
        private readonly Color _bgColor = Color.FromArgb(20, 30, 45);
        private readonly Color _cardBg = Color.FromArgb(30, 42, 60);
        private readonly Color _goldAccent = Color.FromArgb(200, 160, 60);
        private readonly Color _textPrimary = Color.FromArgb(230, 235, 245);
        private readonly Color _textSecondary = Color.FromArgb(150, 165, 185);
        private readonly Color _borderColor = Color.FromArgb(50, 70, 100);

        public MaterialDetailDialog(MetalResult metal, SampleFullAnalysisResult? analysis = null)
        {
            _analysis = analysis;
            _materialResult = metal;
            _materialType = "Metal";
            InitializeComponent();
            PopulateMetalDetails(metal);
        }

        public MaterialDetailDialog(CrystalResult crystal, SampleFullAnalysisResult? analysis = null)
        {
            _analysis = analysis;
            _materialResult = crystal;
            _materialType = "Cristal";
            InitializeComponent();
            PopulateCrystalDetails(crystal);
        }

        public MaterialDetailDialog(GemResult gem, SampleFullAnalysisResult? analysis = null)
        {
            _analysis = analysis;
            _materialResult = gem;
            _materialType = "Gema";
            InitializeComponent();
            PopulateGemDetails(gem);
        }

        private void InitializeComponent()
        {
            Text = $"Detalhes do {_materialType}";
            Size = new Size(450, 380);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = _bgColor;
            ForeColor = _textPrimary;

            // Top accent bar
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 3,
                BackColor = _goldAccent
            };
            Controls.Add(topBar);

            // OK button
            var btnOk = new Button
            {
                Text = "OK",
                Location = new Point(340, 300),
                Size = new Size(90, 32),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(45, 70, 100),
                ForeColor = _textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(65, 95, 130);
            Controls.Add(btnOk);
            AcceptButton = btnOk;
        }

        private void PopulateMetalDetails(MetalResult metal)
        {
            int y = 25;

            // Title
            AddTitleLabel($"🔬 {metal.Name ?? metal.Id}", y);
            y += 40;

            // Details card
            var card = CreateDetailCard(15, y, 410, 220);
            Controls.Add(card);

            int cy = 15;

            AddDetailRow(card, "ID:", metal.Id, ref cy);
            AddDetailRow(card, "Nome:", metal.Name ?? "-", ref cy);
            AddDetailRow(card, "Grupo:", metal.Group ?? "-", ref cy);
            AddSeparator(card, ref cy);
            AddDetailRow(card, "% na Amostra:", $"{metal.PctSample:P4}", ref cy);
            AddDetailRow(card, "PPM Estimado:", metal.PpmEstimated.HasValue ? $"{metal.PpmEstimated.Value:F1} ppm" : "-", ref cy);
            AddDetailRow(card, "Score:", $"{metal.Score:F3}", ref cy);

            // Context from analysis
            if (_analysis != null)
            {
                AddSeparator(card, ref cy);
                AddDetailRow(card, "Qualidade:", $"{_analysis.QualityStatus} ({_analysis.QualityIndex:F1})", ref cy);
                
                int particles = CountParticlesForMaterial(_analysis, metal.Id);
                if (particles > 0)
                {
                    AddDetailRow(card, "Partículas:", particles.ToString(), ref cy);
                }
            }
        }

        private void PopulateCrystalDetails(CrystalResult crystal)
        {
            int y = 25;

            // Title
            AddTitleLabel($"💎 {crystal.Name ?? crystal.Id}", y);
            y += 40;

            // Details card
            var card = CreateDetailCard(15, y, 410, 180);
            Controls.Add(card);

            int cy = 15;

            AddDetailRow(card, "ID:", crystal.Id, ref cy);
            AddDetailRow(card, "Nome:", crystal.Name ?? "-", ref cy);
            AddSeparator(card, ref cy);
            AddDetailRow(card, "% na Amostra:", $"{crystal.PctSample:P4}", ref cy);
            AddDetailRow(card, "Score:", $"{crystal.Score:F3}", ref cy);

            if (_analysis != null)
            {
                AddSeparator(card, ref cy);
                AddDetailRow(card, "Qualidade:", $"{_analysis.QualityStatus} ({_analysis.QualityIndex:F1})", ref cy);
            }
        }

        private void PopulateGemDetails(GemResult gem)
        {
            int y = 25;

            // Title
            AddTitleLabel($"💠 {gem.Name ?? gem.Id}", y);
            y += 40;

            // Details card
            var card = CreateDetailCard(15, y, 410, 180);
            Controls.Add(card);

            int cy = 15;

            AddDetailRow(card, "ID:", gem.Id, ref cy);
            AddDetailRow(card, "Nome:", gem.Name ?? "-", ref cy);
            AddSeparator(card, ref cy);
            AddDetailRow(card, "% na Amostra:", $"{gem.PctSample:P4}", ref cy);
            AddDetailRow(card, "Score:", $"{gem.Score:F3}", ref cy);

            if (_analysis != null)
            {
                AddSeparator(card, ref cy);
                AddDetailRow(card, "Qualidade:", $"{_analysis.QualityStatus} ({_analysis.QualityIndex:F1})", ref cy);
            }
        }

        private void AddTitleLabel(string text, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(15, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = _goldAccent
            };
            Controls.Add(lbl);
        }

        private Panel CreateDetailCard(int x, int y, int width, int height)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = _cardBg
            };

            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(_borderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            return panel;
        }

        private void AddDetailRow(Panel parent, string label, string value, ref int y)
        {
            var lblLabel = new Label
            {
                Text = label,
                Location = new Point(15, y),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = _textSecondary
            };
            parent.Controls.Add(lblLabel);

            var lblValue = new Label
            {
                Text = value,
                Location = new Point(120, y),
                Size = new Size(270, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = _textPrimary
            };
            parent.Controls.Add(lblValue);

            y += 25;
        }

        private void AddSeparator(Panel parent, ref int y)
        {
            var sep = new Panel
            {
                Location = new Point(15, y + 5),
                Size = new Size(380, 1),
                BackColor = _borderColor
            };
            parent.Controls.Add(sep);
            y += 15;
        }

        private int CountParticlesForMaterial(SampleFullAnalysisResult analysis, string materialId)
        {
            if (analysis.Particles == null) return 0;
            int count = 0;
            foreach (var p in analysis.Particles)
            {
                if (string.Equals(p.MaterialId, materialId, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }
    }
}
