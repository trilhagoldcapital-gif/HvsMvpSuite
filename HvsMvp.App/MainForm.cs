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
        // PR9: UI Constants
        private const int ActiveBorderSize = 2;
        private const int NormalBorderSize = 1;
        
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
        
        // PR9: Ribbon-style header panels
        private Panel _quickAccessBar = null!;
        private Panel _statusInfoBar = null!;
        private Label _lblStatusInfo = null!;
        
        // PR9: Quick access buttons
        private Button _btnQuickOpen = null!;
        private Button _btnQuickLive = null!;
        private Button _btnQuickAnalyze = null!;
        private Button _btnQuickPdf = null!;
        
        // PR9: Log panel controls
        private Panel _logControlPanel = null!;
        private Button _btnClearLog = null!;
        private Button _btnSaveLog = null!;

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

        // PR8: Advanced selective visualization state
        private CheckBox _chkXrayMode = null!;
        private CheckBox _chkShowUncertainty = null!;
        private bool _xrayModeEnabled;
        private bool _showUncertaintyEnabled;
        private ImageOrigin _currentImageOrigin = ImageOrigin.ImageFile;
        private bool _selectiveModeActive;
        private bool _frameFrozen;
        private int _selectiveRefreshCounter;
        private const int SelectiveRefreshInterval = 2; // Update selective every N frames

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
                    // PR15: Instead of Application.Exit(), show error and let user decide
                    // The app can still function with limited features
                    MessageBox.Show(this, 
                        $"Arquivo de configuração não encontrado:\n{configPath}\n\nO aplicativo pode não funcionar corretamente sem este arquivo.", 
                        "HVS Config - Aviso", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Warning);
                    
                    // Create a minimal default config so the app doesn't crash
                    _config = new HvsConfig();
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
                // PR15: Show error but don't exit - let the app continue with limited functionality
                MessageBox.Show(this, 
                    $"Erro ao carregar configuração HVS:\n\n{ex.Message}\n\nO aplicativo continuará com funcionalidade limitada.", 
                    "HVS Config - Erro", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Warning);
                
                _config = new HvsConfig();
            }
        }

        private void InitializeLayout()
        {
            // PR11: Clean, simple layout for 1366x768 compatibility
            // Structure: Header (28px) -> Main Toolbar (36px) -> Status Bar (22px) -> Content -> Footer (22px)
            // Total header: 86px - leaves plenty of room for content
            
            _topContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 86,
                BackColor = Color.FromArgb(8, 16, 28)
            };
            Controls.Add(_topContainer);

            // PR11: Header bar (gold) with title and language selector
            _headerBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.FromArgb(200, 160, 60),
                Padding = new Padding(8, 2, 8, 2)
            };
            _topContainer.Controls.Add(_headerBar);

            _lblTitle = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(20, 20, 30),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _headerBar.Controls.Add(_lblTitle);

            _languageMenu = new ContextMenuStrip();
            _languageMenu.Items.Add("Português (pt-BR)", null, (s, e) => SetLanguage("pt-BR"));
            _languageMenu.Items.Add("English (en-US)", null, (s, e) => SetLanguage("en-US"));
            _languageMenu.Items.Add("Español (es-ES)", null, (s, e) => SetLanguage("es-ES"));
            _languageMenu.Items.Add("Français (fr-FR)", null, (s, e) => SetLanguage("fr-FR"));
            _languageMenu.Items.Add("العربية", null, (s, e) => SetLanguage("ar"));
            _languageMenu.Items.Add("中文", null, (s, e) => SetLanguage("zh-CN"));

            _btnLanguage = new Button
            {
                Text = "🌐",
                Size = new Size(32, 22),
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            _btnLanguage.FlatAppearance.BorderSize = 0;
            _btnLanguage.Click += BtnLanguage_Click;
            _hvsToolTip.SetToolTip(_btnLanguage, "Idioma / Language");
            _headerBar.Controls.Add(_btnLanguage);

            // PR11: Main toolbar - single row with all main buttons visible
            var mainToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(12, 24, 40),
                Padding = new Padding(4, 2, 4, 2)
            };
            _topContainer.Controls.Add(mainToolbar);
            mainToolbar.BringToFront();

            _toolbarRow1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0)
            };
            mainToolbar.Controls.Add(_toolbarRow1);

            // PR11: Helper to create toolbar buttons (compact, visible)
            Button ToolbarBtn(string text, string tooltip, bool highlight = false, bool primary = false)
            {
                var b = new Button
                {
                    Text = text,
                    AutoSize = true,
                    MinimumSize = new Size(60, 28),
                    MaximumSize = new Size(100, 30),
                    Margin = new Padding(2, 1, 2, 1),
                    Padding = new Padding(4, 0, 4, 0),
                    BackColor = primary ? Color.FromArgb(50, 90, 130) : 
                                highlight ? Color.FromArgb(40, 70, 100) : Color.FromArgb(25, 45, 70),
                    ForeColor = Color.WhiteSmoke,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5f),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                b.FlatAppearance.BorderColor = primary ? Color.FromArgb(100, 150, 200) :
                                               highlight ? Color.FromArgb(80, 120, 160) : Color.FromArgb(50, 70, 100);
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 85, 130);
                b.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, 100, 150);
                _hvsToolTip.SetToolTip(b, tooltip);
                return b;
            }

            // PR11: Separator helper
            Panel ToolbarSeparator()
            {
                return new Panel
                {
                    Width = 2,
                    Height = 28,
                    BackColor = Color.FromArgb(50, 70, 100),
                    Margin = new Padding(4, 2, 4, 2)
                };
            }

            // PR11: AQUISIÇÃO buttons
            _btnOpen = ToolbarBtn("📂 Abrir", "Abrir imagem de arquivo", primary: true);
            _btnOpen.Click += BtnOpenImage_Click;
            _toolbarRow1.Controls.Add(_btnOpen);

            _btnLive = ToolbarBtn("▶ Live", "Iniciar câmera ao vivo", primary: true);
            _btnLive.Click += BtnLive_Click;
            _toolbarRow1.Controls.Add(_btnLive);

            _btnStopLive = ToolbarBtn("⏹ Parar", "Parar câmera");
            _btnStopLive.Click += BtnParar_Click;
            _toolbarRow1.Controls.Add(_btnStopLive);

            _toolbarRow1.Controls.Add(ToolbarSeparator());

            // PR11: ANÁLISE buttons
            _btnAnalyze = ToolbarBtn("🧪 Analisar", "Executar análise completa", primary: true);
            _btnAnalyze.Click += BtnAnalisar_Click;
            _toolbarRow1.Controls.Add(_btnAnalyze);

            _btnMask = ToolbarBtn("🎨 Másc.", "Ver máscara de segmentação");
            _btnMask.Click += BtnMascara_Click;
            _toolbarRow1.Controls.Add(_btnMask);

            _toolbarRow1.Controls.Add(ToolbarSeparator());

            // PR11: RELATÓRIOS buttons
            _btnPdf = ToolbarBtn("📄 PDF", "Exportar laudo PDF", primary: true);
            _btnPdf.Click += BtnPdf_Click;
            _toolbarRow1.Controls.Add(_btnPdf);

            _btnTxt = ToolbarBtn("📝 TXT", "Exportar laudo TXT");
            _btnTxt.Click += BtnTxt_Click;
            _toolbarRow1.Controls.Add(_btnTxt);

            _toolbarRow1.Controls.Add(ToolbarSeparator());

            // PR11: ZOOM buttons
            _btnZoomIn = ToolbarBtn("🔍+", "Aumentar zoom");
            _btnZoomIn.MinimumSize = new Size(40, 28);
            _btnZoomIn.Click += BtnZoomMais_Click;
            _toolbarRow1.Controls.Add(_btnZoomIn);

            _btnZoomOut = ToolbarBtn("🔍-", "Diminuir zoom");
            _btnZoomOut.MinimumSize = new Size(40, 28);
            _btnZoomOut.Click += BtnZoomMenos_Click;
            _toolbarRow1.Controls.Add(_btnZoomOut);

            _toolbarRow1.Controls.Add(ToolbarSeparator());

            // PR11: Target combo (compact)
            var targetPanel = new Panel { Size = new Size(120, 28), Margin = new Padding(2, 2, 2, 2) };
            _cbTarget = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 2),
                Size = new Size(116, 24),
                BackColor = Color.FromArgb(32, 32, 44),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8)
            };
            targetPanel.Controls.Add(_cbTarget);
            _toolbarRow1.Controls.Add(targetPanel);

            _toolbarRow1.Controls.Add(ToolbarSeparator());

            // PR11: SISTEMA buttons (compact)
            _btnSettings = ToolbarBtn("⚙️", "Configurações");
            _btnSettings.MinimumSize = new Size(32, 28);
            _btnSettings.Click += BtnSettings_Click;
            _toolbarRow1.Controls.Add(_btnSettings);

            _btnAbout = ToolbarBtn("ℹ️", "Sobre o aplicativo");
            _btnAbout.MinimumSize = new Size(32, 28);
            _btnAbout.Click += BtnAbout_Click;
            _toolbarRow1.Controls.Add(_btnAbout);

            // PR11: Status info bar
            _statusInfoBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 22,
                BackColor = Color.FromArgb(12, 22, 38),
                Padding = new Padding(8, 2, 8, 2)
            };
            _topContainer.Controls.Add(_statusInfoBar);
            _statusInfoBar.BringToFront();

            _lblStatusInfo = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(160, 175, 195),
                Font = new Font("Segoe UI", 8f),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "ORIGEM: –  •  MODO: –  •  ALVO: –"
            };
            _statusInfoBar.Controls.Add(_lblStatusInfo);

            // PR11: Initialize hidden buttons (for compatibility with existing code)
            // These buttons are not in the main toolbar but can be accessed via menus or other means
            _btnContinuous = new Button { Visible = false };
            _btnContinuous.Click += BtnContinuous_Click;
            _btnStopContinuous = new Button { Visible = false };
            _btnStopContinuous.Click += BtnStopContinuous_Click;
            _btnCameraSel = new Button { Visible = false };
            _btnCameraSel.Click += BtnSelecionarCamera_Click;
            _btnResolucaoSel = new Button { Visible = false };
            _btnResolucaoSel.Click += BtnSelecionarResolucao_Click;
            _btnSelectiveAnalyze = new Button { Visible = false };
            _btnSelectiveAnalyze.Click += BtnSelectiveAnalyze_Click;
            _btnMaskBg = new Button { Visible = false };
            _btnMaskBg.Click += BtnFundoMasc_Click;
            _btnPhaseMap = new Button { Visible = false };
            _btnPhaseMap.Click += BtnPhaseMap_Click;
            _btnHeatmap = new Button { Visible = false };
            _btnHeatmap.Click += BtnHeatmap_Click;
            _btnScale = new Button { Visible = false };
            _btnScale.Click += BtnEscala_Click;
            _btnWB = new Button { Visible = false };
            _btnWB.Click += BtnBalanco_Click;
            _btnJson = new Button { Visible = false };
            _btnJson.Click += BtnJson_Click;
            _btnCsv = new Button { Visible = false };
            _btnCsv.Click += BtnCsv_Click;
            _btnBiCsv = new Button { Visible = false };
            _btnBiCsv.Click += BtnBiCsv_Click;
            _btnAi = new Button { Visible = false };
            _btnAi.Click += BtnParticulas_Click;
            _btnExportIa = new Button { Visible = false };
            _btnExportIa.Click += BtnExportIa_Click;
            _btnQaPanel = new Button { Visible = false };
            _btnQaPanel.Click += BtnQaPanel_Click;
            _btnTraining = new Button { Visible = false };
            _btnTraining.Click += BtnTraining_Click;
            _btnWhatsApp = new Button { Visible = false };
            _btnWhatsApp.Click += BtnWhatsApp_Click;
            _btnDebugHvs = new Button { Visible = false };
            _btnDebugHvs.Click += BtnDebugHvs_Click;
            _btnCalib = new Button { Visible = false };
            _btnCalib.Click += BtnCalibrarAuto_Click;
            
            // PR11: Quick access buttons (for compatibility)
            _btnQuickOpen = _btnOpen;
            _btnQuickLive = _btnLive;
            _btnQuickAnalyze = _btnAnalyze;
            _btnQuickPdf = _btnPdf;
            
            // PR11: Checkboxes (hidden, for compatibility)
            _chkXrayMode = new CheckBox { Visible = false, Checked = false };
            _chkXrayMode.CheckedChanged += ChkXrayMode_CheckedChanged;
            _chkShowUncertainty = new CheckBox { Visible = false, Checked = false };
            _chkShowUncertainty.CheckedChanged += ChkShowUncertainty_CheckedChanged;
            
            // PR11: Quick access bar (for compatibility)
            _quickAccessBar = new Panel { Visible = false };
            
            // PR11: Toolbar rows (for compatibility)
            _toolbarRow2 = _toolbarRow1;
            _toolbarRow3 = _toolbarRow1;

            // PR11: Main content area - simple split: top (image+materials) / bottom (log)
            _mainVerticalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 4,
                BackColor = Color.FromArgb(30, 30, 50),
                FixedPanel = FixedPanel.Panel2,
                IsSplitterFixed = false
            };
            Controls.Add(_mainVerticalSplit);
            _mainVerticalSplit.BringToFront();

            _mainVerticalSplit.Panel1MinSize = 200;
            _mainVerticalSplit.Panel2MinSize = 80;

            // PR11: Upper area split - left (image) / right (materials)
            _contentVerticalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 4,
                BackColor = Color.FromArgb(25, 30, 45),
                FixedPanel = FixedPanel.Panel2,
                IsSplitterFixed = false
            };
            _mainVerticalSplit.Panel1.Controls.Add(_contentVerticalSplit);

            _contentVerticalSplit.Panel1MinSize = 300;
            _contentVerticalSplit.Panel2MinSize = 200;

            // PR11: For compatibility with old code
            _cameraMaterialsSplit = _contentVerticalSplit;

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

            // PR11: Log panel with control bar (in bottom panel of main split)
            var logContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(6, 14, 24),
                Padding = new Padding(0)
            };
            _mainVerticalSplit.Panel2.Controls.Add(logContainer);

            // PR9: Log control bar
            _logControlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 26,
                BackColor = Color.FromArgb(12, 22, 36),
                Padding = new Padding(4, 2, 4, 2)
            };
            logContainer.Controls.Add(_logControlPanel);

            var lblLogTitle = new Label
            {
                Text = "📋 Log / Console",
                AutoSize = true,
                ForeColor = Color.FromArgb(180, 190, 210),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(4, 5)
            };
            _logControlPanel.Controls.Add(lblLogTitle);

            // PR9: Use FlowLayoutPanel for log control buttons (right-aligned)
            var logButtonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0)
            };
            _logControlPanel.Controls.Add(logButtonFlow);

            _btnClearLog = new Button
            {
                Text = "🗑 Limpar",
                Size = new Size(70, 20),
                Margin = new Padding(2, 2, 4, 0),
                BackColor = Color.FromArgb(30, 50, 70),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8)
            };
            _btnClearLog.FlatAppearance.BorderColor = Color.FromArgb(50, 70, 100);
            _btnClearLog.FlatAppearance.BorderSize = 1;
            _btnClearLog.Click += BtnClearLog_Click;
            logButtonFlow.Controls.Add(_btnClearLog);

            _btnSaveLog = new Button
            {
                Text = "💾 Salvar",
                Size = new Size(70, 20),
                Margin = new Padding(2, 2, 2, 0),
                BackColor = Color.FromArgb(30, 50, 70),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8)
            };
            _btnSaveLog.FlatAppearance.BorderColor = Color.FromArgb(50, 70, 100);
            _btnSaveLog.FlatAppearance.BorderSize = 1;
            _btnSaveLog.Click += BtnSaveLog_Click;
            logButtonFlow.Controls.Add(_btnSaveLog);

            _txtDetails = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(6, 14, 24),
                ForeColor = Color.FromArgb(200, 210, 220),
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9)
            };
            logContainer.Controls.Add(_txtDetails);
            _txtDetails.BringToFront();
            _logControlPanel.BringToFront();

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
            
            // PR11: Initialize button enabled states
            UpdateButtonEnabledStates();
            
            // PR11: Set initial splitter distance after load
            this.Load += (s, e) =>
            {
                try
                {
                    // Image panel takes ~65% of width
                    if (_contentVerticalSplit != null && _contentVerticalSplit.Width > 0)
                    {
                        _contentVerticalSplit.SplitterDistance = (int)(_contentVerticalSplit.Width * 0.65);
                    }
                    // Log panel takes ~20% of height
                    if (_mainVerticalSplit != null && _mainVerticalSplit.Height > 0)
                    {
                        _mainVerticalSplit.SplitterDistance = (int)(_mainVerticalSplit.Height * 0.80);
                    }
                }
                catch { } // Ignore layout errors during load
            };
        }

        private void ApplyLocaleTexts()
        {
            if (!_i18n.TryGetValue(_currentLocale, out var t))
                t = _i18n["pt-BR"];

            Text = t["title"];
            _lblTitle.Text = t["title"];

            UpdateMaterialHeadersFromScene();
            _lblStatus.Text = t["status.ready"];
            
            // PR9: Update status info bar
            UpdateStatusInfoBar();

            // PR11: Language button shows just an icon now
            // _btnLanguage.Text is already set in InitializeLayout

            // Update training mode button state
            UpdateTrainingModeButton();
        }
        
        /// <summary>
        /// PR10: Updates the status info bar with current state in professional format.
        /// Format: ORIGEM: ... • MODO: ... • ALVO: ... • FOCO: ... • MÁSCARA: ...
        /// </summary>
        private void UpdateStatusInfoBar()
        {
            if (_lblStatusInfo == null) return;
            
            // Determine origin
            string origin = _currentImageOrigin switch
            {
                ImageOrigin.ImageFile => "IMAGEM",
                ImageOrigin.CameraLive => "CÂMERA (Live)",
                ImageOrigin.CameraContinuous => "CÂMERA (Contínuo)",
                ImageOrigin.CameraFrozen => "CÂMERA (Congelado)",
                _ => "–"
            };
            
            // Determine view mode
            string viewMode = _currentViewMode switch
            {
                ViewMode.Original => "Original",
                ViewMode.MaskGlobal => "Máscara",
                ViewMode.MapaFases => "Mapa de Fases",
                ViewMode.HeatmapAlvo => "Heatmap",
                ViewMode.SeletivaAlvo => $"Seletiva{(_xrayModeEnabled ? " X-ray" : "")}",
                ViewMode.SeletivaXray => "Seletiva X-ray",
                ViewMode.SeletivaAuPgm => "Seletiva Au+PGM",
                ViewMode.FundoMascarado => "Fundo mascarado",
                _ => "–"
            };
            
            // Determine target
            string target = _cbTarget?.SelectedItem?.ToString() ?? "–";
            if (target.Contains(":"))
            {
                var parts = target.Split(':');
                if (parts.Length > 1) target = parts[1].Trim();
            }
            
            // Determine focus and mask from last analysis
            string focus = "–";
            string mask = "–";
            if (_lastScene?.Summary?.Diagnostics != null)
            {
                var diag = _lastScene.Summary.Diagnostics;
                double focusScore = diag.FocusScorePercent;
                focus = focusScore >= 50 ? $"OK ({focusScore:F0}%)" : 
                        focusScore >= 30 ? $"Atenção ({focusScore:F0}%)" : 
                        $"Ruim ({focusScore:F0}%)";
                
                double maskFrac = diag.ForegroundFraction * 100;
                mask = diag.ForegroundFractionStatus ?? $"{maskFrac:F0}%";
            }
            
            // PR10: Professional format with bullet separators
            _lblStatusInfo.Text = $"ORIGEM: {origin}  •  MODO: {viewMode}  •  ALVO: {target}  •  FOCO: {focus}  •  MÁSCARA: {mask}";
        }
        
        /// <summary>
        /// PR9: Clear log button handler.
        /// </summary>
        private void BtnClearLog_Click(object? sender, EventArgs e)
        {
            _txtDetails.Clear();
            AppendLog("Log limpo.");
        }
        
        /// <summary>
        /// PR9: Save log button handler.
        /// </summary>
        private void BtnSaveLog_Click(object? sender, EventArgs e)
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                
                string fileName = $"session_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string path = Path.Combine(logDir, fileName);
                
                File.WriteAllText(path, _txtDetails.Text, Encoding.UTF8);
                AppendLog($"Log salvo em: {path}");
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao salvar log: {ex.Message}");
            }
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
                    // PR8: Update image origin
                    _currentImageOrigin = ImageOrigin.ImageFile;
                    _frameFrozen = false;
                    _selectiveModeActive = false;
                    SetViewMode(ViewMode.Original);
                    ApplyZoom();
                    AppendLog($"Imagem carregada: {Path.GetFileName(dlg.FileName)}");
                    
                    // PR9: Update button enabled states
                    UpdateButtonEnabledStates();
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
        /// PR8: Inclui informação de modo X-ray.
        /// PR9: Atualiza status info bar e button toggle states.
        /// </summary>
        private void SetViewMode(ViewMode mode, string? targetMaterial = null)
        {
            _currentViewMode = mode;
            _currentTargetMaterial = targetMaterial;
            
            // Atualizar label de status com descrição do modo (PR8: inclui X-ray)
            string description = VisualizationService.GetViewModeDescription(mode, targetMaterial, _xrayModeEnabled);
            AppendLog(description);
            
            // PR9: Update status info bar
            UpdateStatusInfoBar();
            
            // PR9: Update toggle button visual states
            UpdateViewModeButtonStates();
        }
        
        /// <summary>
        /// PR9: Updates the visual state of view mode toggle buttons.
        /// </summary>
        private void UpdateViewModeButtonStates()
        {
            // Colors for active and inactive states
            Color activeColor = Color.FromArgb(60, 100, 140);
            Color activeBorder = Color.FromArgb(100, 150, 200);
            Color normalColor = Color.FromArgb(25, 45, 70);
            Color normalBorder = Color.FromArgb(50, 70, 100);
            
            // Reset all toggle buttons to normal state
            SetButtonToggleState(_btnMask, false, normalColor, normalBorder);
            SetButtonToggleState(_btnMaskBg, false, normalColor, normalBorder);
            SetButtonToggleState(_btnPhaseMap, false, normalColor, normalBorder);
            SetButtonToggleState(_btnHeatmap, false, normalColor, normalBorder);
            SetButtonToggleState(_btnSelectiveAnalyze, false, normalColor, normalBorder);
            
            // Set active button based on current view mode
            switch (_currentViewMode)
            {
                case ViewMode.MaskGlobal:
                    SetButtonToggleState(_btnMask, true, activeColor, activeBorder);
                    break;
                case ViewMode.FundoMascarado:
                    SetButtonToggleState(_btnMaskBg, true, activeColor, activeBorder);
                    break;
                case ViewMode.MapaFases:
                    SetButtonToggleState(_btnPhaseMap, true, activeColor, activeBorder);
                    break;
                case ViewMode.HeatmapAlvo:
                    SetButtonToggleState(_btnHeatmap, true, activeColor, activeBorder);
                    break;
                case ViewMode.SeletivaAlvo:
                case ViewMode.SeletivaXray:
                case ViewMode.SeletivaAuPgm:
                    SetButtonToggleState(_btnSelectiveAnalyze, true, activeColor, activeBorder);
                    break;
            }
        }
        
        /// <summary>
        /// PR9: Sets the visual toggle state of a button.
        /// </summary>
        private void SetButtonToggleState(Button? btn, bool active, Color backColor, Color borderColor)
        {
            if (btn == null) return;
            btn.BackColor = backColor;
            btn.FlatAppearance.BorderColor = borderColor;
            btn.FlatAppearance.BorderSize = active ? ActiveBorderSize : NormalBorderSize;
        }
        
        /// <summary>
        /// PR9: Updates button enabled state based on context.
        /// </summary>
        private void UpdateButtonEnabledStates()
        {
            bool hasImage = _pictureSample?.Image != null;
            bool hasAnalysis = _lastScene != null;
            
            // Analysis buttons - require image
            _btnAnalyze.Enabled = hasImage;
            _btnQuickAnalyze.Enabled = hasImage;
            
            // View mode buttons - require analysis
            _btnSelectiveAnalyze.Enabled = hasAnalysis;
            _btnMask.Enabled = hasAnalysis;
            _btnMaskBg.Enabled = hasAnalysis;
            _btnPhaseMap.Enabled = hasAnalysis;
            _btnHeatmap.Enabled = hasAnalysis;
            
            // Report buttons - require analysis
            _btnTxt.Enabled = hasAnalysis;
            _btnPdf.Enabled = hasAnalysis;
            _btnQuickPdf.Enabled = hasAnalysis;
            _btnJson.Enabled = hasAnalysis;
            _btnCsv.Enabled = hasAnalysis;
            _btnBiCsv.Enabled = hasAnalysis;
            _btnExportIa.Enabled = hasAnalysis && (_lastScene?.Summary?.Particles?.Count ?? 0) > 0;
            _btnQaPanel.Enabled = hasAnalysis && (_lastScene?.Summary?.Particles?.Count ?? 0) > 0;
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
                // PR8: Update image origin
                _currentImageOrigin = ImageOrigin.CameraLive;
                _frameFrozen = false;
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
                
                // PR8: Frame freezing - maintain last scene for selective analysis
                _frameFrozen = true;
                _currentImageOrigin = ImageOrigin.CameraFrozen;
                
                AppendLog("Live microscópio parado – Frame congelado para análise.");
                
                // PR8: If there's no analysis yet but we have an image, run analysis
                if (_lastScene == null && _pictureSample.Image != null && _analysisService != null)
                {
                    AppendLog("Executando análise no frame congelado...");
                    using var bmp = new Bitmap(_pictureSample.Image);
                    _lastScene = _analysisService.AnalyzeScene(bmp, null);
                    _lastBaseImageClone?.Dispose();
                    _lastBaseImageClone = (Bitmap)bmp.Clone();
                    UpdateMaterialListsFromScene();
                    AppendLog("✅ Análise concluída. Frame pronto para análise seletiva.");
                }
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
                
                // PR9: Update button enabled states and status info bar
                UpdateButtonEnabledStates();
                UpdateStatusInfoBar();
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
        /// Análise seletiva unificada (PR7 - Fase 1, atualizado PR8).
        /// Funciona igualmente para imagem estática, frame de câmera e modo contínuo.
        /// PR8: Suporta modo X-ray, Au+PGM combinado, e visualização de incerteza.
        /// </summary>
        private void BtnSelectiveAnalyze_Click(object? sender, EventArgs e)
        {
            try
            {
                // PR8: Toggle selective mode if already active
                if (_selectiveModeActive && _currentViewMode == ViewMode.SeletivaAlvo || 
                    _currentViewMode == ViewMode.SeletivaXray || 
                    _currentViewMode == ViewMode.SeletivaAuPgm)
                {
                    // Deactivate selective mode, return to original
                    _selectiveModeActive = false;
                    if (_lastBaseImageClone != null)
                    {
                        _pictureSample.Image?.Dispose();
                        _pictureSample.Image = (Bitmap)_lastBaseImageClone.Clone();
                    }
                    SetViewMode(ViewMode.Original);
                    AppendLog("Análise seletiva desativada.");
                    return;
                }

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
                string modeInfo = _xrayModeEnabled ? " (X-ray)" : "";
                if (_showUncertaintyEnabled) modeInfo += " (Incerteza)";
                AppendLog($"🎯 Análise seletiva para: {alvoTexto}{modeInfo}");

                // PR8: Activate selective mode
                _selectiveModeActive = true;

                // PR8: Check for Au+PGM combined mode
                if (alvoTexto.StartsWith("Alvo: Au + PGM", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyAuPgmSelectiveView();
                    return;
                }

                // Standard single-target selective analysis
                ApplySingleTargetSelectiveViewWithDetails(alvoTexto, focusIsBad);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Erro na análise seletiva: {ex.Message}");
            }
        }

        /// <summary>
        /// PR8: Apply single target selective view with full details (used by button click).
        /// </summary>
        private void ApplySingleTargetSelectiveViewWithDetails(string alvoTexto, bool focusIsBad)
        {
            if (_lastScene == null || _lastBaseImageClone == null)
                return;

            var summary = _lastScene.Summary;
            var sb = new StringBuilder();
            sb.AppendLine($"🎯 Análise seletiva – {alvoTexto}");
            sb.AppendLine("────────────────────────────────────────");

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

            if (string.IsNullOrWhiteSpace(targetId))
            {
                AppendLog("⚠️ Nenhum ID de alvo encontrado no resumo. Nada para destacar.");
                _selectiveModeActive = false;
                return;
            }

            // Generate selective view
            using var baseImg = new Bitmap(_lastBaseImageClone);
            Bitmap? selective = null;
            SelectiveConfidenceResult? confResult = null;

            if (_xrayModeEnabled)
            {
                selective = VisualizationService.BuildSelectiveXrayView(
                    baseImg,
                    _lastScene,
                    targetId,
                    out confResult,
                    materialType: tipoAlvo,
                    confidenceThreshold: 0.5,
                    showUncertainty: _showUncertaintyEnabled);
            }
            else
            {
                selective = VisualizationService.BuildSelectiveView(
                    baseImg,
                    _lastScene,
                    targetId,
                    tipoAlvo,
                    confidenceThreshold: 0.5);
                confResult = BuildConfidenceResultFromScene(_lastScene, targetId);
            }

            if (selective != null)
            {
                _pictureSample.Image?.Dispose();
                _pictureSample.Image = (Bitmap)selective.Clone();
                
                // Atualizar ViewMode e status
                _currentTargetMaterial = targetName ?? targetId;
                SetViewMode(_xrayModeEnabled ? ViewMode.SeletivaXray : ViewMode.SeletivaAlvo, _currentTargetMaterial);
                
                // PR8: Build enhanced summary
                if (confResult != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Área alta confiança: {confResult.HighConfidencePercent:F0}% da área {_currentTargetMaterial}");
                    sb.AppendLine($"Área baixa confiança: {confResult.LowConfidencePercent:F0}% (zona de transição)");
                    sb.AppendLine($"Partículas: {confResult.ParticleCount}");
                }

                // Add focus warning
                if (focusIsBad)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠️ ATENÇÃO: foco ruim – resultados apenas indicativos.");
                }

                // PR8: Add origin and mode info
                sb.AppendLine();
                sb.AppendLine($"Origem: {VisualizationService.GetImageOriginDescription(_currentImageOrigin)}");
                if (_xrayModeEnabled)
                    sb.AppendLine("Modo: X-ray");
                if (_showUncertaintyEnabled)
                    sb.AppendLine("Visualização de incerteza: Ativa");

                _txtDetails.Text = sb.ToString();
                
                _zoomFactor = 1.0f;
                ApplyZoom();
                
                string modeStr = _xrayModeEnabled ? " (X-ray)" : "";
                if (pctSample > 0)
                {
                    AppendLog($"✅ Análise seletiva{modeStr} aplicada para '{targetId}' ({pctSample:P2} da amostra).");
                }
                else
                {
                    AppendLog($"ℹ️ Análise seletiva{modeStr} para '{targetId}': 0% detectado nesta amostra.");
                }
            }
            else
            {
                AppendLog("❌ Falha ao gerar visualização seletiva.");
                _selectiveModeActive = false;
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
                        var parts = sel.Split(':');
                        if (parts.Length > 1)
                        {
                            targetName = parts[1].Trim();
                            targetId = ExtractMaterialIdFromName(targetName);
                        }
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

            // PR8: Add Au+PGM combined option
            _cbTarget.Items.Insert(0, "Alvo: Au + PGM");

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

        // PR8: X-ray mode checkbox handler
        private void ChkXrayMode_CheckedChanged(object? sender, EventArgs e)
        {
            _xrayModeEnabled = _chkXrayMode.Checked;
            AppendLog($"Modo X-ray: {(_xrayModeEnabled ? "Ativado" : "Desativado")}");
            
            // If selective mode is active, refresh the visualization
            if (_selectiveModeActive && _lastScene != null && _lastBaseImageClone != null)
            {
                RefreshSelectiveVisualization();
            }
        }

        // PR8: Uncertainty visualization checkbox handler
        private void ChkShowUncertainty_CheckedChanged(object? sender, EventArgs e)
        {
            _showUncertaintyEnabled = _chkShowUncertainty.Checked;
            AppendLog($"Visualização de incerteza: {(_showUncertaintyEnabled ? "Ativada" : "Desativada")}");
            
            // If selective mode is active, refresh the visualization
            if (_selectiveModeActive && _lastScene != null && _lastBaseImageClone != null)
            {
                RefreshSelectiveVisualization();
            }
        }

        // PR8: Refresh selective visualization (used after mode changes or during live)
        private void RefreshSelectiveVisualization()
        {
            if (_lastScene == null || _lastBaseImageClone == null)
                return;

            if (_cbTarget == null || _cbTarget.SelectedItem == null)
                return;

            string alvoTexto = _cbTarget.SelectedItem.ToString() ?? "";

            // Check if Au+PGM combined mode
            if (alvoTexto.StartsWith("Alvo: Au + PGM", StringComparison.OrdinalIgnoreCase))
            {
                ApplyAuPgmSelectiveView();
            }
            else
            {
                ApplySingleTargetSelectiveView(alvoTexto);
            }
        }

        // PR8: Apply Au+PGM combined selective view
        private void ApplyAuPgmSelectiveView()
        {
            if (_lastScene == null || _lastBaseImageClone == null)
                return;

            using var baseImg = new Bitmap(_lastBaseImageClone);
            var selective = VisualizationService.BuildSelectiveAuPgmView(
                baseImg,
                _lastScene,
                _xrayModeEnabled,
                out var combinedResult,
                confidenceThreshold: 0.5,
                showUncertainty: _showUncertaintyEnabled);

            if (selective != null)
            {
                _pictureSample.Image?.Dispose();
                _pictureSample.Image = (Bitmap)selective.Clone();

                _currentTargetMaterial = "Au+PGM";
                SetViewMode(_xrayModeEnabled ? ViewMode.SeletivaXray : ViewMode.SeletivaAuPgm, _currentTargetMaterial);

                if (combinedResult != null)
                {
                    _txtDetails.Text = VisualizationService.BuildAuPgmSummary(
                        combinedResult, _currentImageOrigin, _xrayModeEnabled);
                }

                _zoomFactor = 1.0f;
                ApplyZoom();
                AppendLog($"✅ Análise seletiva Au+PGM aplicada{(_xrayModeEnabled ? " (X-ray)" : "")}.");
            }
        }

        // PR8: Apply single target selective view
        private void ApplySingleTargetSelectiveView(string alvoTexto)
        {
            if (_lastScene == null || _lastBaseImageClone == null)
                return;

            string? targetId = null;
            string? targetName = null;
            int tipoAlvo = 0;

            // Parse target from combo text
            if (alvoTexto.StartsWith("Metal:", StringComparison.OrdinalIgnoreCase))
            {
                targetName = alvoTexto.Substring("Metal:".Length).Trim();
                targetId = ExtractMaterialIdFromName(targetName);
                tipoAlvo = 0;
            }
            else if (alvoTexto.StartsWith("Cristal:", StringComparison.OrdinalIgnoreCase))
            {
                targetName = alvoTexto.Substring("Cristal:".Length).Trim();
                targetId = ExtractMaterialIdFromName(targetName);
                tipoAlvo = 1;
            }
            else if (alvoTexto.StartsWith("Gema:", StringComparison.OrdinalIgnoreCase))
            {
                targetName = alvoTexto.Substring("Gema:".Length).Trim();
                targetId = ExtractMaterialIdFromName(targetName);
                tipoAlvo = 2;
            }

            if (string.IsNullOrWhiteSpace(targetId))
                return;

            using var baseImg = new Bitmap(_lastBaseImageClone);

            Bitmap? selective = null;
            SelectiveConfidenceResult? confResult = null;

            if (_xrayModeEnabled)
            {
                selective = VisualizationService.BuildSelectiveXrayView(
                    baseImg,
                    _lastScene,
                    targetId,
                    out confResult,
                    materialType: tipoAlvo,
                    confidenceThreshold: 0.5,
                    showUncertainty: _showUncertaintyEnabled);
            }
            else
            {
                selective = VisualizationService.BuildSelectiveView(
                    baseImg,
                    _lastScene,
                    targetId,
                    tipoAlvo,
                    confidenceThreshold: 0.5);

                // Build confidence result for standard view
                confResult = BuildConfidenceResultFromScene(_lastScene, targetId);
            }

            if (selective != null)
            {
                _pictureSample.Image?.Dispose();
                _pictureSample.Image = (Bitmap)selective.Clone();

                _currentTargetMaterial = targetName ?? targetId;
                SetViewMode(_xrayModeEnabled ? ViewMode.SeletivaXray : ViewMode.SeletivaAlvo, _currentTargetMaterial);

                if (confResult != null)
                {
                    _txtDetails.Text = VisualizationService.BuildSelectiveSummary(
                        _currentTargetMaterial, confResult, _currentImageOrigin, _xrayModeEnabled);
                }

                _zoomFactor = 1.0f;
                ApplyZoom();
            }
        }

        // PR8: Build confidence result from scene for standard selective view
        private SelectiveConfidenceResult? BuildConfidenceResultFromScene(FullSceneAnalysis scene, string targetId)
        {
            if (scene?.Labels == null) return null;

            int totalTarget = 0;
            int highConf = 0;
            int lowConf = 0;
            int totalSample = 0;

            int w = scene.Width;
            int h = scene.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var lbl = scene.Labels[x, y];
                    if (lbl == null || !lbl.IsSample) continue;
                    totalSample++;

                    if (string.Equals(lbl.MaterialId, targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        totalTarget++;
                        if (lbl.MaterialConfidence >= 0.7)
                            highConf++;
                        else
                            lowConf++;
                    }
                }
            }

            return new SelectiveConfidenceResult
            {
                TotalTargetPixels = totalTarget,
                HighConfidencePixels = highConf,
                LowConfidencePixels = lowConf,
                TargetFractionOfSample = totalSample > 0 ? (double)totalTarget / totalSample : 0,
                PpmEstimated = totalSample > 0 ? ((double)totalTarget / totalSample) * 1_000_000 : 0,
                ParticleCount = scene.Summary?.Particles?.Count(p => 
                    string.Equals(p.MaterialId, targetId, StringComparison.OrdinalIgnoreCase)) ?? 0
            };
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

            // PR8: Update image origin
            _currentImageOrigin = ImageOrigin.CameraContinuous;
            _frameFrozen = false;

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

                // PR8: Frame freezing - keep last scene and selective view active
                _frameFrozen = true;
                _currentImageOrigin = ImageOrigin.CameraFrozen;
                
                _lblStatus.Text = "Modo contínuo parado – Frame congelado";
                AppendLog("Modo contínuo encerrado. Frame congelado para análise.");

                // PR8: If selective mode was active, update the summary to show frozen state
                if (_selectiveModeActive && _lastScene != null)
                {
                    AppendLog("Análise seletiva mantida no frame congelado. Você pode mudar alvo/modo.");
                }
            }
            catch (Exception ex)
            {
                AppendLog("Erro parar contínuo: " + ex.Message);
            }
        }

        /// <summary>
        /// Handler para análise contínua com cena completa (PR7 - Fase 1, atualizado PR8).
        /// Armazena a cena completa para permitir análise seletiva em modo contínuo/live.
        /// PR8: Atualiza visualização seletiva persistente a cada frame.
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

            // PR8: Atualizar visualização seletiva persistente
            if (_selectiveModeActive)
            {
                _selectiveRefreshCounter++;
                if (_selectiveRefreshCounter >= SelectiveRefreshInterval)
                {
                    _selectiveRefreshCounter = 0;
                    RefreshSelectiveVisualization();
                }
            }

            // PR8: Update status with origin info
            string originInfo = VisualizationService.GetImageOriginDescription(_currentImageOrigin);
            string viewModeInfo = _selectiveModeActive ? $" | Seletiva: {_currentTargetMaterial}" : "";
            _lblStatus.Text =
                $"Contínuo: foco={scene.Summary.Diagnostics.FocusScore:F2} · Qualidade={scene.Summary.QualityIndex:F1} ({scene.Summary.QualityStatus}){viewModeInfo}";

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

        /// <summary>
        /// PR10: Load an image file from the welcome screen.
        /// </summary>
        public void LoadImageFromWelcome(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath))
            {
                AppendLog($"Arquivo de imagem não encontrado: {imagePath}");
                return;
            }

            try
            {
                using var bmp = new Bitmap(imagePath);
                _pictureSample.Image?.Dispose();
                _pictureSample.Image = (Bitmap)bmp.Clone();
                _zoomFactor = 1.0f;
                _lastBaseImageClone?.Dispose();
                _lastBaseImageClone = (Bitmap)bmp.Clone();
                _lastScene = null;
                _currentImageOrigin = ImageOrigin.ImageFile;
                _frameFrozen = false;
                _selectiveModeActive = false;
                SetViewMode(ViewMode.Original);
                ApplyZoom();
                AppendLog($"Imagem carregada: {System.IO.Path.GetFileName(imagePath)}");
                UpdateButtonEnabledStates();
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao carregar imagem: {ex.Message}");
            }
        }

        /// <summary>
        /// PR10: Start live camera mode from the welcome screen.
        /// </summary>
        public void StartLiveFromWelcome()
        {
            // Trigger the live button click
            BtnLive_Click(this, EventArgs.Empty);
        }
    }
}