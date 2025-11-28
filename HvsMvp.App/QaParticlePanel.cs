using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// QA Panel for reviewing and labeling particles.
    /// </summary>
    public class QaParticlePanel : Form
    {
        private readonly FullSceneAnalysis _scene;
        private readonly Bitmap _sourceImage;
        private readonly AppSettings _settings;

        private List<ParticleRecord> _filteredParticles = new List<ParticleRecord>();
        private int _currentIndex = -1;

        // Controls
        private ListBox _listParticles = null!;
        private PictureBox _picParticle = null!;
        private Label _lblInfo = null!;
        private ComboBox _cbMaterialFilter = null!;
        private NumericUpDown _nudMinArea = null!;
        private NumericUpDown _nudMinConfidence = null!;
        private NumericUpDown _nudMaxConfidence = null!;
        private ComboBox _cbNewLabel = null!;
        private Button _btnApplyLabel = null!;
        private Button _btnMarkNoise = null!;
        private Button _btnKeepPredicted = null!;
        private Button _btnSaveQa = null!;
        private Button _btnExportQa = null!;
        private TextBox _txtNotes = null!;
        private Label _lblStats = null!;

        // QA labels storage
        private Dictionary<Guid, QaLabel> _qaLabels = new Dictionary<Guid, QaLabel>();
        private string _qaFilePath;

        public QaParticlePanel(FullSceneAnalysis scene, Bitmap sourceImage, AppSettings settings)
        {
            _scene = scene;
            _sourceImage = sourceImage;
            _settings = settings;
            _qaFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "exports", "dataset-ia",
                $"qa_labels_{scene.Summary.Id:N}.csv");

            InitializeComponent();
            LoadExistingQaLabels();
            ApplyFilter();
        }

        private void InitializeComponent()
        {
            Text = "QA de Partículas - Rotulação Manual";
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(25, 30, 40);
            ForeColor = Color.WhiteSmoke;

            // Left panel - filters and list
            var leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 350,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(20, 25, 35)
            };
            Controls.Add(leftPanel);

            // Filter controls
            int y = 10;

            var lblFilter = new Label
            {
                Text = "🔍 Filtros",
                Location = new Point(10, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 180, 100)
            };
            leftPanel.Controls.Add(lblFilter);
            y += 30;

            AddLabel(leftPanel, "Material:", 10, y);
            _cbMaterialFilter = new ComboBox
            {
                Location = new Point(120, y - 3),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 45, 55),
                ForeColor = Color.WhiteSmoke
            };
            _cbMaterialFilter.Items.Add("(Todos)");
            var materials = _scene.Summary.Particles
                .Select(p => p.MaterialId ?? "Unknown")
                .Distinct()
                .OrderBy(m => m);
            foreach (var m in materials)
                _cbMaterialFilter.Items.Add(m);
            _cbMaterialFilter.SelectedIndex = 0;
            _cbMaterialFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            leftPanel.Controls.Add(_cbMaterialFilter);
            y += 35;

            AddLabel(leftPanel, "Área mínima:", 10, y);
            _nudMinArea = new NumericUpDown
            {
                Location = new Point(120, y - 3),
                Size = new Size(80, 25),
                Minimum = 0,
                Maximum = 100000,
                Value = 50,
                BackColor = Color.FromArgb(40, 45, 55),
                ForeColor = Color.WhiteSmoke
            };
            _nudMinArea.ValueChanged += (s, e) => ApplyFilter();
            leftPanel.Controls.Add(_nudMinArea);
            y += 35;

            AddLabel(leftPanel, "Confiança min:", 10, y);
            _nudMinConfidence = new NumericUpDown
            {
                Location = new Point(120, y - 3),
                Size = new Size(80, 25),
                DecimalPlaces = 2,
                Increment = 0.05M,
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                BackColor = Color.FromArgb(40, 45, 55),
                ForeColor = Color.WhiteSmoke
            };
            _nudMinConfidence.ValueChanged += (s, e) => ApplyFilter();
            leftPanel.Controls.Add(_nudMinConfidence);
            y += 35;

            AddLabel(leftPanel, "Confiança max:", 10, y);
            _nudMaxConfidence = new NumericUpDown
            {
                Location = new Point(120, y - 3),
                Size = new Size(80, 25),
                DecimalPlaces = 2,
                Increment = 0.05M,
                Minimum = 0,
                Maximum = 1,
                Value = 1,
                BackColor = Color.FromArgb(40, 45, 55),
                ForeColor = Color.WhiteSmoke
            };
            _nudMaxConfidence.ValueChanged += (s, e) => ApplyFilter();
            leftPanel.Controls.Add(_nudMaxConfidence);
            y += 45;

            // Stats
            _lblStats = new Label
            {
                Location = new Point(10, y),
                Size = new Size(320, 40),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 180, 220)
            };
            leftPanel.Controls.Add(_lblStats);
            y += 50;

            // Particle list
            var lblList = new Label
            {
                Text = "📋 Partículas",
                Location = new Point(10, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 180, 100)
            };
            leftPanel.Controls.Add(lblList);
            y += 25;

            _listParticles = new ListBox
            {
                Location = new Point(10, y),
                Size = new Size(320, 350),
                BackColor = Color.FromArgb(30, 35, 45),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle
            };
            _listParticles.SelectedIndexChanged += ListParticles_SelectedIndexChanged;
            leftPanel.Controls.Add(_listParticles);

            // Right panel - particle view and labeling
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            Controls.Add(rightPanel);

            // Particle image
            _picParticle = new PictureBox
            {
                Location = new Point(10, 10),
                Size = new Size(256, 256),
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            rightPanel.Controls.Add(_picParticle);

            // Particle info
            _lblInfo = new Label
            {
                Location = new Point(280, 10),
                Size = new Size(320, 256),
                Font = new Font("Consolas", 9),
                ForeColor = Color.Gainsboro,
                BackColor = Color.FromArgb(20, 25, 35)
            };
            rightPanel.Controls.Add(_lblInfo);

            // Labeling controls
            int ly = 280;

            var lblLabeling = new Label
            {
                Text = "🏷️ Rotulação",
                Location = new Point(10, ly),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 180, 100)
            };
            rightPanel.Controls.Add(lblLabeling);
            ly += 30;

            AddLabel(rightPanel, "Novo rótulo:", 10, ly);
            _cbNewLabel = new ComboBox
            {
                Location = new Point(120, ly - 3),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 45, 55),
                ForeColor = Color.WhiteSmoke
            };
            foreach (var label in TrainingModeService.AvailableLabels)
                _cbNewLabel.Items.Add(label);
            _cbNewLabel.SelectedIndex = 0;
            rightPanel.Controls.Add(_cbNewLabel);

            _btnApplyLabel = CreateButton("✅ Aplicar", 280, ly - 3, 100);
            _btnApplyLabel.Click += BtnApplyLabel_Click;
            rightPanel.Controls.Add(_btnApplyLabel);

            _btnMarkNoise = CreateButton("🚫 Ruído", 390, ly - 3, 80);
            _btnMarkNoise.Click += BtnMarkNoise_Click;
            rightPanel.Controls.Add(_btnMarkNoise);

            _btnKeepPredicted = CreateButton("↩️ Manter", 480, ly - 3, 80);
            _btnKeepPredicted.Click += BtnKeepPredicted_Click;
            rightPanel.Controls.Add(_btnKeepPredicted);
            ly += 40;

            AddLabel(rightPanel, "Notas:", 10, ly);
            _txtNotes = new TextBox
            {
                Location = new Point(120, ly - 3),
                Size = new Size(440, 60),
                Multiline = true,
                BackColor = Color.FromArgb(40, 45, 55),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle
            };
            rightPanel.Controls.Add(_txtNotes);
            ly += 80;

            // Action buttons
            _btnSaveQa = CreateButton("💾 Salvar QA", 10, ly, 120);
            _btnSaveQa.BackColor = Color.FromArgb(40, 100, 60);
            _btnSaveQa.Click += BtnSaveQa_Click;
            rightPanel.Controls.Add(_btnSaveQa);

            _btnExportQa = CreateButton("📤 Exportar CSV", 140, ly, 120);
            _btnExportQa.Click += BtnExportQa_Click;
            rightPanel.Controls.Add(_btnExportQa);

            var btnClose = CreateButton("Fechar", 500, ly, 80);
            btnClose.Click += (s, e) => Close();
            rightPanel.Controls.Add(btnClose);
        }

        private Label AddLabel(Control parent, string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                ForeColor = Color.Gainsboro
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private Button CreateButton(string text, int x, int y, int width)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 28),
                BackColor = Color.FromArgb(40, 50, 70),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, 70, 90);
            return btn;
        }

        private void ApplyFilter()
        {
            string materialFilter = _cbMaterialFilter.SelectedItem?.ToString() ?? "(Todos)";
            int minArea = (int)_nudMinArea.Value;
            double minConf = (double)_nudMinConfidence.Value;
            double maxConf = (double)_nudMaxConfidence.Value;

            _filteredParticles = _scene.Summary.Particles
                .Where(p =>
                    (materialFilter == "(Todos)" || (p.MaterialId ?? "Unknown") == materialFilter) &&
                    p.ApproxAreaPixels >= minArea &&
                    p.Confidence >= minConf &&
                    p.Confidence <= maxConf)
                .OrderByDescending(p => p.ApproxAreaPixels)
                .ToList();

            _listParticles.Items.Clear();
            foreach (var p in _filteredParticles)
            {
                string qaStatus = _qaLabels.ContainsKey(p.ParticleId) ? "✅" : "⬜";
                string line = $"{qaStatus} {p.MaterialId,-8} A:{p.ApproxAreaPixels,5} C:{p.Confidence:F2}";
                _listParticles.Items.Add(line);
            }

            int labeled = _qaLabels.Count;
            int total = _scene.Summary.Particles.Count;
            _lblStats.Text = $"Filtradas: {_filteredParticles.Count} / {total}\nRotuladas: {labeled} / {total}";

            if (_filteredParticles.Count > 0)
            {
                _listParticles.SelectedIndex = 0;
            }
            else
            {
                _currentIndex = -1;
                _picParticle.Image = null;
                _lblInfo.Text = "Nenhuma partícula corresponde aos filtros.";
            }
        }

        private void ListParticles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int idx = _listParticles.SelectedIndex;
            if (idx < 0 || idx >= _filteredParticles.Count)
            {
                _currentIndex = -1;
                return;
            }

            _currentIndex = idx;
            var particle = _filteredParticles[idx];

            // Show particle crop
            ShowParticleCrop(particle);

            // Show particle info
            ShowParticleInfo(particle);

            // Load existing QA label if any
            if (_qaLabels.TryGetValue(particle.ParticleId, out var qa))
            {
                int labelIdx = _cbNewLabel.Items.IndexOf(qa.MaterialHuman);
                if (labelIdx >= 0) _cbNewLabel.SelectedIndex = labelIdx;
                _txtNotes.Text = qa.Notes ?? "";
            }
            else
            {
                // Default to predicted material
                int labelIdx = _cbNewLabel.Items.IndexOf(particle.MaterialId);
                if (labelIdx >= 0) _cbNewLabel.SelectedIndex = labelIdx;
                _txtNotes.Text = "";
            }
        }

        private void ShowParticleCrop(ParticleRecord particle)
        {
            try
            {
                int cropSize = 128;
                int half = cropSize / 2;
                int x1 = Math.Max(0, particle.CenterX - half);
                int y1 = Math.Max(0, particle.CenterY - half);
                int x2 = Math.Min(_sourceImage.Width, particle.CenterX + half);
                int y2 = Math.Min(_sourceImage.Height, particle.CenterY + half);

                int w = x2 - x1;
                int h = y2 - y1;

                if (w > 0 && h > 0)
                {
                    var crop = new Bitmap(w, h);
                    using (var g = Graphics.FromImage(crop))
                    {
                        g.DrawImage(_sourceImage,
                            new Rectangle(0, 0, w, h),
                            new Rectangle(x1, y1, w, h),
                            GraphicsUnit.Pixel);
                    }

                    _picParticle.Image?.Dispose();
                    _picParticle.Image = crop;
                }
            }
            catch
            {
                _picParticle.Image = null;
            }
        }

        private void ShowParticleInfo(ParticleRecord particle)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ID: {particle.ParticleId}");
            sb.AppendLine();
            sb.AppendLine($"Material: {particle.MaterialId}");
            sb.AppendLine($"Confiança: {particle.Confidence:F3}");
            sb.AppendLine($"Área: {particle.ApproxAreaPixels} px");
            sb.AppendLine();
            sb.AppendLine($"Centro: ({particle.CenterX}, {particle.CenterY})");
            sb.AppendLine($"Circularidade: {particle.Circularity:F3}");
            sb.AppendLine($"Aspect Ratio: {particle.AspectRatio:F2}");
            sb.AppendLine();
            sb.AppendLine($"HSV médio: H={particle.AvgH:F1} S={particle.AvgS:F2} V={particle.AvgV:F2}");
            sb.AppendLine();
            sb.AppendLine($"Score HVS: {particle.ScoreHvs:F3}");
            sb.AppendLine($"Score IA: {particle.ScoreIa:F3}");
            sb.AppendLine($"Score combinado: {particle.ScoreCombined:F3}");

            if (_qaLabels.TryGetValue(particle.ParticleId, out var qa))
            {
                sb.AppendLine();
                sb.AppendLine($"--- QA ---");
                sb.AppendLine($"Rótulo humano: {qa.MaterialHuman}");
                sb.AppendLine($"Por: {qa.Operator}");
                sb.AppendLine($"Em: {qa.Timestamp:yyyy-MM-dd HH:mm}");
            }

            _lblInfo.Text = sb.ToString();
        }

        private void BtnApplyLabel_Click(object? sender, EventArgs e)
        {
            if (_currentIndex < 0 || _currentIndex >= _filteredParticles.Count) return;

            var particle = _filteredParticles[_currentIndex];
            string newLabel = _cbNewLabel.SelectedItem?.ToString() ?? particle.MaterialId ?? "Unknown";

            ApplyQaLabel(particle, newLabel);
            MoveToNext();
        }

        private void BtnMarkNoise_Click(object? sender, EventArgs e)
        {
            if (_currentIndex < 0 || _currentIndex >= _filteredParticles.Count) return;

            var particle = _filteredParticles[_currentIndex];
            ApplyQaLabel(particle, "Artefato");
            MoveToNext();
        }

        private void BtnKeepPredicted_Click(object? sender, EventArgs e)
        {
            if (_currentIndex < 0 || _currentIndex >= _filteredParticles.Count) return;

            var particle = _filteredParticles[_currentIndex];
            ApplyQaLabel(particle, particle.MaterialId ?? "Unknown");
            MoveToNext();
        }

        private void ApplyQaLabel(ParticleRecord particle, string newLabel)
        {
            var qa = new QaLabel
            {
                ParticleId = particle.ParticleId,
                AnalysisId = _scene.Summary.Id,
                MaterialPredicted = particle.MaterialId ?? "Unknown",
                MaterialHuman = newLabel,
                Timestamp = DateTime.UtcNow,
                Operator = _settings.DefaultOperator ?? Environment.UserName,
                Notes = _txtNotes.Text
            };

            _qaLabels[particle.ParticleId] = qa;

            // Update list display
            int idx = _listParticles.SelectedIndex;
            if (idx >= 0)
            {
                string line = $"✅ {particle.MaterialId,-8} A:{particle.ApproxAreaPixels,5} C:{particle.Confidence:F2}";
                _listParticles.Items[idx] = line;
            }

            // Update stats
            int labeled = _qaLabels.Count;
            int total = _scene.Summary.Particles.Count;
            _lblStats.Text = $"Filtradas: {_filteredParticles.Count} / {total}\nRotuladas: {labeled} / {total}";
        }

        private void MoveToNext()
        {
            if (_currentIndex < _filteredParticles.Count - 1)
            {
                _listParticles.SelectedIndex = _currentIndex + 1;
            }
        }

        private void BtnSaveQa_Click(object? sender, EventArgs e)
        {
            SaveQaLabels();
            MessageBox.Show(this, $"QA salvo em:\n{_qaFilePath}", "QA Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnExportQa_Click(object? sender, EventArgs e)
        {
            SaveQaLabels();

            using var dlg = new SaveFileDialog
            {
                Title = "Exportar QA Labels",
                Filter = "CSV|*.csv",
                FileName = $"qa_labels_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                File.Copy(_qaFilePath, dlg.FileName, true);
                MessageBox.Show(this, $"Exportado para:\n{dlg.FileName}", "Exportado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SaveQaLabels()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_qaFilePath)!);

            var sb = new StringBuilder();
            sb.AppendLine("ParticleId,AnalysisId,MaterialPredicted,MaterialHuman,Timestamp,Operator,Notes");

            foreach (var qa in _qaLabels.Values)
            {
                sb.AppendLine(
                    $"\"{qa.ParticleId}\"," +
                    $"\"{qa.AnalysisId}\"," +
                    $"\"{EscapeCsv(qa.MaterialPredicted)}\"," +
                    $"\"{EscapeCsv(qa.MaterialHuman)}\"," +
                    $"\"{qa.Timestamp:O}\"," +
                    $"\"{EscapeCsv(qa.Operator)}\"," +
                    $"\"{EscapeCsv(qa.Notes)}\"");
            }

            File.WriteAllText(_qaFilePath, sb.ToString(), Encoding.UTF8);
        }

        private void LoadExistingQaLabels()
        {
            if (!File.Exists(_qaFilePath)) return;

            try
            {
                var lines = File.ReadAllLines(_qaFilePath);
                for (int i = 1; i < lines.Length; i++) // Skip header
                {
                    var parts = ParseCsvLine(lines[i]);
                    if (parts.Length >= 6)
                    {
                        if (Guid.TryParse(parts[0].Trim('"'), out var particleId))
                        {
                            _qaLabels[particleId] = new QaLabel
                            {
                                ParticleId = particleId,
                                AnalysisId = Guid.TryParse(parts[1].Trim('"'), out var aid) ? aid : Guid.Empty,
                                MaterialPredicted = parts[2].Trim('"'),
                                MaterialHuman = parts[3].Trim('"'),
                                Timestamp = DateTime.TryParse(parts[4].Trim('"'), out var ts) ? ts : DateTime.UtcNow,
                                Operator = parts[5].Trim('"'),
                                Notes = parts.Length > 6 ? parts[6].Trim('"') : ""
                            };
                        }
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());

            return result.ToArray();
        }

        private static string EscapeCsv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }
    }

    /// <summary>
    /// QA label for a particle.
    /// </summary>
    public class QaLabel
    {
        public Guid ParticleId { get; set; }
        public Guid AnalysisId { get; set; }
        public string MaterialPredicted { get; set; } = "";
        public string MaterialHuman { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Operator { get; set; } = "";
        public string? Notes { get; set; }
    }
}
