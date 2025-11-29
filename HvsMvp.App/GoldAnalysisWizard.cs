using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR15: Wizard for guided gold sample analysis with Live camera.
    /// Steps: 1) Select camera, 2) Position sample, 3) Analyze, 4) Generate report.
    /// </summary>
    public class GoldAnalysisWizard : WizardForm
    {
        private readonly MainForm _mainForm;
        private readonly HvsConfig? _config;
        
        // Step state
        private string? _imagePath;
        private bool _useCamera = true;
        private string _sampleName = "";
        
        // Step 1 controls
        private RadioButton? _rbCamera;
        private RadioButton? _rbImage;
        private TextBox? _txtImagePath;
        private Button? _btnBrowse;
        
        // Step 2 controls
        private PictureBox? _previewBox;
        private Label? _lblPreviewStatus;
        
        // Step 3 controls
        private TextBox? _txtSampleName;
        private TextBox? _txtNotes;
        
        // Step 4 controls
        private TextBox? _txtResultSummary;
        private CheckBox? _chkGeneratePdf;
        private CheckBox? _chkGenerateTxt;
        
        public GoldAnalysisWizard(AppSettings settings, MainForm mainForm, HvsConfig? config)
            : base(settings, "Assistente: Análise de Ouro (Au)")
        {
            _mainForm = mainForm;
            _config = config;
            
            DefineSteps();
            InitializeWizard();
        }
        
        private void DefineSteps()
        {
            // Step 1: Select source (Camera or Image)
            AddStep(new WizardStep
            {
                Title = "Fonte da Imagem",
                Description = "Escolha se deseja usar a câmera ao vivo ou carregar uma imagem existente.",
                BuildContent = BuildStep1Content,
                Validate = ValidateStep1,
                OnCompleted = () => { }
            });
            
            // Step 2: Position/Load sample
            AddStep(new WizardStep
            {
                Title = "Amostra",
                Description = "Posicione a amostra de ouro ou verifique a imagem carregada.",
                BuildContent = BuildStep2Content,
                Validate = ValidateStep2,
                OnCompleted = () => { }
            });
            
            // Step 3: Sample info
            AddStep(new WizardStep
            {
                Title = "Informações",
                Description = "Preencha os dados da amostra para o laudo.",
                BuildContent = BuildStep3Content,
                Validate = ValidateStep3,
                OnCompleted = () => { }
            });
            
            // Step 4: Analyze and results
            AddStep(new WizardStep
            {
                Title = "Análise e Laudo",
                Description = "Execute a análise e gere o laudo final.",
                BuildContent = BuildStep4Content,
                Validate = () => true,
                OnCompleted = ExecuteAnalysis
            });
        }
        
        private void BuildStep1Content(Panel panel)
        {
            var lblInstructions = new Label
            {
                Text = "Selecione a fonte da imagem para análise:",
                Location = new Point(0, 10),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke
            };
            panel.Controls.Add(lblInstructions);
            
            _rbCamera = new RadioButton
            {
                Text = "🎥 Usar câmera ao vivo (Live)",
                Location = new Point(20, 50),
                Size = new Size(380, 30),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke,
                Checked = _useCamera
            };
            _rbCamera.CheckedChanged += (s, e) => UpdateStep1State();
            panel.Controls.Add(_rbCamera);
            
            var lblCameraDesc = new Label
            {
                Text = "Captura em tempo real da câmera conectada ao microscópio.",
                Location = new Point(45, 80),
                Size = new Size(380, 20),
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(140, 150, 170)
            };
            panel.Controls.Add(lblCameraDesc);
            
            _rbImage = new RadioButton
            {
                Text = "📂 Carregar imagem existente",
                Location = new Point(20, 120),
                Size = new Size(380, 30),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke,
                Checked = !_useCamera
            };
            _rbImage.CheckedChanged += (s, e) => UpdateStep1State();
            panel.Controls.Add(_rbImage);
            
            _txtImagePath = new TextBox
            {
                Location = new Point(45, 155),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(35, 40, 50),
                ForeColor = Color.WhiteSmoke,
                Enabled = !_useCamera,
                Text = _imagePath ?? ""
            };
            panel.Controls.Add(_txtImagePath);
            
            _btnBrowse = new Button
            {
                Text = "...",
                Location = new Point(350, 155),
                Size = new Size(35, 25),
                BackColor = Color.FromArgb(50, 55, 65),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Enabled = !_useCamera
            };
            _btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(70, 75, 85);
            _btnBrowse.Click += BtnBrowse_Click;
            panel.Controls.Add(_btnBrowse);
            
            var lblTip = new Label
            {
                Text = "💡 Dica: Para melhores resultados, use iluminação brightfield e foco adequado.",
                Location = new Point(0, 220),
                Size = new Size(450, 25),
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(200, 160, 60)
            };
            panel.Controls.Add(lblTip);
        }
        
        private void UpdateStep1State()
        {
            _useCamera = _rbCamera?.Checked ?? true;
            if (_txtImagePath != null) _txtImagePath.Enabled = !_useCamera;
            if (_btnBrowse != null) _btnBrowse.Enabled = !_useCamera;
        }
        
        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar imagem de amostra",
                Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Todos|*.*"
            };
            
            if (!string.IsNullOrWhiteSpace(_settings.DefaultImagesDirectory) &&
                Directory.Exists(_settings.DefaultImagesDirectory))
            {
                dlg.InitialDirectory = _settings.DefaultImagesDirectory;
            }
            
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _imagePath = dlg.FileName;
                if (_txtImagePath != null)
                {
                    _txtImagePath.Text = _imagePath;
                }
            }
        }
        
        private bool ValidateStep1()
        {
            _useCamera = _rbCamera?.Checked ?? true;
            
            if (!_useCamera)
            {
                _imagePath = _txtImagePath?.Text;
                if (string.IsNullOrWhiteSpace(_imagePath) || !File.Exists(_imagePath))
                {
                    MessageBox.Show(this, 
                        "Selecione uma imagem válida para análise.", 
                        "Validação", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Warning);
                    return false;
                }
            }
            
            return true;
        }
        
        private void BuildStep2Content(Panel panel)
        {
            if (_useCamera)
            {
                var lblInstructions = new Label
                {
                    Text = "Posicione a amostra de ouro sob o microscópio:",
                    Location = new Point(0, 10),
                    Size = new Size(400, 25),
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.WhiteSmoke
                };
                panel.Controls.Add(lblInstructions);
                
                var instructions = new string[]
                {
                    "1. Coloque a amostra na platina do microscópio",
                    "2. Ajuste o foco até obter nitidez",
                    "3. Verifique a iluminação (brightfield recomendado)",
                    "4. Centralize a região de interesse"
                };
                
                int y = 50;
                foreach (var instruction in instructions)
                {
                    var lbl = new Label
                    {
                        Text = instruction,
                        Location = new Point(20, y),
                        Size = new Size(400, 22),
                        Font = new Font("Segoe UI", 9),
                        ForeColor = Color.FromArgb(180, 190, 210)
                    };
                    panel.Controls.Add(lbl);
                    y += 25;
                }
                
                _lblPreviewStatus = new Label
                {
                    Text = "📷 O Live será iniciado automaticamente ao avançar.",
                    Location = new Point(0, 200),
                    Size = new Size(450, 25),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 200, 100)
                };
                panel.Controls.Add(_lblPreviewStatus);
            }
            else
            {
                var lblInstructions = new Label
                {
                    Text = "Verifique a imagem carregada:",
                    Location = new Point(0, 10),
                    Size = new Size(400, 25),
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.WhiteSmoke
                };
                panel.Controls.Add(lblInstructions);
                
                _previewBox = new PictureBox
                {
                    Location = new Point(0, 45),
                    Size = new Size(400, 220),
                    BackColor = Color.Black,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle
                };
                
                if (!string.IsNullOrWhiteSpace(_imagePath) && File.Exists(_imagePath))
                {
                    try
                    {
                        using var bmp = new Bitmap(_imagePath);
                        _previewBox.Image = (Bitmap)bmp.Clone();
                    }
                    catch
                    {
                        // Ignore preview errors
                    }
                }
                
                panel.Controls.Add(_previewBox);
                
                var lblPath = new Label
                {
                    Text = $"📁 {Path.GetFileName(_imagePath ?? "")}",
                    Location = new Point(0, 275),
                    Size = new Size(450, 22),
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.FromArgb(160, 170, 190)
                };
                panel.Controls.Add(lblPath);
            }
        }
        
        private bool ValidateStep2()
        {
            return true; // Always valid - user visually confirmed
        }
        
        private void BuildStep3Content(Panel panel)
        {
            var lblSampleName = new Label
            {
                Text = "Nome/ID da amostra:",
                Location = new Point(0, 10),
                Size = new Size(180, 22),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke
            };
            panel.Controls.Add(lblSampleName);
            
            _txtSampleName = new TextBox
            {
                Location = new Point(0, 35),
                Size = new Size(350, 28),
                BackColor = Color.FromArgb(35, 40, 50),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10),
                Text = _sampleName
            };
            panel.Controls.Add(_txtSampleName);
            
            var lblNotes = new Label
            {
                Text = "Observações (opcional):",
                Location = new Point(0, 80),
                Size = new Size(180, 22),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke
            };
            panel.Controls.Add(lblNotes);
            
            _txtNotes = new TextBox
            {
                Location = new Point(0, 105),
                Size = new Size(450, 100),
                BackColor = Color.FromArgb(35, 40, 50),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            panel.Controls.Add(_txtNotes);
            
            var lblTip = new Label
            {
                Text = "💡 Inclua informações como origem, lote, data de coleta, etc.",
                Location = new Point(0, 220),
                Size = new Size(450, 22),
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(200, 160, 60)
            };
            panel.Controls.Add(lblTip);
        }
        
        private bool ValidateStep3()
        {
            _sampleName = _txtSampleName?.Text?.Trim() ?? "";
            
            if (string.IsNullOrWhiteSpace(_sampleName))
            {
                MessageBox.Show(this,
                    "Informe o nome ou ID da amostra.",
                    "Validação",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
            
            return true;
        }
        
        private void BuildStep4Content(Panel panel)
        {
            var lblInstructions = new Label
            {
                Text = "Configuração do laudo final:",
                Location = new Point(0, 10),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke
            };
            panel.Controls.Add(lblInstructions);
            
            _chkGeneratePdf = new CheckBox
            {
                Text = "📄 Gerar laudo PDF",
                Location = new Point(20, 50),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke,
                Checked = _settings.GeneratePdfByDefault
            };
            panel.Controls.Add(_chkGeneratePdf);
            
            _chkGenerateTxt = new CheckBox
            {
                Text = "📝 Gerar laudo TXT",
                Location = new Point(20, 80),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke,
                Checked = _settings.GenerateTxtByDefault
            };
            panel.Controls.Add(_chkGenerateTxt);
            
            var lblSummaryTitle = new Label
            {
                Text = "Resumo da análise (após execução):",
                Location = new Point(0, 130),
                Size = new Size(300, 22),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.WhiteSmoke
            };
            panel.Controls.Add(lblSummaryTitle);
            
            _txtResultSummary = new TextBox
            {
                Location = new Point(0, 155),
                Size = new Size(450, 120),
                BackColor = Color.FromArgb(25, 30, 40),
                ForeColor = Color.FromArgb(180, 190, 210),
                Font = new Font("Consolas", 9),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "Clique em 'Concluir' para executar a análise..."
            };
            panel.Controls.Add(_txtResultSummary);
        }
        
        private void ExecuteAnalysis()
        {
            try
            {
                // For now, this wizard prepares everything and lets MainForm do the actual analysis
                // In a full implementation, we would capture/analyze here
                
                // Update sample name in settings for the report
                if (!string.IsNullOrWhiteSpace(_sampleName))
                {
                    _settings.DefaultSampleName = _sampleName;
                }
                
                // The actual analysis will be triggered when the wizard closes
                // and MainForm applies the wizard result
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Erro durante a análise:\n\n{ex.Message}",
                    "Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        
        protected override void OnWizardCompleted()
        {
            // Store wizard results
            WizardResult = new GoldAnalysisWizardResult
            {
                UseCamera = _useCamera,
                ImagePath = _imagePath,
                SampleName = _sampleName,
                Notes = _txtNotes?.Text ?? "",
                GeneratePdf = _chkGeneratePdf?.Checked ?? false,
                GenerateTxt = _chkGenerateTxt?.Checked ?? true
            };
            
            DialogResult = DialogResult.OK;
            Close();
        }
        
        /// <summary>
        /// Gets the wizard result after completion.
        /// </summary>
        public GoldAnalysisWizardResult? WizardResult { get; private set; }
    }
    
    /// <summary>
    /// PR15: Result from the Gold Analysis Wizard.
    /// </summary>
    public class GoldAnalysisWizardResult
    {
        public bool UseCamera { get; set; }
        public string? ImagePath { get; set; }
        public string SampleName { get; set; } = "";
        public string Notes { get; set; } = "";
        public bool GeneratePdf { get; set; }
        public bool GenerateTxt { get; set; }
    }
}
