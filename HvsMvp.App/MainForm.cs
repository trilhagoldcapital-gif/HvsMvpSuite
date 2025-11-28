using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Collections.Generic;

namespace HvsMvp.App
{
    public class MainForm : Form
    {
        private HvsConfig? _config;
        private HvsAnalysisService? _analysisService;
        private AppSettings _appSettings = null!;

        // Cena completa da última análise
        private FullSceneAnalysis? _lastScene;

        // Last exported report path for sharing
        private string? _lastExportedReportPath;

        private Panel _topContainer = null!;
        private FlowLayoutPanel _toolbarRow1 = null!;
        private FlowLayoutPanel _toolbarRow2 = null!;
        private FlowLayoutPanel _toolbarRow3 = null!;
        private Panel _headerBar = null!;
        private Label _lblTitle = null!;
        private Button _btnLanguage = null!;
        private ContextMenuStrip _languageMenu = null!;

        private SplitContainer _mainVerticalSplit = null!;
        private SplitContainer _contentVerticalSplit = null!;
        private SplitContainer _cameraMaterialsSplit = null!;

        private Panel _imagePanel = null!;
        private PictureBox _pictureSample = null!;

        private Panel _rightPanel = null!;
        private TableLayoutPanel _materialsLayout = null!;
        private Label _lblMetalsHeader = null!;
        private Label _lblCrystalsHeader = null!;
        private Label _lblGemsHeader = null!;
        private ListBox _listMetals = null!;
        private ListBox _listCrystals = null!;
        private ListBox _listGems = null!;

        private ComboBox _cbTarget = null!;
        private Button _btnSelectiveAnalyze = null!;

        private TextBox _txtDetails = null!;
        private Panel _footerPanel = null!;
        private Label _lblStatus = null!;

        private Button _btnOpen = null!;
        private Button _btnLive = null!;
        private Button _btnStopLive = null!;
        private Button _btnAnalyze = null!;
        private Button _btnContinuous = null!;
        private Button _btnStopContinuous = null!;
        private Button _btnMask = null!;
        private Button _btnMaskBg = null!;
        private Button _btnPhaseMap = null!;   // NEW: Phase map visualization
        private Button _btnHeatmap = null!;     // NEW: Target heatmap
        private Button _btnTraining = null!;    // NEW: Training mode toggle
        private Button _btnAi = null!;
        private Button _btnZoomIn = null!;
        private Button _btnZoomOut = null!;
        private Button _btnWB = null!;
        private Button _btnScale = null!;
        private Button _btnCameraSel = null!;
        private Button _btnResolucaoSel = null!;
        private Button _btnTxt = null!;
        private Button _btnPdf = null!;      // NEW: PDF export
        private Button _btnJson = null!;
        private Button _btnCsv = null!;
        private Button _btnBiCsv = null!;
        private Button _btnExportIa = null!;   // NOVO: export dataset IA
        private Button _btnQaPanel = null!;    // NEW: QA Panel
        private Button _btnDebugHvs = null!;
        private Button _btnCalib = null!;
        private Button _btnSettings = null!;
        private Button _btnAbout = null!;
        private Button _btnWhatsApp = null!;

        // Services for new features
        private PhaseMapService _phaseMapService = new PhaseMapService();
        private TrainingModeService _trainingService = new TrainingModeService();
        private ReportService? _reportService;
        private BiExportService? _biExportService;
        private IaDatasetService? _iaDatasetService;

        // Quality checklist panel
        private QualityChecklistPanel _qualityPanel = null!;

        // Footer version label
        private Label _lblVersion = null!;

        private readonly ToolTip _hvsToolTip = new ToolTip { IsBalloon = false, UseAnimation = true, UseFading = true };

        private readonly MicroscopeCameraService _microscopeCamera = new MicroscopeCameraService();
        private bool _liveRunning;
        private int _cameraIndex = 1;
        private int _cameraWidth = 1920;
        private int _cameraHeight = 1080;

        // Idioma
        private string _currentLocale = "pt-BR";
        private readonly Dictionary<string, Dictionary<string, string>> _i18n =
            new()
            {
                ["pt-BR"] = new()
                {
                    ["title"] = "TGC Metal Analítico · HVS-MVP",
                    ["metals"] = "Metais",
                    ["crystals"] = "Cristais",
                    ["gems"] = "Gemas",
                    ["status.ready"] = "Pronto · HVS-MVP carregado",
                    ["btn.open"] = "📂 Abrir imagem",
                    ["btn.live"] = "▶ Live",
                    ["btn.stop"] = "⏹ Parar",
                    ["btn.analyze"] = "🧪 Analisar",
                    ["btn.cont"] = "⚙ Contínuo",
                    ["btn.cont.stop"] = "⏸ Parar contínuo",
                    ["btn.mask"] = "🎨 Máscara",
                    ["btn.mask.bg"] = "🖼 Fundo mascarado",
                    ["btn.phase.map"] = "🗺 Mapa de Fases",
                    ["btn.heatmap"] = "🔥 Heatmap Alvo",
                    ["btn.training"] = "🎯 Modo Treino",
                    ["btn.ai"] = "🔬 Partículas / Dataset IA",
                    ["btn.zoom.in"] = "🔍 Zoom +",
                    ["btn.zoom.out"] = "🔎 Zoom -",
                    ["btn.wb"] = "⚪ Balanço de branco",
                    ["btn.scale"] = "📏 Escala",
                    ["btn.camera"] = "🎥 Câmera...",
                    ["btn.res"] = "⚙️ Resolução...",
                    ["btn.txt"] = "📝 Laudo TXT",
                    ["btn.pdf"] = "📄 Laudo PDF",
                    ["btn.json"] = "{} JSON",
                    ["btn.csv"] = "📊 CSV",
                    ["btn.bi.csv"] = "📈 BI CSV",
                    ["btn.export.ia"] = "🤖 Dataset IA",
                    ["btn.qa.panel"] = "✅ QA Partículas",
                    ["btn.debug"] = "🛠 Debug HVS",
                    ["btn.calib"] = "📸 Calibrar (auto)",
                    ["label.target"] = "Alvo:"
                },
                ["en-US"] = new()
                {
                    ["title"] = "TGC Metal Analytics · HVS-MVP",
                    ["metals"] = "Metals",
                    ["crystals"] = "Crystals",
                    ["gems"] = "Gems",
                    ["status.ready"] = "Ready · HVS-MVP loaded",
                    ["btn.open"] = "📂 Open image",
                    ["btn.live"] = "▶ Live",
                    ["btn.stop"] = "⏹ Stop",
                    ["btn.analyze"] = "🧪 Analyze",
                    ["btn.cont"] = "⚙ Continuous",
                    ["btn.cont.stop"] = "⏸ Stop continuous",
                    ["btn.mask"] = "🎨 Mask",
                    ["btn.mask.bg"] = "🖼 Background masked",
                    ["btn.phase.map"] = "🗺 Phase Map",
                    ["btn.heatmap"] = "🔥 Target Heatmap",
                    ["btn.training"] = "🎯 Training Mode",
                    ["btn.ai"] = "🔬 Particles / AI Dataset",
                    ["btn.zoom.in"] = "🔍 Zoom +",
                    ["btn.zoom.out"] = "🔎 Zoom -",
                    ["btn.wb"] = "⚪ White balance",
                    ["btn.scale"] = "📏 Scale",
                    ["btn.camera"] = "🎥 Camera...",
                    ["btn.res"] = "⚙️ Resolution...",
                    ["btn.txt"] = "📝 TXT Report",
                    ["btn.pdf"] = "📄 PDF Report",
                    ["btn.json"] = "{} JSON",
                    ["btn.csv"] = "📊 CSV",
                    ["btn.bi.csv"] = "📈 BI CSV",
                    ["btn.export.ia"] = "🤖 IA Dataset",
                    ["btn.qa.panel"] = "✅ QA Particles",
                    ["btn.debug"] = "🛠 HVS Debug",
                    ["btn.calib"] = "📸 Calibrate (auto)",
                    ["label.target"] = "Target:"
                }
            };

        // Zoom
        private float _zoomFactor = 1.0f;

        // Análise contínua
        private ContinuousAnalysisController? _continuousController;
        private bool _continuousRunning;

        // Visualizações
        private Bitmap? _lastBaseImageClone;   // imagem analisada
        
        // ViewMode tracking (PR7 - Fase 1)
        private ViewMode _currentViewMode = ViewMode.Original;
        private string? _currentTargetMaterial;

        public MainForm()
        {
            Text = "TGC Metal Analítico – HVS-MVP";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1280, 720);
            BackColor = Color.FromArgb(5, 10, 20);

            // Load app settings
            _appSettings = AppSettings.Load();

            // Initialize production services
            _reportService = new ReportService(_appSettings);
            _biExportService = new BiExportService(_appSettings);
            _iaDatasetService = new IaDatasetService(_appSettings);

            // Apply settings to camera
            _cameraIndex = _appSettings.DefaultCameraIndex;
            _cameraWidth = _appSettings.GetResolutionWidth();
            _cameraHeight = _appSettings.GetResolutionHeight();

            LoadHvsConfig();
            InitializeLayout();
            InitializeCameraEvents();
            PopulateMaterials();

            FormClosing += MainForm_FormClosing;

            // Check for updates on startup if enabled
            CheckForUpdatesOnStartupAsync();
        }

        private void LoadHvsConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hvs-config.json");
                if (!File.Exists(configPath))
                {
                    MessageBox.Show(this, $"Arquivo de configuração não encontrado:\n{configPath}", "HVS Config", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }

                var json = File.ReadAllText(configPath);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _config = JsonSerializer.Deserialize<HvsConfig>(json, opts);

                if (_config?.App != null && !string.IsNullOrWhiteSpace(_config.App.Name))
                    Text = $"{_config.App.Name} · {(_config.App.Version ?? "v1.0")}";

                if (_config != null)
                    _analysisService = new HvsAnalysisService(_config);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao carregar configuração HVS:\n\n{ex}", "HVS Config", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        private void InitializeLayout()
        {
            _topContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 170,
                BackColor = Color.FromArgb(8, 16, 28)
            };
            Controls.Add(_topContainer);

            var topLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 15));
            _topContainer.Controls.Add(topLayout);

            var toolbarPanel1 = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 24, 40),
                Padding = new Padding(6, 4, 6, 2)
            };
            topLayout.Controls.Add(toolbarPanel1, 0, 0);

            _toolbarRow1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true
            };
            toolbarPanel1.Controls.Add(_toolbarRow1);

            var toolbarPanel2 = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 24, 40),
                Padding = new Padding(6, 0, 6, 2)
            };
            topLayout.Controls.Add(toolbarPanel2, 0, 1);

            _toolbarRow2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true
            };
            toolbarPanel2.Controls.Add(_toolbarRow2);

            // Third toolbar row for settings, about, and sharing
            var toolbarPanel3 = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 24, 40),
                Padding = new Padding(6, 0, 6, 2)
            };
            topLayout.Controls.Add(toolbarPanel3, 0, 2);

            _toolbarRow3 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true
            };
            toolbarPanel3.Controls.Add(_toolbarRow3);

            _headerBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(200, 160, 60),
                Padding = new Padding(16, 4, 16, 4)
            };
            topLayout.Controls.Add(_headerBar, 0, 3);

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _headerBar.Controls.Add(headerLayout);

            _lblTitle = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(20, 20, 30),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Margin = new Padding(4, 0, 4, 0),
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };
            headerLayout.Controls.Add(_lblTitle, 0, 0);

            _languageMenu = new ContextMenuStrip();
            _languageMenu.Items.Add("Português (pt-BR)", null, (s, e) => SetLanguage("pt-BR"));
            _languageMenu.Items.Add("English (en-US)", null, (s, e) => SetLanguage("en-US"));
            _languageMenu.Items.Add("Español (es-ES)", null, (s, e) => SetLanguage("es-ES"));
            _languageMenu.Items.Add("Français (fr-FR)", null, (s, e) => SetLanguage("fr-FR"));
            _languageMenu.Items.Add("العربية", null, (s, e) => SetLanguage("ar"));
            _languageMenu.Items.Add("中文", null, (s, e) => SetLanguage("zh-CN"));

            _btnLanguage = new Button
            {
                Text = "Idioma ▾",
                Size = new Size(110, 26),
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(12, 0, 0, 0)
            };
            _btnLanguage.FlatAppearance.BorderColor = Color.FromArgb(30, 30, 50);
            _btnLanguage.FlatAppearance.BorderSize = 1;
            _btnLanguage.Click += BtnLanguage_Click;
            headerLayout.Controls.Add(_btnLanguage, 1, 0);

            Button Cmd(string text)
            {
                var b = new Button
                {
                    Text = text,
                    AutoSize = true,
                    Height = 24,
                    Margin = new Padding(4, 1, 4, 1),
                    Padding = new Padding(8, 1, 8, 1),
                    BackColor = Color.FromArgb(20, 40, 65),
                    ForeColor = Color.WhiteSmoke,
                    FlatStyle = FlatStyle.Flat
                };
                b.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 110);
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 60, 95);
                b.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 80, 120);
                return b;
            }

            _btnOpen = Cmd("");
            _btnOpen.Click += BtnOpenImage_Click;
            _toolbarRow1.Controls.Add(_btnOpen);

            _btnLive = Cmd("");
            _btnLive.Click += BtnLive_Click;
            _toolbarRow1.Controls.Add(_btnLive);

            _btnStopLive = Cmd("");
            _btnStopLive.Click += BtnParar_Click;
            _toolbarRow1.Controls.Add(_btnStopLive);

            _btnAnalyze = Cmd("");
            _btnAnalyze.Click += BtnAnalisar_Click;
            _toolbarRow1.Controls.Add(_btnAnalyze);

            _btnContinuous = Cmd("");
            _btnContinuous.Click += BtnContinuous_Click;
            _toolbarRow1.Controls.Add(_btnContinuous);

            _btnStopContinuous = Cmd("");
            _btnStopContinuous.Click += BtnStopContinuous_Click;
            _toolbarRow1.Controls.Add(_btnStopContinuous);

            var lblAlvo = new Label
            {
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(10, 6, 2, 0)
            };
            _toolbarRow1.Controls.Add(lblAlvo);

            _cbTarget = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160,
                Margin = new Padding(2, 3, 4, 0),
                BackColor = Color.FromArgb(32, 32, 44),
                ForeColor = Color.White
            };
            _toolbarRow1.Controls.Add(_cbTarget);

            _btnSelectiveAnalyze = Cmd("🎯 Análise seletiva");
            _btnSelectiveAnalyze.Click += BtnSelectiveAnalyze_Click;
            _toolbarRow1.Controls.Add(_btnSelectiveAnalyze);

            _btnMask = Cmd("");
            _btnMask.Click += BtnMascara_Click;
            _toolbarRow1.Controls.Add(_btnMask);

            _btnMaskBg = Cmd("");
            _btnMaskBg.Click += BtnFundoMasc_Click;
            _toolbarRow1.Controls.Add(_btnMaskBg);

            // NEW: Phase Map button
            _btnPhaseMap = Cmd("");
            _btnPhaseMap.Click += BtnPhaseMap_Click;
            _toolbarRow1.Controls.Add(_btnPhaseMap);

            // NEW: Heatmap button
            _btnHeatmap = Cmd("");
            _btnHeatmap.Click += BtnHeatmap_Click;
            _toolbarRow1.Controls.Add(_btnHeatmap);

            _btnAi = Cmd("");
            _btnAi.Click += BtnParticulas_Click;
            _toolbarRow2.Controls.Add(_btnAi);

            _btnZoomIn = Cmd("");
            _btnZoomIn.Click += BtnZoomMais_Click;
            _toolbarRow2.Controls.Add(_btnZoomIn);

            _btnZoomOut = Cmd("");
            _btnZoomOut.Click += BtnZoomMenos_Click;
            _toolbarRow2.Controls.Add(_btnZoomOut);

            _btnWB = Cmd("");
            _btnWB.Click += BtnBalanco_Click;
            _toolbarRow2.Controls.Add(_btnWB);

            _btnScale = Cmd("");
            _btnScale.Click += BtnEscala_Click;
            _toolbarRow2.Controls.Add(_btnScale);

            _btnCameraSel = Cmd("");
            _btnCameraSel.Click += BtnSelecionarCamera_Click;
            _toolbarRow2.Controls.Add(_btnCameraSel);

            _btnResolucaoSel = Cmd("");
            _btnResolucaoSel.Click += BtnSelecionarResolucao_Click;
            _toolbarRow2.Controls.Add(_btnResolucaoSel);

            _btnTxt = Cmd("");
            _btnTxt.Click += BtnTxt_Click;
            _toolbarRow2.Controls.Add(_btnTxt);

            _btnPdf = Cmd("");
            _btnPdf.Click += BtnPdf_Click;
            _toolbarRow2.Controls.Add(_btnPdf);

            _btnJson = Cmd("");
            _btnJson.Click += BtnJson_Click;
            _toolbarRow2.Controls.Add(_btnJson);

            _btnCsv = Cmd("");
            _btnCsv.Click += BtnCsv_Click;
            _toolbarRow2.Controls.Add(_btnCsv);

            _btnBiCsv = Cmd("");
            _btnBiCsv.Click += BtnBiCsv_Click;
            _toolbarRow2.Controls.Add(_btnBiCsv);

            // NOVO: botão Exportar dataset IA
            _btnExportIa = Cmd("");
            _btnExportIa.Click += BtnExportIa_Click;
            _toolbarRow2.Controls.Add(_btnExportIa);

            // NOVO: botão QA Panel
            _btnQaPanel = Cmd("");
            _btnQaPanel.Click += BtnQaPanel_Click;
            _toolbarRow2.Controls.Add(_btnQaPanel);

            _btnDebugHvs = Cmd("");
            _btnDebugHvs.Click += BtnDebugHvs_Click;
            _toolbarRow2.Controls.Add(_btnDebugHvs);

            _btnCalib = Cmd("");
            _btnCalib.Click += BtnCalibrarAuto_Click;
            _toolbarRow2.Controls.Add(_btnCalib);

            // Third row: Settings, About, WhatsApp sharing, Training Mode
            _btnSettings = Cmd("⚙️ Configurações");
            _btnSettings.Click += BtnSettings_Click;
            _toolbarRow3.Controls.Add(_btnSettings);

            _btnAbout = Cmd("ℹ️ Sobre");
            _btnAbout.Click += BtnAbout_Click;
            _toolbarRow3.Controls.Add(_btnAbout);

            _btnWhatsApp = Cmd("📱 WhatsApp");
            _btnWhatsApp.Click += BtnWhatsApp_Click;
            _toolbarRow3.Controls.Add(_btnWhatsApp);

            // NEW: Training mode button
            _btnTraining = Cmd("");
            _btnTraining.Click += BtnTraining_Click;
            _toolbarRow3.Controls.Add(_btnTraining);

            _mainVerticalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(30, 30, 50),
                FixedPanel = FixedPanel.None,
                IsSplitterFixed = false
            };
            Controls.Add(_mainVerticalSplit);

            _mainVerticalSplit.Panel1MinSize = 100;
            _mainVerticalSplit.Panel2MinSize = 200;

            Controls.Remove(_topContainer);
            _mainVerticalSplit.Panel1.Controls.Add(_topContainer);
            _topContainer.Dock = DockStyle.Fill;

            this.Load += (s, e) =>
            {
                _mainVerticalSplit.SplitterDistance = (int)(ClientSize.Height * 0.20);
            };

            _contentVerticalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(25, 30, 45),
                FixedPanel = FixedPanel.None,
                IsSplitterFixed = false
            };
            _mainVerticalSplit.Panel2.Controls.Add(_contentVerticalSplit);

            _contentVerticalSplit.Panel1MinSize = 200;
            _contentVerticalSplit.Panel2MinSize = 100;
            _contentVerticalSplit.SplitterDistance = (int)(ClientSize.Height * 0.6);

            _cameraMaterialsSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(25, 30, 45),
                FixedPanel = FixedPanel.None,
                IsSplitterFixed = false
            };
            _contentVerticalSplit.Panel1.Controls.Add(_cameraMaterialsSplit);

            _cameraMaterialsSplit.Panel1MinSize = 300;
            _cameraMaterialsSplit.Panel2MinSize = 250;
            _cameraMaterialsSplit.SplitterDistance = (int)(ClientSize.Width * 0.63);

            _imagePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(8),
                AutoScroll = true
            };
            _cameraMaterialsSplit.Panel1.Controls.Add(_imagePanel);

            _pictureSample = new PictureBox
            {
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Normal
            };
            _imagePanel.Controls.Add(_pictureSample);
            _pictureSample.MouseMove += PictureSample_MouseMove;

            _rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(8, 18, 30),
                Padding = new Padding(8)
            };
            _cameraMaterialsSplit.Panel2.Controls.Add(_rightPanel);

            _materialsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            _materialsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _materialsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _materialsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            _rightPanel.Controls.Add(_materialsLayout);

            var matHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 22
            };
            _materialsLayout.Controls.Add(matHeader, 0, 0);

            var lblMatTitle = new Label
            {
                Text = "Metais / Cristais / Gemas",
                AutoSize = true,
                ForeColor = Color.FromArgb(210, 220, 235),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Location = new Point(2, 3)
            };
            matHeader.Controls.Add(lblMatTitle);

            var columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            _materialsLayout.Controls.Add(columns, 0, 1);

            (Panel colPanel, Label header, ListBox list) CreateColumn(string headerText)
            {
                var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 2, 4, 2) };
                var hdr = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 18,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(220, 230, 245),
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    Text = headerText
                };
                var lb = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(12, 24, 36),
                    ForeColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Segoe UI", 8)
                };
                panel.Controls.Add(lb);
                panel.Controls.Add(hdr);
                return (panel, hdr, lb);
            }

            var (colMet, hdrMet, listMet) = CreateColumn("Metais (0)");
            _lblMetalsHeader = hdrMet;
            _listMetals = listMet;
            columns.Controls.Add(colMet, 0, 0);

            var (colCr, hdrCr, listCr) = CreateColumn("Cristais (0)");
            _lblCrystalsHeader = hdrCr;
            _listCrystals = listCr;
            columns.Controls.Add(colCr, 1, 0);

            var (colGe, hdrGe, listGe) = CreateColumn("Gemas (0)");
            _lblGemsHeader = hdrGe;
            _listGems = listGe;
            columns.Controls.Add(colGe, 2, 0);

            // Quality Checklist Panel
            _qualityPanel = new QualityChecklistPanel
            {
                Dock = DockStyle.Fill
            };
            _materialsLayout.Controls.Add(_qualityPanel, 0, 2);

            _listMetals.SelectedIndexChanged += (s, e) =>
                ShowMaterialDetails(_config?.Materials?.Metais?.ElementAtOrDefault(_listMetals.SelectedIndex));

            _listCrystals.SelectedIndexChanged += (s, e) =>
                ShowMaterialDetails(_config?.Materials?.Cristais?.ElementAtOrDefault(_listCrystals.SelectedIndex));

            _listGems.SelectedIndexChanged += (s, e) =>
                ShowMaterialDetails(_config?.Materials?.Gemas?.ElementAtOrDefault(_listGems.SelectedIndex));

            _txtDetails = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(6, 14, 24),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9)
            };
            _contentVerticalSplit.Panel2.Controls.Add(_txtDetails);

            _footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(8, 18, 30),
                Padding = new Padding(8, 2, 8, 2)
            };
            Controls.Add(_footerPanel);

            _lblStatus = new Label
            {
                ForeColor = Color.FromArgb(170, 185, 210),
                Dock = DockStyle.Left,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _footerPanel.Controls.Add(_lblStatus);

            // Version label in footer
            _lblVersion = new Label
            {
                ForeColor = Color.FromArgb(120, 130, 150),
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Text = $"v{UpdateService.GetCurrentVersion()}"
            };
            _footerPanel.Controls.Add(_lblVersion);

            ApplyLocaleTexts();
            lblAlvo.Text = _i18n[_currentLocale]["label.target"];
        }

        private void ApplyLocaleTexts()
        {
            if (!_i18n.TryGetValue(_currentLocale, out var t))
                t = _i18n["pt-BR"];

            Text = t["title"];
            _lblTitle.Text = t["title"];

            UpdateMaterialHeadersFromScene();
            _lblStatus.Text = t["status.ready"];

            _btnOpen.Text = t["btn.open"];
            _btnLive.Text = t["btn.live"];
            _btnStopLive.Text = t["btn.stop"];
            _btnAnalyze.Text = t["btn.analyze"];
            _btnContinuous.Text = t["btn.cont"];
            _btnStopContinuous.Text = t["btn.cont.stop"];
            _btnMask.Text = t["btn.mask"];
            _btnMaskBg.Text = t["btn.mask.bg"];
            _btnPhaseMap.Text = t["btn.phase.map"];
            _btnHeatmap.Text = t["btn.heatmap"];
            _btnTraining.Text = t["btn.training"];
            _btnAi.Text = t["btn.ai"];
            _btnZoomIn.Text = t["btn.zoom.in"];
            _btnZoomOut.Text = t["btn.zoom.out"];
            _btnWB.Text = t["btn.wb"];
            _btnScale.Text = t["btn.scale"];
            _btnCameraSel.Text = t["btn.camera"];
            _btnResolucaoSel.Text = t["btn.res"];
            _btnTxt.Text = t["btn.txt"];
            _btnPdf.Text = t["btn.pdf"];
            _btnJson.Text = t["btn.json"];
            _btnCsv.Text = t["btn.csv"];
            _btnBiCsv.Text = t["btn.bi.csv"];
            _btnExportIa.Text = t["btn.export.ia"];
            _btnQaPanel.Text = t["btn.qa.panel"];
            _btnDebugHvs.Text = t["btn.debug"];
            _btnCalib.Text = t["btn.calib"];
            _btnLanguage.Text = $"Idioma ({_currentLocale}) ▾";

            // Update training mode button state
            UpdateTrainingModeButton();
        }

        private void InitializeCameraEvents()
        {
            _microscopeCamera.FrameReceived += MicroscopeCamera_FrameReceived;
        }

        private void MicroscopeCamera_FrameReceived(Bitmap frame)
        {
            try
            {
                if (!IsHandleCreated)
                {
                    frame.Dispose();
                    return;
                }

                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var old = _pictureSample.Image;
                        _pictureSample.Image = (Bitmap)frame.Clone();
                        old?.Dispose();

                        ApplyZoom();
                    }
                    finally
                    {
                        frame.Dispose();
                    }
                }));
            }
            catch
            {
                frame.Dispose();
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            try { _microscopeCamera.Dispose(); } catch { }
            try { _continuousController?.Stop(); } catch { }
        }

        private void BtnLanguage_Click(object? sender, EventArgs e)
        {
            _languageMenu.Show(_btnLanguage, new Point(0, _btnLanguage.Height));
        }

        private void SetLanguage(string locale)
        {
            if (!_i18n.ContainsKey(locale))
                locale = "pt-BR";

            _currentLocale = locale;
            ApplyLocaleTexts();
            AppendLog($"Idioma definido: {locale}");
        }

        private void BtnOpenImage_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar imagem de amostra",
                Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Todos os arquivos|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    using var bmp = new Bitmap(dlg.FileName);
                    _pictureSample.Image?.Dispose();
                    _pictureSample.Image = (Bitmap)bmp.Clone();
                    _zoomFactor = 1.0f;
                    _lastBaseImageClone?.Dispose();
                    _lastBaseImageClone = (Bitmap)bmp.Clone();
                    _lastScene = null;
                    SetViewMode(ViewMode.Original);
                    ApplyZoom();
                    AppendLog($"Imagem carregada: {Path.GetFileName(dlg.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Erro ao carregar imagem:\n\n{ex.Message}", "Imagem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void AppendLog(string message)
        {
            if (_txtDetails == null) return;
            var time = DateTime.Now.ToString("HH:mm:ss");
            _txtDetails.AppendText($"[{time}] {message}\r\n");
        }

        /// <summary>
        /// Define o modo de visualização atual e atualiza o status na UI.
        /// PR7 - Fase 1: Controle claro de modo de visualização.
        /// </summary>
        private void SetViewMode(ViewMode mode, string? targetMaterial = null)
        {
            _currentViewMode = mode;
            _currentTargetMaterial = targetMaterial;
            
            // Atualizar label de status com descrição do modo
            string description = VisualizationService.GetViewModeDescription(mode, targetMaterial);
            AppendLog(description);
        }

        private void BtnLive_Click(object? sender, EventArgs e)
        {
            if (_liveRunning)
            {
                AppendLog("Live já está ativo.");
                return;
            }

            try
            {
                _microscopeCamera.DeviceIndex = _cameraIndex;
                _microscopeCamera.Width = _cameraWidth;
                _microscopeCamera.Height = _cameraHeight;
                _microscopeCamera.Start();

                _liveRunning = true;
                AppendLog($"Live microscópio iniciado – câmera {_cameraIndex}, {_cameraWidth}x{_cameraHeight}.");
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao iniciar Live microscópio: {ex.Message}");
                _liveRunning = false;
            }
        }

        private void BtnParar_Click(object? sender, EventArgs e)
        {
            if (!_liveRunning)
            {
                AppendLog("Live não está ativo.");
                return;
            }

            try
            {
                _microscopeCamera.Stop();
                _liveRunning = false;
                AppendLog("Live microscópio parado.");
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao parar Live microscópio: {ex.Message}");
            }
        }

        private void BtnSelecionarCamera_Click(object? sender, EventArgs e)
        {
            using var form = new Form
            {
                Text = "Selecionar câmera",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(400, 160),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var lbl = new Label
            {
                Text = "Escolha o índice da câmera (0, 1, 2...)",
                AutoSize = true,
                Location = new Point(12, 15)
            };
            var combo = new ComboBox
            {
                Location = new Point(15, 40),
                Width = 80,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            for (int i = 0; i < 4; i++) combo.Items.Add(i);
            combo.SelectedItem = _cameraIndex;
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(220, 80), Width = 70 };
            var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(300, 80), Width = 70 };
            form.Controls.Add(lbl); form.Controls.Add(combo); form.Controls.Add(btnOk); form.Controls.Add(btnCancel);
            form.AcceptButton = btnOk; form.CancelButton = btnCancel;
            if (form.ShowDialog(this) == DialogResult.OK && combo.SelectedItem is int idx)
            {
                _cameraIndex = idx;
                AppendLog($"Câmera selecionada: índice {_cameraIndex}");
            }
        }

        private void BtnSelecionarResolucao_Click(object? sender, EventArgs e)
        {
            using var form = new Form
            {
                Text = "Selecionar resolução",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(420, 220),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var lbl = new Label
            {
                Text = "Escolha uma resolução padrão:",
                AutoSize = true,
                Location = new Point(12, 15)
            };
            var combo = new ComboBox
            {
                Location = new Point(15, 40),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            string[] presets = new[] { "640x480", "800x600", "1280x720", "1920x1080" };
            foreach (var p in presets) combo.Items.Add(p);
            string atual = $"{_cameraWidth}x{_cameraHeight}";
            if (combo.Items.Contains(atual)) combo.SelectedItem = atual; else combo.SelectedIndex = 3;

            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(220, 150), Width = 70 };
            var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(300, 150), Width = 70 };
            form.Controls.Add(lbl); form.Controls.Add(combo); form.Controls.Add(btnOk); form.Controls.Add(btnCancel);
            form.AcceptButton = btnOk; form.CancelButton = btnCancel;

            if (form.ShowDialog(this) == DialogResult.OK && combo.SelectedItem is string sel)
            {
                var parts = sel.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                {
                    _cameraWidth = w; _cameraHeight = h;
                    AppendLog($"Resolução selecionada: {_cameraWidth}x{_cameraHeight}");
                }
            }
        }

        private void BtnAnalisar_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_analysisService == null || _config == null)
                {
                    AppendLog("⚠️ Serviço de análise HVS não está inicializado (config ausente).");
                    return;
                }
                if (_pictureSample.Image == null)
                {
                    AppendLog("⚠️ Nenhuma imagem disponível para análise.");
                    return;
                }

                // Verificar estado da câmera se em live mode
                if (_liveRunning)
                {
                    AppendLog("📷 Capturando frame da câmera para análise...");
                }

                using var bmp = new Bitmap(_pictureSample.Image);

                // Usar AnalyzeScene para obter a cena completa com Labels
                var scene = _analysisService.AnalyzeScene(bmp, imagePath: null);
                
                // Se QualityStatus for Invalid, executar reanálise automática
                if (string.Equals(scene.Summary.QualityStatus, "Invalid", StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("🔄 Qualidade baixa detectada, executando reanálise automática...");
                    var reanalyzedSummary = _analysisService.RunWithAutoReanalysis(bmp, imagePath: null);
                    scene.Summary = reanalyzedSummary;
                }

                _lastScene = scene;

                _lastBaseImageClone?.Dispose();
                _lastBaseImageClone = (Bitmap)bmp.Clone();
                SetViewMode(ViewMode.Original);

                _txtDetails.Text = scene.Summary.ShortReport;
                UpdateMaterialListsFromScene();
                
                var s = scene.Summary;
                string materiaisEncontrados = s.Metals.Count + s.Crystals.Count + s.Gems.Count > 0 
                    ? $"✅ {s.Metals.Count} metais, {s.Crystals.Count} cristais, {s.Gems.Count} gemas" 
                    : "⚠️ Nenhum material detectado";
                
                AppendLog($"Análise HVS completa: {materiaisEncontrados}");

                _lblStatus.Text =
                    $"Qualidade: {s.QualityIndex:F1} ({s.QualityStatus}) · Foco={s.Diagnostics.FocusScorePercent:F1} · Exposição={s.Diagnostics.ExposureScore:F1} · Máscara={s.Diagnostics.MaskScore:F1}";

                // Update quality checklist panel
                _qualityPanel.UpdateFromDiagnostics(s.Diagnostics);

                // Clear last exported report path since we have new analysis
                _lastExportedReportPath = null;
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Erro ao executar análise: {ex.Message}");
            }
        }

        // Export BI CSV - Using new BiExportService
        private void BtnBiCsv_Click(object? sender, EventArgs e)
        {
            if (_lastScene?.Summary == null)
            {
                AppendLog("Nenhuma análise disponível para exportar BI CSV.");
                return;
            }

            try
            {
                if (_biExportService == null)
                {
                    _biExportService = new BiExportService(_appSettings);
                }

                string sampleName = _appSettings.DefaultSampleName;
                string clientProject = _appSettings.DefaultClientProject;
                string captureMode = _liveRunning ? "Live" : (_continuousRunning ? "Continuous" : "Image");

                var path = _biExportService.ExportToBiCsv(
                    _lastScene.Summary,
                    sampleName,
                    clientProject,
                    captureMode,
                    null);

                AppendLog($"BI CSV consolidado exportado: {path}");
            }
            catch (Exception ex)
            {
                AppendLog("Erro ao exportar BI CSV: " + ex.Message);
            }
        }

        // Export dataset IA por partícula - Using new IaDatasetService
        private void BtnExportIa_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_lastScene?.Summary == null || _lastBaseImageClone == null)
                {
                    AppendLog("Nenhuma análise/imagem disponível para exportar dataset IA.");
                    return;
                }

                var s = _lastScene.Summary;
                if (s.Particles == null || s.Particles.Count == 0)
                {
                    AppendLog("Nenhuma partícula registrada para exportar dataset IA.");
                    return;
                }

                if (_iaDatasetService == null)
                {
                    _iaDatasetService = new IaDatasetService(_appSettings);
                }

                var result = _iaDatasetService.ExportDataset(
                    _lastScene,
                    _lastBaseImageClone,
                    _appSettings.DefaultOperator);

                if (result.Success)
                {
                    AppendLog($"✅ Dataset IA exportado: {result.ExportedCount} partículas em {result.OutputDirectory}");
                    AppendLog($"   Índice CSV: {result.CsvIndexPath}");
                    if (result.SkippedCount > 0)
                    {
                        AppendLog($"   ⚠️ {result.SkippedCount} partículas ignoradas (muito pequenas)");
                    }
                }
                else
                {
                    AppendLog($"❌ Falha ao exportar dataset IA: {result.Message}");
                }

                if (result.Errors.Count > 0)
                {
                    foreach (var error in result.Errors.Take(5))
                    {
                        AppendLog($"   Erro: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("Erro ao exportar dataset IA: " + ex.Message);
            }
        }

        /// <summary>
        /// Análise seletiva unificada (PR7 - Fase 1).
        /// Funciona igualmente para imagem estática, frame de câmera e modo contínuo.
        /// Usa a classificação base existente - apenas FILTRA para o alvo selecionado.
        /// </summary>
        private void BtnSelectiveAnalyze_Click(object? sender, EventArgs e)
        {
            try
            {
                // Verificar se há uma análise disponível
                if (_lastScene == null)
                {
                    // Se estiver em live mode e tiver imagem, tenta fazer análise primeiro
                    if (_pictureSample.Image != null && _analysisService != null)
                    {
                        AppendLog("Executando análise antes da análise seletiva...");
                        using var bmp = new Bitmap(_pictureSample.Image);
                        var scene = _analysisService.AnalyzeScene(bmp, null);
                        _lastScene = scene;
                        _lastBaseImageClone?.Dispose();
                        _lastBaseImageClone = (Bitmap)bmp.Clone();
                    }
                    else
                    {
                        AppendLog("⚠️ Nenhuma análise disponível para análise seletiva. Abra uma imagem ou use Analisar primeiro.");
                        return;
                    }
                }
                
                if (_cbTarget == null || _cbTarget.SelectedItem == null)
                {
                    AppendLog("⚠️ Nenhum alvo selecionado para análise seletiva. Selecione um alvo no combo 'Alvo'.");
                    return;
                }

                // PR7 - Fase 1: Aviso de foco ruim
                var diag = _lastScene.Summary.Diagnostics;
                bool focusIsBad = string.Equals(diag.QualityStatus, "Invalid", StringComparison.OrdinalIgnoreCase) ||
                                  diag.FocusScorePercent < 30.0;
                if (focusIsBad)
                {
                    AppendLog("⚠️ ATENÇÃO: foco ruim detectado – resultados seletivos apenas indicativos.");
                }

                string alvoTexto = _cbTarget.SelectedItem.ToString() ?? "(desconhecido)";
                AppendLog($"🎯 Análise seletiva para: {alvoTexto}");

                var summary = _lastScene.Summary;
                var sb = new StringBuilder();
                sb.AppendLine($"🎯 Análise seletiva – {alvoTexto}");
                sb.AppendLine("--------------------------------");

                // Extrair informações do alvo
                string? targetId = null;
                int tipoAlvo = -1;
                double pctSample = 0;
                string? targetName = null;

                if (alvoTexto.StartsWith("Metal:", StringComparison.OrdinalIgnoreCase))
                {
                    string name = alvoTexto.Substring("Metal:".Length).Trim();
                    var m = summary.Metals.Find(x =>
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Id, name, StringComparison.OrdinalIgnoreCase));

                    if (m != null)
                    {
                        var ppm = m.PpmEstimated.HasValue ? $"{m.PpmEstimated.Value:F1} ppm" : "-";
                        sb.AppendLine($"ID: {m.Id}");
                        sb.AppendLine($"Nome: {m.Name}");
                        sb.AppendLine($"Grupo: {m.Group}");
                        sb.AppendLine($"Fração na amostra: {m.PctSample:P4}");
                        sb.AppendLine($"PPM estimado: {ppm}");
                        sb.AppendLine($"Score combinado: {m.Score:F3}");
                        targetId = m.Id;
                        targetName = m.Name;
                        pctSample = m.PctSample;
                        tipoAlvo = 0;
                    }
                    else
                    {
                        // Tentar encontrar pelo nome no combo (pode não estar no resultado atual)
                        targetId = ExtractMaterialIdFromName(name);
                        targetName = name;
                        tipoAlvo = 0;
                        sb.AppendLine($"⚠️ Metal '{name}' não encontrado na última análise (0% na amostra).");
                        sb.AppendLine("Metais disponíveis:");
                        foreach (var metal in summary.Metals.OrderByDescending(x => x.PctSample).Take(5))
                        {
                            sb.AppendLine($"  - {metal.Name} ({metal.Id}): {metal.PctSample:P3}");
                        }
                    }
                }
                else if (alvoTexto.StartsWith("Cristal:", StringComparison.OrdinalIgnoreCase))
                {
                    string name = alvoTexto.Substring("Cristal:".Length).Trim();
                    var c = summary.Crystals.Find(x =>
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Id, name, StringComparison.OrdinalIgnoreCase));
                    if (c != null)
                    {
                        sb.AppendLine($"ID: {c.Id}");
                        sb.AppendLine($"Nome: {c.Name}");
                        sb.AppendLine($"Fração na amostra: {c.PctSample:P4}");
                        sb.AppendLine($"Score combinado: {c.Score:F3}");
                        targetId = c.Id;
                        targetName = c.Name;
                        pctSample = c.PctSample;
                        tipoAlvo = 1;
                    }
                    else
                    {
                        targetId = ExtractMaterialIdFromName(name);
                        targetName = name;
                        tipoAlvo = 1;
                        sb.AppendLine($"⚠️ Cristal '{name}' não encontrado na última análise.");
                    }
                }
                else if (alvoTexto.StartsWith("Gema:", StringComparison.OrdinalIgnoreCase))
                {
                    string name = alvoTexto.Substring("Gema:".Length).Trim();
                    var g = summary.Gems.Find(x =>
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Id, name, StringComparison.OrdinalIgnoreCase));
                    if (g != null)
                    {
                        sb.AppendLine($"ID: {g.Id}");
                        sb.AppendLine($"Nome: {g.Name}");
                        sb.AppendLine($"Fração na amostra: {g.PctSample:P4}");
                        sb.AppendLine($"Score combinado: {g.Score:F3}");
                        targetId = g.Id;
                        targetName = g.Name;
                        pctSample = g.PctSample;
                        tipoAlvo = 2;
                    }
                    else
                    {
                        targetId = ExtractMaterialIdFromName(name);
                        targetName = name;
                        tipoAlvo = 2;
                        sb.AppendLine($"⚠️ Gema '{name}' não encontrada na última análise.");
                    }
                }

                // Add focus warning to details if needed
                if (focusIsBad)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠️ ATENÇÃO: foco ruim – resultados apenas indicativos.");
                }

                _txtDetails.Text = sb.ToString();

                if (string.IsNullOrWhiteSpace(targetId))
                {
                    AppendLog("⚠️ Nenhum ID de alvo encontrado no resumo. Nada para destacar.");
                    return;
                }

                if (_lastBaseImageClone == null)
                {
                    AppendLog("⚠️ Imagem base ausente para máscara seletiva.");
                    return;
                }

                // PR7 - Fase 1: Usar método unificado BuildSelectiveView
                using var baseImg = new Bitmap(_lastBaseImageClone);
                var selective = VisualizationService.BuildSelectiveView(
                    baseImg,
                    _lastScene,
                    targetId,
                    tipoAlvo,
                    confidenceThreshold: 0.5);

                if (selective != null)
                {
                    _pictureSample.Image?.Dispose();
                    _pictureSample.Image = (Bitmap)selective.Clone();
                    
                    // Atualizar ViewMode e status
                    _currentTargetMaterial = targetName ?? targetId;
                    SetViewMode(ViewMode.SeletivaAlvo, _currentTargetMaterial);
                    
                    _zoomFactor = 1.0f;
                    ApplyZoom();
                    
                    if (pctSample > 0)
                    {
                        AppendLog($"✅ Análise seletiva aplicada para '{targetId}' ({pctSample:P2} da amostra).");
                    }
                    else
                    {
                        AppendLog($"ℹ️ Análise seletiva para '{targetId}': 0% detectado nesta amostra.");
                    }
                }
                else
                {
                    AppendLog("❌ Falha ao gerar visualização seletiva.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Erro na análise seletiva: {ex.Message}");
            }
        }

        /// <summary>
        /// Extrai ID do material a partir do nome (fallback para quando não está no resultado).
        /// </summary>
        private string ExtractMaterialIdFromName(string name)
        {
            // Mapeamento comum de nomes para IDs
            var nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ouro"] = "Au",
                ["Gold"] = "Au",
                ["Prata"] = "Ag",
                ["Silver"] = "Ag",
                ["Platina"] = "Pt",
                ["Platinum"] = "Pt",
                ["Paládio"] = "Pd",
                ["Palladium"] = "Pd",
                ["Cobre"] = "Cu",
                ["Copper"] = "Cu",
                ["Ferro"] = "Fe",
                ["Iron"] = "Fe",
                ["Níquel"] = "Ni",
                ["Nickel"] = "Ni",
                ["Zinco"] = "Zn",
                ["Zinc"] = "Zn",
                ["Alumínio"] = "Al",
                ["Aluminum"] = "Al",
                ["Chumbo"] = "Pb",
                ["Lead"] = "Pb",
                ["Quartzo"] = "SiO2",
                ["Quartz"] = "SiO2",
                ["Calcita"] = "CaCO3",
                ["Calcite"] = "CaCO3",
                ["Diamante"] = "C",
                ["Diamond"] = "C",
                ["Safira"] = "Al2O3_blue",
                ["Sapphire"] = "Al2O3_blue",
                ["Rubi"] = "Al2O3_red",
                ["Ruby"] = "Al2O3_red",
                ["Esmeralda"] = "Be3Al2Si6O18",
                ["Emerald"] = "Be3Al2Si6O18",
                ["Ametista"] = "SiO2_purple",
                ["Amethyst"] = "SiO2_purple"
            };

            if (nameToId.TryGetValue(name, out var id))
                return id;

            // Se não encontrar, usar o nome como ID
            return name;
        }

        private void BtnMascara_Click(object? sender, EventArgs e)
        {
            if (_lastScene == null || _lastScene.MaskPreview == null)
            {
                AppendLog("Nenhuma máscara disponível (execute uma análise primeiro).");
                return;
            }

            // Toggle mask view
            if (_currentViewMode == ViewMode.MaskGlobal)
            {
                // Return to original
                if (_lastBaseImageClone != null)
                {
                    _pictureSample.Image?.Dispose();
                    _pictureSample.Image = (Bitmap)_lastBaseImageClone.Clone();
                }
                SetViewMode(ViewMode.Original);
            }
            else
            {
                _pictureSample.Image?.Dispose();
                _pictureSample.Image = (Bitmap)_lastScene.MaskPreview.Clone();
                SetViewMode(ViewMode.MaskGlobal);
            }

            _zoomFactor = 1.0f;
            ApplyZoom();
        }

        private void BtnFundoMasc_Click(object? sender, EventArgs e)
        {
            if (_lastBaseImageClone == null)
            {
                AppendLog("Nenhuma imagem base para mascarar fundo (execute uma análise).");
                return;
            }

            // Toggle masked background view
            if (_currentViewMode == ViewMode.FundoMascarado)
            {
                // Return to original
                _pictureSample.Image?.Dispose();
                _pictureSample.Image = (Bitmap)_lastBaseImageClone.Clone();
                SetViewMode(ViewMode.Original);
            }
            else
            {
                using var baseImg = new Bitmap(_lastBaseImageClone);
                Bitmap? mp = _lastScene?.MaskPreview;
                var masked = VisualizationService.BuildBackgroundMaskedView(
                    baseImg,
                    _lastScene?.Mask,
                    mp);
                _pictureSample.Image?.Dispose();
                _pictureSample.Image = (Bitmap)masked.Clone();
                SetViewMode(ViewMode.FundoMascarado);
            }

            _zoomFactor = 1.0f;
            ApplyZoom();
        }

        // ===== Phase Map visualization =====
        private void BtnPhaseMap_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_lastScene == null)
                {
                    AppendLog("Nenhuma análise disponível. Execute uma análise primeiro.");
                    return;
                }

                var phaseMap = _phaseMapService.GeneratePhaseMap(_lastScene);
                _pictureSample.Image?.Dispose();
                _pictureSample.Image = phaseMap;

                SetViewMode(ViewMode.MapaFases);
                _zoomFactor = 1.0f;
                ApplyZoom();
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao gerar mapa de fases: {ex.Message}");
            }
        }

        // ===== Target Heatmap visualization =====
        private void BtnHeatmap_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_lastScene == null || _lastBaseImageClone == null)
                {
                    AppendLog("Nenhuma análise disponível. Execute uma análise primeiro.");
                    return;
                }

                // Get selected target from combo
                string targetId = "Au"; // Default
                string targetName = "Ouro";
                if (_cbTarget?.SelectedItem != null)
                {
                    string sel = _cbTarget.SelectedItem.ToString() ?? "";
                    if (sel.Contains(":"))
                    {
                        targetName = sel.Split(':')[1].Trim();
                        targetId = ExtractMaterialIdFromName(targetName);
                    }
                }

                using var baseImg = new Bitmap(_lastBaseImageClone);
                var heatmap = _phaseMapService.GenerateTargetHeatmap(baseImg, _lastScene, targetId, 0.6);

                _pictureSample.Image?.Dispose();
                _pictureSample.Image = heatmap;

                _currentTargetMaterial = targetName;
                SetViewMode(ViewMode.HeatmapAlvo, _currentTargetMaterial);
                _zoomFactor = 1.0f;
                ApplyZoom();
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao gerar heatmap: {ex.Message}");
            }
        }

        // ===== Training Mode =====
        private void BtnTraining_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_trainingService.IsSessionActive)
                {
                    // End training session
                    var exportPath = _trainingService.EndSession();
                    if (!string.IsNullOrEmpty(exportPath))
                    {
                        AppendLog($"Sessão de treino finalizada. Dados exportados: {exportPath}");
                    }
                    else
                    {
                        AppendLog("Sessão de treino finalizada (sem dados).");
                    }

                    // Remove click handler from picture
                    _pictureSample.MouseClick -= PictureSample_TrainingClick;
                }
                else
                {
                    // Start training session
                    _trainingService.StartSession(_appSettings.DefaultOperator);
                    AppendLog("Modo de treino ativado. Clique em partículas para rotulá-las.");

                    // Add click handler for labeling
                    _pictureSample.MouseClick += PictureSample_TrainingClick;
                }

                UpdateTrainingModeButton();
            }
            catch (Exception ex)
            {
                AppendLog($"Erro no modo de treino: {ex.Message}");
            }
        }

        private void UpdateTrainingModeButton()
        {
            if (_btnTraining == null) return;

            if (_trainingService.IsSessionActive)
            {
                _btnTraining.BackColor = Color.FromArgb(120, 60, 60);
                _btnTraining.Text = "⏹ Parar Treino";
            }
            else
            {
                _btnTraining.BackColor = Color.FromArgb(20, 40, 65);
                _btnTraining.Text = _i18n[_currentLocale]["btn.training"];
            }
        }

        private void PictureSample_TrainingClick(object? sender, MouseEventArgs e)
        {
            if (!_trainingService.IsSessionActive || _lastScene == null)
                return;

            // Translate mouse coordinates to image coordinates
            if (!TryTranslateToImagePoint(_pictureSample, e.Location, out var imgPt))
            {
                AppendLog("Clique fora da imagem.");
                return;
            }

            // Show label selection dialog
            using var dlg = new Form
            {
                Text = "Rotular Partícula",
                Size = new Size(300, 200),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lbl = new Label
            {
                Text = $"Posição: ({imgPt.X}, {imgPt.Y})\nSelecione o material:",
                AutoSize = true,
                Location = new Point(12, 12)
            };
            dlg.Controls.Add(lbl);

            var combo = new ComboBox
            {
                Location = new Point(12, 60),
                Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var label in TrainingModeService.AvailableLabels)
            {
                combo.Items.Add(label);
            }
            combo.SelectedIndex = 0;
            dlg.Controls.Add(combo);

            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(100, 120), Width = 80 };
            var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(190, 120), Width = 80 };
            dlg.Controls.Add(btnOk);
            dlg.Controls.Add(btnCancel);
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;

            if (dlg.ShowDialog(this) == DialogResult.OK && combo.SelectedItem != null)
            {
                string selectedLabel = combo.SelectedItem.ToString() ?? "Unknown";
                _trainingService.AddExampleFromClick(imgPt.X, imgPt.Y, selectedLabel, _lastScene);

                var (total, byLabel) = _trainingService.GetSessionStats();
                AppendLog($"Partícula rotulada como '{selectedLabel}'. Total na sessão: {total}");
            }
        }

        private void BtnParticulas_Click(object? sender, EventArgs e)
        {
            try
            {
                string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datasets", "hvs-calibration");
                if (!Directory.Exists(baseDir))
                {
                    AppendLog("Nenhum dataset IA encontrado (pasta hvs-calibration inexistente).");
                    return;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = baseDir,
                    UseShellExecute = true
                });
                AppendLog("Abrindo pasta de datasets IA: " + baseDir);
            }
            catch (Exception ex)
            {
                AppendLog("Erro ao abrir pasta de datasets IA: " + ex.Message);
            }
        }

        private void BtnZoomMais_Click(object? sender, EventArgs e)
        {
            _zoomFactor = Math.Min(8f, _zoomFactor * 1.25f);
            ApplyZoom();
            AppendLog($"Zoom: {_zoomFactor:F2}x");
        }

        private void BtnZoomMenos_Click(object? sender, EventArgs e)
        {
            _zoomFactor = Math.Max(0.125f, _zoomFactor / 1.25f);
            ApplyZoom();
            AppendLog($"Zoom: {_zoomFactor:F2}x");
        }

        private void ApplyZoom()
        {
            if (_pictureSample.Image == null) return;

            var img = _pictureSample.Image;
            int newW = (int)(img.Width * _zoomFactor);
            int newH = (int)(img.Height * _zoomFactor);

            _pictureSample.SizeMode = PictureBoxSizeMode.Normal;
            _pictureSample.Size = new Size(newW, newH);
            _imagePanel.AutoScrollMinSize = _pictureSample.Size;

            if (newW < _imagePanel.ClientSize.Width && newH < _imagePanel.ClientSize.Height)
            {
                _pictureSample.Location = new Point(
                    (_imagePanel.ClientSize.Width - newW) / 2,
                    (_imagePanel.ClientSize.Height - newH) / 2
                );
            }
            else
            {
                _pictureSample.Location = new Point(0, 0);
            }
        }

        private void BtnBalanco_Click(object? sender, EventArgs e)
        {
            AppendLog("Balanço de branco (placeholder).");
        }

        private void BtnEscala_Click(object? sender, EventArgs e)
        {
            AppendLog("Ferramenta de escala (placeholder).");
        }

        /// <summary>
        /// Generate a text report with version and lab information.
        /// </summary>
        private string BuildFullTextReport()
        {
            if (_lastScene?.Summary == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"Versão do MicroLab: v{UpdateService.GetCurrentVersion()}");
            sb.AppendLine($"Laboratório: {_appSettings.LabName}");
            if (!string.IsNullOrWhiteSpace(_appSettings.DefaultOperator))
                sb.AppendLine($"Operador: {_appSettings.DefaultOperator}");
            sb.AppendLine();
            sb.Append(_lastScene.Summary.ShortReport);
            return sb.ToString();
        }

        /// <summary>
        /// Export text report to file and return the path.
        /// </summary>
        private string? ExportTextReport(string filePrefix = "analysis")
        {
            if (_lastScene?.Summary == null)
                return null;

            string dir = !string.IsNullOrWhiteSpace(_appSettings.ReportsDirectory)
                ? _appSettings.ReportsDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{filePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, BuildFullTextReport(), Encoding.UTF8);
            _lastExportedReportPath = path;
            return path;
        }

        private void BtnTxt_Click(object? sender, EventArgs e)
        {
            if (_lastScene?.Summary == null)
            {
                AppendLog("Nenhuma análise disponível para exportar TXT.");
                return;
            }

            try
            {
                if (_reportService == null)
                {
                    _reportService = new ReportService(_appSettings);
                }

                string sampleName = _appSettings.DefaultSampleName;
                string clientProject = _appSettings.DefaultClientProject;

                var path = _reportService.ExportTxtReport(_lastScene.Summary, sampleName, clientProject);
                _lastExportedReportPath = path;
                AppendLog("Laudo TXT exportado: " + path);
            }
            catch (Exception ex)
            {
                AppendLog("Erro ao exportar laudo TXT: " + ex.Message);
            }
        }

        private void BtnPdf_Click(object? sender, EventArgs e)
        {
            if (_lastScene?.Summary == null)
            {
                AppendLog("Nenhuma análise disponível para exportar PDF.");
                return;
            }

            try
            {
                if (_reportService == null)
                {
                    _reportService = new ReportService(_appSettings);
                }

                string sampleName = _appSettings.DefaultSampleName;
                string clientProject = _appSettings.DefaultClientProject;

                var path = _reportService.ExportPdfReport(_lastScene.Summary, sampleName, clientProject);
                _lastExportedReportPath = path;
                AppendLog("Laudo PDF exportado: " + path);
            }
            catch (Exception ex)
            {
                AppendLog("Erro ao exportar laudo PDF: " + ex.Message);
            }
        }

        private void BtnQaPanel_Click(object? sender, EventArgs e)
        {
            if (_lastScene?.Summary == null || _lastBaseImageClone == null)
            {
                AppendLog("Nenhuma análise disponível para QA. Execute uma análise primeiro.");
                return;
            }

            if (_lastScene.Summary.Particles == null || _lastScene.Summary.Particles.Count == 0)
            {
                AppendLog("Nenhuma partícula detectada para QA.");
                return;
            }

            try
            {
                using var qaPanel = new QaParticlePanel(_lastScene, _lastBaseImageClone, _appSettings);
                qaPanel.ShowDialog(this);
                AppendLog("Sessão de QA de partículas finalizada.");
            }
            catch (Exception ex)
            {
                AppendLog("Erro ao abrir painel de QA: " + ex.Message);
            }
        }

        private void BtnJson_Click(object? sender, EventArgs e)
        {
            if (_lastScene?.Summary == null)
            {
                AppendLog("Nenhuma análise disponível para exportar JSON.");
                return;
            }

            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
                Directory.CreateDirectory(dir);

                var s = _lastScene.Summary;
                var obj = new
                {
                    id = s.Id,
                    utc = s.CaptureDateTimeUtc.ToString("o"),
                    diagnostics = s.Diagnostics,
                    metals = s.Metals,
                    crystals = s.Crystals,
                    gems = s.Gems,
                    quality = new
                    {
                        s.QualityIndex,
                        s.QualityStatus
                    },
                    particles = s.Particles
                };
                string json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
                string path = Path.Combine(dir, "analysis_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json");
                File.WriteAllText(path, json, Encoding.UTF8);
                AppendLog("Exportado JSON: " + path);
            }
            catch (Exception ex)
            {
                AppendLog("Erro ao exportar JSON: " + ex.Message);
            }
        }

        private void BtnCsv_Click(object? sender, EventArgs e)
        {
            if (_lastScene?.Summary == null)
            {
                AppendLog("Nenhuma análise disponível para exportar CSV.");
                return;
            }

            try
            {
                var s = _lastScene.Summary;

                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
                Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("Tipo,Id,Nome,Grupo,PctSample,PPM,Score");
                foreach (var m in s.Metals)
                {
                    string ppm = m.PpmEstimated.HasValue ? m.PpmEstimated.Value.ToString("F1") : "";
                    sb.AppendLine($"Metal,{m.Id},{m.Name},{m.Group},{m.PctSample:F6},{ppm},{m.Score:F4}");
                }
                foreach (var c in s.Crystals)
                {
                    sb.AppendLine($"Cristal,{c.Id},{c.Name},,{c.PctSample:F6},,{c.Score:F4}");
                }
                foreach (var g in s.Gems)
                {
                    sb.AppendLine($"Gema,{g.Id},{g.Name},,{g.PctSample:F6},,{g.Score:F4}");
                }

                string path = Path.Combine(dir, "analysis_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".csv");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                AppendLog("Exportado CSV: " + path);
            }
            catch (Exception ex)
            {
                AppendLog("Erro ao exportar CSV: " + ex.Message);
            }
        }

        private void UpdateMaterialListsFromScene()
        {
            if (_lastScene?.Summary == null) return;

            var s = _lastScene.Summary;

            _listMetals.BeginUpdate();
            _listMetals.Items.Clear();
            foreach (var m in s.Metals.OrderByDescending(x => x.PctSample))
            {
                string ppm = m.PpmEstimated.HasValue ? $"{m.PpmEstimated.Value:F1} ppm" : "-";
                string line = $"{m.Name} ({m.Id}) – {m.PctSample:P3} | {ppm} | score={m.Score:F2}";
                _listMetals.Items.Add(line);
            }
            _listMetals.EndUpdate();

            _listCrystals.BeginUpdate();
            _listCrystals.Items.Clear();
            foreach (var c in s.Crystals.OrderByDescending(x => x.PctSample))
            {
                string line = $"{c.Name} ({c.Id}) – {c.PctSample:P3} | score={c.Score:F2}";
                _listCrystals.Items.Add(line);
            }
            _listCrystals.EndUpdate();

            _listGems.BeginUpdate();
            _listGems.Items.Clear();
            foreach (var g in s.Gems.OrderByDescending(x => x.PctSample))
            {
                string line = $"{g.Name} ({g.Id}) – {g.PctSample:P3} | score={g.Score:F2}";
                _listGems.Items.Add(line);
            }
            _listGems.EndUpdate();

            UpdateMaterialHeadersFromScene();
        }

        private void UpdateMaterialHeadersFromScene()
        {
            if (_lastScene?.Summary == null)
            {
                _lblMetalsHeader.Text = $"{_i18n[_currentLocale]["metals"]} (0)";
                _lblCrystalsHeader.Text = $"{_i18n[_currentLocale]["crystals"]} (0)";
                _lblGemsHeader.Text = $"{_i18n[_currentLocale]["gems"]} (0)";
                return;
            }

            var s = _lastScene.Summary;
            _lblMetalsHeader.Text = $"{_i18n[_currentLocale]["metals"]} ({s.Metals.Count})";
            _lblCrystalsHeader.Text = $"{_i18n[_currentLocale]["crystals"]} ({s.Crystals.Count})";
            _lblGemsHeader.Text = $"{_i18n[_currentLocale]["gems"]} ({s.Gems.Count})";
        }

        private void PictureSample_MouseMove(object? sender, MouseEventArgs e)
        {
            try
            {
                if (_pictureSample.Image == null || _lastScene == null) return;
                if (!TryTranslateToImagePoint(_pictureSample, e.Location, out var imgPt)) return;

                if (imgPt.X < 0 || imgPt.Y < 0 ||
                    imgPt.X >= _lastScene.Width ||
                    imgPt.Y >= _lastScene.Height)
                    return;

                var lbl = _lastScene.Labels[imgPt.X, imgPt.Y];
                if (lbl == null || !lbl.IsSample || lbl.MaterialType == PixelMaterialType.Background)
                {
                    _hvsToolTip.ToolTipTitle = "HVS Detector";
                    _hvsToolTip.Show("Fundo / fora da amostra", _pictureSample, e.Location.X + 16, e.Location.Y + 16, 1200);
                    return;
                }

                string tipo = lbl.MaterialType switch
                {
                    PixelMaterialType.Metal => "Metal",
                    PixelMaterialType.Crystal => "Cristal",
                    PixelMaterialType.Gem => "Gema",
                    _ => "Amostra"
                };

                string id = lbl.MaterialId ?? "(desconhecido)";
                string text =
                    $"{tipo}: {id} | conf={lbl.MaterialConfidence:F2} | H={lbl.H:F1} S={lbl.S:F2} V={lbl.V:F2}";

                _hvsToolTip.ToolTipTitle = "HVS Detector";
                _hvsToolTip.Show(text, _pictureSample, e.Location.X + 16, e.Location.Y + 16, 1500);
            }
            catch { }
        }

        private bool TryTranslateToImagePoint(PictureBox pb, Point mousePt, out Point imgPt)
        {
            imgPt = new Point(-1, -1);
            if (pb.Image == null) return false;

            var img = pb.Image;
            int iw = img.Width, ih = img.Height;

            var scroll = _imagePanel.AutoScrollPosition;
            int x = (int)((mousePt.X - pb.Left - scroll.X) / _zoomFactor);
            int y = (int)((mousePt.Y - pb.Top - scroll.Y) / _zoomFactor);

            if (x < 0 || y < 0 || x >= iw || y >= ih) return false;
            imgPt = new Point(x, y);
            return true;
        }

        private void BtnDebugHvs_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_lastScene == null)
                {
                    AppendLog("Nenhuma análise de cena para debug.");
                    return;
                }

                var s = _lastScene.Summary;
                var sb = new StringBuilder();
                sb.AppendLine("DEBUG HVS – Cena completa");
                sb.AppendLine($"Imagem: {s.ImagePath ?? "(memória)"}");
                sb.AppendLine($"Foco: {s.Diagnostics.FocusScore:F2}");
                sb.AppendLine($"ForegroundFraction: {s.Diagnostics.ForegroundFraction:P1}");
                sb.AppendLine($"Pixels: {_lastScene.Width}x{_lastScene.Height}");
                sb.AppendLine();
                sb.AppendLine("Metais:");
                foreach (var m in s.Metals.OrderByDescending(m => m.PctSample))
                {
                    sb.AppendLine($" - {m.Id} {m.Name}: {m.PctSample:P4}, ppm={m.PpmEstimated?.ToString("F1") ?? "-"}");
                }
                sb.AppendLine();
                sb.AppendLine("Cristais:");
                foreach (var c in s.Crystals.OrderByDescending(c => c.PctSample))
                {
                    sb.AppendLine($" - {c.Id} {c.Name}: {c.PctSample:P4}");
                }
                sb.AppendLine();
                sb.AppendLine("Gemas:");
                foreach (var g in s.Gems.OrderByDescending(g => g.PctSample))
                {
                    sb.AppendLine($" - {g.Id} {g.Name}: {g.PctSample:P4}");
                }

                _txtDetails.Text = sb.ToString();
                AppendLog("Debug HVS da cena gerado.");
            }
            catch (Exception ex)
            {
                AppendLog($"Erro no Debug HVS: {ex.Message}");
            }
        }

        private void BtnCalibrarAuto_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_analysisService == null || _config == null)
                {
                    AppendLog("Serviço de análise HVS não está inicializado.");
                    return;
                }
                if (_pictureSample.Image == null)
                {
                    AppendLog("Nenhuma imagem para snapshot.");
                    return;
                }

                using var bmp = new Bitmap(_pictureSample.Image);
                var scene = _analysisService.AnalyzeScene(bmp, imagePath: null);

                string alvo = (_cbTarget?.SelectedItem?.ToString() ?? "SemAlvo").Replace(':', '_').Replace(' ', '_');
                string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datasets", "hvs-calibration", alvo);
                Directory.CreateDirectory(baseDir);

                string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string imgPath = Path.Combine(baseDir, $"snapshot_{ts}.png");
                string maskPath = Path.Combine(baseDir, $"mask_{ts}.png");
                string reportPath = Path.Combine(baseDir, $"laudo_{ts}.json");

                bmp.Save(imgPath, System.Drawing.Imaging.ImageFormat.Png);
                scene.MaskPreview.Save(maskPath, System.Drawing.Imaging.ImageFormat.Png);

                var s = scene.Summary;
                var laudo = new
                {
                    id = s.Id,
                    utc = s.CaptureDateTimeUtc.ToString("o"),
                    alvo = alvo,
                    diagnostics = new
                    {
                        focus = s.Diagnostics.FocusScore,
                        clipping = s.Diagnostics.SaturationClippingFraction,
                        foreground = s.Diagnostics.ForegroundFraction,
                        qualityIndex = s.QualityIndex,
                        qualityStatus = s.QualityStatus
                    },
                    metals = s.Metals.ConvertAll(m => new { id = m.Id, name = m.Name, group = m.Group, pct = m.PctSample, ppm = m.PpmEstimated, score = m.Score }),
                    crystals = s.Crystals.ConvertAll(c => new { id = c.Id, name = c.Name, pct = c.PctSample, score = c.Score }),
                    gems = s.Gems.ConvertAll(g => new { id = g.Id, name = g.Name, pct = g.PctSample, score = g.Score })
                };

                var json = JsonSerializer.Serialize(laudo, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(reportPath, json);

                _lastScene = scene;
                _lastBaseImageClone?.Dispose();
                _lastBaseImageClone = (Bitmap)bmp.Clone();
                SetViewMode(ViewMode.Original);

                UpdateMaterialListsFromScene();

                AppendLog($"Snapshot/Laudo salvos em {baseDir}");
            }
            catch (Exception ex)
            {
                AppendLog($"Erro na calibração automática: {ex.Message}");
            }
        }

        private void PopulateMaterials()
        {
            if (_config?.Materials == null) return;

            _listMetals.Items.Clear();
            _listCrystals.Items.Clear();
            _listGems.Items.Clear();
            _cbTarget.Items.Clear();

            int nMet = 0, nCr = 0, nGe = 0;

            if (_config.Materials.Metais != null)
            {
                foreach (var m in _config.Materials.Metais)
                {
                    _listMetals.Items.Add($"{m.Nome ?? m.Id} ({m.Id})");
                    nMet++;

                    var nome = string.IsNullOrWhiteSpace(m.Nome) ? m.Id : m.Nome;
                    if (!string.IsNullOrWhiteSpace(nome))
                        _cbTarget.Items.Add($"Metal: {nome}");
                }
            }

            if (_config.Materials.Cristais != null)
            {
                foreach (var c in _config.Materials.Cristais)
                {
                    _listCrystals.Items.Add($"{c.Nome ?? c.Id} ({c.Id})");
                    nCr++;

                    var nome = string.IsNullOrWhiteSpace(c.Nome) ? c.Id : c.Nome;
                    if (!string.IsNullOrWhiteSpace(nome))
                        _cbTarget.Items.Add($"Cristal: {nome}");
                }
            }

            if (_config.Materials.Gemas != null)
            {
                foreach (var g in _config.Materials.Gemas)
                {
                    _listGems.Items.Add($"{g.Nome ?? g.Id} ({g.Id})");
                    nGe++;

                    var nome = string.IsNullOrWhiteSpace(g.Nome) ? g.Id : g.Nome;
                    if (!string.IsNullOrWhiteSpace(nome))
                        _cbTarget.Items.Add($"Gema: {nome}");
                }
            }

            if (_cbTarget.Items.Count > 0)
                _cbTarget.SelectedIndex = 0;

            UpdateMaterialHeadersFromScene();

            _txtDetails.Text =
                $"{_i18n[_currentLocale]["metals"]}:   {nMet}\r\n" +
                $"{_i18n[_currentLocale]["crystals"]}: {nCr}\r\n" +
                $"{_i18n[_currentLocale]["gems"]}:    {nGe}\r\n\r\n" +
                "Selecione um material nas listas para ver detalhes.";
        }

        private void ShowMaterialDetails(HvsMaterial? material)
        {
            if (material == null)
            {
                _txtDetails.Text = string.Empty;
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"ID:   {material.Id}");
            sb.AppendLine($"Nome: {material.Nome}");
            if (!string.IsNullOrWhiteSpace(material.Grupo))
                sb.AppendLine($"Grupo: {material.Grupo}");
            sb.AppendLine();

            _txtDetails.Text = sb.ToString();
        }

        // ====== Análise contínua ======
        private void BtnContinuous_Click(object? sender, EventArgs e)
        {
            if (_continuousRunning)
            {
                AppendLog("Já em modo contínuo.");
                return;
            }
            if (_analysisService == null)
            {
                AppendLog("Serviço de análise indisponível.");
                return;
            }

            // PR7 - Fase 1: Usar construtor com FullSceneAnalysis para suportar análise seletiva em modo contínuo
            _continuousController = new ContinuousAnalysisController(
                frameProvider: () => SafeGetCurrentFrameClone(),
                sceneAnalyzer: bmp => _analysisService.AnalyzeScene(bmp, null),
                intervalMs: 800);

            _continuousController.SceneAnalysisCompleted += OnContinuousSceneAnalysis;
            _continuousController.Start();
            _continuousRunning = true;
            _lblStatus.Text = "Análise contínua ativa";
            AppendLog("Modo contínuo iniciado.");
        }

        private void BtnStopContinuous_Click(object? sender, EventArgs e)
        {
            StopContinuousAnalysis();
        }

        private void StopContinuousAnalysis()
        {
            if (!_continuousRunning) return;
            try
            {
                _continuousController?.Stop();
                _continuousController = null;
                _continuousRunning = false;
                _lblStatus.Text = "Modo contínuo parado";
                AppendLog("Modo contínuo encerrado.");
            }
            catch (Exception ex)
            {
                AppendLog("Erro parar contínuo: " + ex.Message);
            }
        }

        /// <summary>
        /// Handler para análise contínua com cena completa (PR7 - Fase 1).
        /// Armazena a cena completa para permitir análise seletiva em modo contínuo/live.
        /// </summary>
        private void OnContinuousSceneAnalysis(FullSceneAnalysis scene)
        {
            if (scene == null) return;

            // Armazenar a cena completa para análise seletiva
            _lastScene = scene;
            
            // Armazenar imagem base para visualização seletiva
            if (_pictureSample.Image != null)
            {
                _lastBaseImageClone?.Dispose();
                _lastBaseImageClone = (Bitmap)_pictureSample.Image.Clone();
            }

            UpdateMaterialListsFromScene();
            _lblStatus.Text =
                $"Contínuo: foco={scene.Summary.Diagnostics.FocusScore:F2} · Qualidade={scene.Summary.QualityIndex:F1} ({scene.Summary.QualityStatus})";

            // Update quality panel
            _qualityPanel.UpdateFromDiagnostics(scene.Summary.Diagnostics);
        }

        /// <summary>
        /// Handler legado para compatibilidade (usado pelo evento AnalysisCompleted).
        /// </summary>
        private void OnContinuousAnalysis(SampleFullAnalysisResult result)
        {
            if (result == null) return;

            if (_lastScene == null)
                _lastScene = new FullSceneAnalysis
                {
                    Summary = result,
                    Labels = new PixelLabel[1, 1],
                    Mask = new SampleMaskClass?[1, 1],
                    MaskPreview = new Bitmap(1, 1),
                    Width = 1,
                    Height = 1
                };
            else
                _lastScene.Summary = result;

            UpdateMaterialListsFromScene();
            _lblStatus.Text =
                $"Contínuo: foco={result.Diagnostics.FocusScore:F2} · Qualidade={result.QualityIndex:F1} ({result.QualityStatus})";

            // Update quality panel
            _qualityPanel.UpdateFromDiagnostics(result.Diagnostics);
        }

        private Bitmap? SafeGetCurrentFrameClone()
        {
            if (_pictureSample?.Image == null) return null;
            try { return (Bitmap)_pictureSample.Image.Clone(); }
            catch { return null; }
        }

        // ===== New functionality handlers =====

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            using var settingsForm = new SettingsForm(_appSettings);
            if (settingsForm.ShowDialog(this) == DialogResult.OK)
            {
                // Reload settings
                _appSettings = AppSettings.Load();

                // Apply camera settings
                _cameraIndex = _appSettings.DefaultCameraIndex;
                _cameraWidth = _appSettings.GetResolutionWidth();
                _cameraHeight = _appSettings.GetResolutionHeight();

                AppendLog("Configurações salvas e aplicadas.");
            }
        }

        private void BtnAbout_Click(object? sender, EventArgs e)
        {
            using var aboutForm = new AboutForm(_appSettings);
            aboutForm.ShowDialog(this);
        }

        private void BtnWhatsApp_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_lastScene?.Summary == null)
                {
                    AppendLog("Nenhuma análise disponível para compartilhar. Execute uma análise primeiro.");
                    return;
                }

                // Get sample name from image path or use default
                string sampleName = "Amostra";
                if (!string.IsNullOrWhiteSpace(_lastScene.Summary.ImagePath))
                {
                    sampleName = Path.GetFileNameWithoutExtension(_lastScene.Summary.ImagePath);
                }
                else
                {
                    sampleName = $"Análise_{_lastScene.Summary.Id:N}";
                }

                // If there's no exported report, export one first
                if (string.IsNullOrWhiteSpace(_lastExportedReportPath) || !File.Exists(_lastExportedReportPath))
                {
                    var path = ExportTextReport("laudo");
                    if (path != null)
                    {
                        AppendLog($"Laudo exportado automaticamente: {path}");
                    }
                }

                WhatsAppService.ShareReport(sampleName, _lastExportedReportPath ?? "", _appSettings);
                AppendLog("WhatsApp Web aberto. Arraste o arquivo do laudo para a conversa.");
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao compartilhar via WhatsApp: {ex.Message}");
            }
        }

        private async void CheckForUpdatesOnStartupAsync()
        {
            try
            {
                if (!_appSettings.AutoUpdateCheckEnabled)
                    return;

                // Check if we should check (based on frequency)
                if (_appSettings.LastUpdateCheck.HasValue)
                {
                    var hoursSinceLastCheck = (DateTime.Now - _appSettings.LastUpdateCheck.Value).TotalHours;
                    if (hoursSinceLastCheck < _appSettings.UpdateCheckFrequencyHours)
                        return;
                }

                var updateService = new UpdateService();
                var (hasUpdate, latestVersion, errorMessage) = await updateService.CheckForUpdatesAsync();

                _appSettings.LastUpdateCheck = DateTime.Now;

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    _appSettings.LastUpdateResult = "Error";
                }
                else if (hasUpdate)
                {
                    _appSettings.LastUpdateResult = "UpdateAvailable";
                    _appSettings.LatestVersionFound = latestVersion;

                    // Show notification to user
                    var result = MessageBox.Show(
                        this,
                        $"Nova versão disponível: {latestVersion}\n\nDeseja abrir a página de download?",
                        "Atualização Disponível",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        updateService.OpenReleasesPage();
                    }
                }
                else
                {
                    _appSettings.LastUpdateResult = "NoUpdate";
                }

                _appSettings.Save();
            }
            catch
            {
                // Silent fail on startup update check
            }
        }
    }
}