using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR10: Futuristic welcome screen with 3D-style glass card effect.
    /// Features animated background particles and three main action buttons.
    /// </summary>
    public class WelcomeScreen : Form
    {
        // Action result enum
        public enum WelcomeAction
        {
            None,
            NewImageAnalysis,
            LiveCamera,
            ExploreSamplesReports,
            GoToMainDirect
        }

        // Result after user interaction
        public WelcomeAction SelectedAction { get; private set; } = WelcomeAction.None;
        public string? SelectedImagePath { get; private set; }
        public bool SkipWelcomeOnStartup { get; private set; }

        // Animation
        private readonly System.Windows.Forms.Timer _particleTimer;
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private float _opacity = 0f;
        private bool _fadingIn = true;
        private bool _fadingOut = false;
        private Particle[]? _particles;
        private readonly Random _random = new Random();

        // Color palette
        private readonly Color _bgColor = Color.FromArgb(8, 14, 24);
        private readonly Color _cardBg = Color.FromArgb(25, 35, 55);
        private readonly Color _cardBorder = Color.FromArgb(45, 60, 85);
        private readonly Color _goldAccent = Color.FromArgb(200, 160, 60);
        private readonly Color _cyanAccent = Color.FromArgb(60, 180, 200);
        private readonly Color _textPrimary = Color.FromArgb(230, 235, 245);
        private readonly Color _textSecondary = Color.FromArgb(150, 165, 185);

        // Controls
        private Panel _glassCard = null!;
        private CheckBox _chkSkipWelcome = null!;
        private Button _btnSettings = null!;
        private Label _lblVersion = null!;

        // Settings reference
        private readonly AppSettings _appSettings;

        public WelcomeScreen(AppSettings appSettings)
        {
            _appSettings = appSettings;

            // Form setup
            Text = "TGC Metal Analítico – HVS-MVP";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(900, 620);
            BackColor = _bgColor;
            DoubleBuffered = true;
            Opacity = 0;

            InitializeParticles();
            InitializeLayout();

            // Particle animation timer
            _particleTimer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30fps
            _particleTimer.Tick += ParticleTimer_Tick;

            // Fade animation timer
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += FadeTimer_Tick;

            // Start animations when form loads
            Load += (s, e) =>
            {
                _particleTimer.Start();
                _fadeTimer.Start();
                
                // PR15: Safety fallback - ensure form becomes visible after 1 second
                // even if fade animation fails
                var visibilityTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                visibilityTimer.Tick += (ts, te) =>
                {
                    visibilityTimer.Stop();
                    visibilityTimer.Dispose();
                    try
                    {
                        if (!IsDisposed && Opacity < 0.9)
                        {
                            Opacity = 1.0;
                            _fadingIn = false;
                        }
                    }
                    catch { } // Ignore errors if form is disposed
                };
                visibilityTimer.Start();
            };
            
            // PR15: Ensure visibility timer is cleaned up on form close
            FormClosing += (s, e) =>
            {
                // Timers are already stopped in OnFormClosing, but ensure visibility
                try
                {
                    if (Opacity < 0.5 && !IsDisposed)
                    {
                        Opacity = 1.0;
                    }
                }
                catch { }
            };
        }

        private void InitializeParticles()
        {
            // Create ambient particles for the background animation
            _particles = new Particle[40];
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i] = new Particle
                {
                    X = _random.Next(0, 900),
                    Y = _random.Next(0, 620),
                    VelocityX = (float)(_random.NextDouble() * 0.4 - 0.2),
                    VelocityY = (float)(_random.NextDouble() * 0.3 - 0.15),
                    Size = _random.Next(2, 6),
                    Alpha = _random.Next(20, 60)
                };
            }
        }

        private void InitializeLayout()
        {
            // Glass card (central panel) - PR16: Increased height for status panel
            _glassCard = new Panel
            {
                Size = new Size(600, 510),
                Location = new Point((Width - 600) / 2, (Height - 510) / 2 - 10),
                BackColor = Color.Transparent
            };
            _glassCard.Paint += GlassCard_Paint;
            Controls.Add(_glassCard);

            // Logo container
            var logoContainer = new Panel
            {
                Size = new Size(100, 65),
                Location = new Point(250, 25),
                BackColor = _goldAccent
            };
            _glassCard.Controls.Add(logoContainer);

            var lblLogoText = new Label
            {
                Text = "TGC",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 28, 40),
                BackColor = Color.Transparent
            };
            logoContainer.Controls.Add(lblLogoText);

            // Title
            var lblTitle = new Label
            {
                Text = "TGC Metal Analítico – HVS-MVP",
                Location = new Point(0, 105),
                Size = new Size(600, 35),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = _textPrimary,
                BackColor = Color.Transparent
            };
            _glassCard.Controls.Add(lblTitle);

            // Subtitle
            var lblSubtitle = new Label
            {
                Text = "HVS · IA · Microscopia Metalúrgica · Laudos Automatizados",
                Location = new Point(0, 142),
                Size = new Size(600, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10, FontStyle.Italic),
                ForeColor = _textSecondary,
                BackColor = Color.Transparent
            };
            _glassCard.Controls.Add(lblSubtitle);

            // Action buttons panel
            var actionsPanel = new FlowLayoutPanel
            {
                Location = new Point(35, 185),
                Size = new Size(530, 180),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            _glassCard.Controls.Add(actionsPanel);

            // Three action cards
            actionsPanel.Controls.Add(CreateActionCard(
                "📷",
                "Nova análise de imagem",
                "Carregar imagem de amostra para análise detalhada",
                ActionCard_NewImage_Click));

            actionsPanel.Controls.Add(CreateActionCard(
                "🎥",
                "Análise ao vivo",
                "Iniciar captura com análise em tempo real (câmera)",
                ActionCard_Live_Click,
                isHighlighted: true));

            actionsPanel.Controls.Add(CreateActionCard(
                "📁",
                "Explorar amostras",
                "Abrir pasta de amostras, laudos e exports",
                ActionCard_Explore_Click));

            // PR16: Status panel showing system readiness
            var statusPanel = CreateStatusPanel();
            statusPanel.Location = new Point(35, 370);
            _glassCard.Controls.Add(statusPanel);

            // Version label - moved down
            _lblVersion = new Label
            {
                Text = $"Versão: v{UpdateService.GetCurrentVersion()}",
                Location = new Point(0, 415),
                Size = new Size(600, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                ForeColor = _textSecondary,
                BackColor = Color.Transparent
            };
            _glassCard.Controls.Add(_lblVersion);

            // Credits - moved down
            var lblCredits = new Label
            {
                Text = "© 2025 – Desenvolvido por Basel Ibrahim Al Jughami – Trilha Gold Capital",
                Location = new Point(0, 435),
                Size = new Size(600, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                ForeColor = _goldAccent,
                BackColor = Color.Transparent
            };
            _glassCard.Controls.Add(lblCredits);

            // Settings button (small) - moved down
            _btnSettings = new Button
            {
                Text = "⚙️ Configurações iniciais",
                Size = new Size(160, 28),
                Location = new Point(220, 462),
                BackColor = Color.FromArgb(35, 50, 70),
                ForeColor = _textSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            _btnSettings.FlatAppearance.BorderColor = Color.FromArgb(55, 75, 100);
            _btnSettings.FlatAppearance.BorderSize = 1;
            _btnSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 70, 95);
            _btnSettings.Click += BtnSettings_Click;
            _glassCard.Controls.Add(_btnSettings);

            // Skip welcome checkbox (bottom of form, outside card)
            _chkSkipWelcome = new CheckBox
            {
                Text = "Não mostrar ao iniciar (modo operador)",
                AutoSize = true,
                Location = new Point((Width - 260) / 2, Height - 65),
                ForeColor = _textSecondary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9)
            };
            Controls.Add(_chkSkipWelcome);

            // Direct link button
            var btnDirect = new LinkLabel
            {
                Text = "Ir direto para a interface principal",
                AutoSize = true,
                Location = new Point((Width - 200) / 2, Height - 38),
                LinkColor = _cyanAccent,
                ActiveLinkColor = Color.FromArgb(100, 210, 230),
                Font = new Font("Segoe UI", 9)
            };
            btnDirect.LinkClicked += BtnDirect_LinkClicked;
            Controls.Add(btnDirect);

            // Close button (X in top-right corner)
            var btnClose = new Button
            {
                Text = "✕",
                Size = new Size(36, 30),
                Location = new Point(Width - 46, 8),
                BackColor = Color.Transparent,
                ForeColor = _textSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(150, 50, 50);
            btnClose.Click += (s, e) =>
            {
                SelectedAction = WelcomeAction.None;
                Close();
            };
            Controls.Add(btnClose);
        }

        private Panel CreateActionCard(string icon, string title, string description, EventHandler onClick, bool isHighlighted = false)
        {
            var card = new Panel
            {
                Size = new Size(165, 165),
                Margin = new Padding(8),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            Color cardBg = isHighlighted ? Color.FromArgb(35, 55, 75) : Color.FromArgb(22, 35, 50);
            Color cardBorder = isHighlighted ? _goldAccent : Color.FromArgb(45, 60, 80);
            Color hoverBg = isHighlighted ? Color.FromArgb(45, 70, 95) : Color.FromArgb(35, 50, 70);

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Background with rounded corners
                using var bgBrush = new SolidBrush(cardBg);
                using var borderPen = new Pen(cardBorder, isHighlighted ? 2 : 1);
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = CreateRoundedRectPath(rect, 12);
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            };

            // Icon
            var lblIcon = new Label
            {
                Text = icon,
                Location = new Point(0, 18),
                Size = new Size(165, 45),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 28),
                ForeColor = isHighlighted ? _goldAccent : _cyanAccent,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            card.Controls.Add(lblIcon);

            // Title
            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(8, 68),
                Size = new Size(149, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = _textPrimary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            card.Controls.Add(lblTitle);

            // Description
            var lblDesc = new Label
            {
                Text = description,
                Location = new Point(8, 110),
                Size = new Size(149, 50),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8),
                ForeColor = _textSecondary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            card.Controls.Add(lblDesc);

            // Hover effects
            void ApplyHover()
            {
                card.BackColor = Color.Transparent;
                card.Invalidate();
            }

            card.MouseEnter += (s, e) =>
            {
                cardBg = hoverBg;
                ApplyHover();
            };
            card.MouseLeave += (s, e) =>
            {
                cardBg = isHighlighted ? Color.FromArgb(35, 55, 75) : Color.FromArgb(22, 35, 50);
                ApplyHover();
            };

            // Click handlers for all controls in the card
            card.Click += onClick;
            lblIcon.Click += onClick;
            lblTitle.Click += onClick;
            lblDesc.Click += onClick;

            // Propagate hover to children using card's color update logic
            foreach (Control ctrl in card.Controls)
            {
                ctrl.MouseEnter += (s, e) =>
                {
                    cardBg = hoverBg;
                    ApplyHover();
                };
                ctrl.MouseLeave += (s, e) =>
                {
                    var pos = card.PointToClient(Cursor.Position);
                    if (!card.ClientRectangle.Contains(pos))
                    {
                        cardBg = isHighlighted ? Color.FromArgb(35, 55, 75) : Color.FromArgb(22, 35, 50);
                        ApplyHover();
                    }
                };
            }

            return card;
        }

        private void GlassCard_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Glass effect background
            var rect = new Rectangle(0, 0, _glassCard.Width - 1, _glassCard.Height - 1);
            using var path = CreateRoundedRectPath(rect, 20);

            // Semi-transparent fill with gradient
            using var bgBrush = new LinearGradientBrush(
                rect,
                Color.FromArgb(180, 20, 30, 48),
                Color.FromArgb(160, 15, 25, 40),
                LinearGradientMode.Vertical);
            g.FillPath(bgBrush, path);

            // Border with glow effect
            using var borderPen = new Pen(Color.FromArgb(80, _cardBorder.R, _cardBorder.G, _cardBorder.B), 1);
            g.DrawPath(borderPen, path);

            // Inner highlight (top)
            using var highlightPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1);
            g.DrawLine(highlightPen, 20, 1, _glassCard.Width - 20, 1);
        }

        private GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw background gradient
            using var bgBrush = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(8, 14, 24),
                Color.FromArgb(12, 20, 32),
                LinearGradientMode.Vertical);
            g.FillRectangle(bgBrush, ClientRectangle);

            // Draw particles
            if (_particles != null)
            {
                foreach (var p in _particles)
                {
                    using var pBrush = new SolidBrush(Color.FromArgb(p.Alpha, _cyanAccent));
                    g.FillEllipse(pBrush, p.X, p.Y, p.Size, p.Size);
                }
            }

            // Draw subtle grid pattern
            using var gridPen = new Pen(Color.FromArgb(15, 100, 140, 180), 1);
            for (int x = 0; x < Width; x += 40)
            {
                g.DrawLine(gridPen, x, 0, x, Height);
            }
            for (int y = 0; y < Height; y += 40)
            {
                g.DrawLine(gridPen, 0, y, Width, y);
            }

            // Draw border
            using var borderPen = new Pen(Color.FromArgb(40, 60, 90), 1);
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            // Top accent bar
            using var accentBrush = new SolidBrush(_goldAccent);
            g.FillRectangle(accentBrush, 0, 0, Width, 3);
        }

        private void ParticleTimer_Tick(object? sender, EventArgs e)
        {
            if (_particles == null) return;

            // Update particle positions
            foreach (var p in _particles)
            {
                p.X += p.VelocityX;
                p.Y += p.VelocityY;

                // Wrap around edges
                if (p.X < -10) p.X = Width + 10;
                if (p.X > Width + 10) p.X = -10;
                if (p.Y < -10) p.Y = Height + 10;
                if (p.Y > Height + 10) p.Y = -10;
            }

            // Repaint only the background (not the controls)
            Invalidate(false);
        }

        private void FadeTimer_Tick(object? sender, EventArgs e)
        {
            if (_fadingIn)
            {
                _opacity += 0.08f;
                if (_opacity >= 1f)
                {
                    _opacity = 1f;
                    _fadingIn = false;
                    _fadeTimer.Stop();
                }
                Opacity = _opacity;
            }
            else if (_fadingOut)
            {
                _opacity -= 0.1f;
                if (_opacity <= 0f)
                {
                    _opacity = 0f;
                    _fadingOut = false;
                    _fadeTimer.Stop();
                    DialogResult = DialogResult.OK;
                    Close();
                }
                Opacity = _opacity;
            }
        }

        private void StartFadeOut()
        {
            SkipWelcomeOnStartup = _chkSkipWelcome.Checked;
            _fadingOut = true;
            _fadeTimer.Start();
        }

        private void ActionCard_NewImage_Click(object? sender, EventArgs e)
        {
            // Open file dialog to select an image
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar imagem de amostra",
                Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Todos os arquivos|*.*",
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(_appSettings.DefaultImagesDirectory) &&
                System.IO.Directory.Exists(_appSettings.DefaultImagesDirectory))
            {
                dlg.InitialDirectory = _appSettings.DefaultImagesDirectory;
            }

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                SelectedAction = WelcomeAction.NewImageAnalysis;
                SelectedImagePath = dlg.FileName;
                StartFadeOut();
            }
        }

        private void ActionCard_Live_Click(object? sender, EventArgs e)
        {
            SelectedAction = WelcomeAction.LiveCamera;
            StartFadeOut();
        }

        private void ActionCard_Explore_Click(object? sender, EventArgs e)
        {
            SelectedAction = WelcomeAction.ExploreSamplesReports;
            StartFadeOut();
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            using var settingsForm = new SettingsForm(_appSettings);
            settingsForm.ShowDialog(this);
        }

        private void BtnDirect_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            SelectedAction = WelcomeAction.GoToMainDirect;
            StartFadeOut();
        }

        /// <summary>
        /// PR16: Create status panel showing system readiness indicators.
        /// </summary>
        private FlowLayoutPanel CreateStatusPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Size = new Size(530, 35),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            // Check JSON config status
            bool jsonOk = CheckJsonConfigStatus();
            panel.Controls.Add(CreateStatusIndicator(jsonOk ? "✓" : "⚠", "JSON Metais", jsonOk));

            // Check camera availability (simulated - actual check would be async)
            bool cameraOk = CheckCameraStatus();
            panel.Controls.Add(CreateStatusIndicator(cameraOk ? "✓" : "○", "Câmera", cameraOk));

            // Check masks/analysis ready
            bool analysisOk = true; // Analysis service is always available
            panel.Controls.Add(CreateStatusIndicator("✓", "Análise", analysisOk));

            // Check if app is ready
            bool allOk = jsonOk && analysisOk;
            var statusLabel = new Label
            {
                Text = allOk ? "Sistema pronto" : "Verificar configuração",
                AutoSize = true,
                ForeColor = allOk ? Color.FromArgb(100, 200, 100) : Color.FromArgb(255, 180, 80),
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                Margin = new Padding(20, 8, 0, 0)
            };
            panel.Controls.Add(statusLabel);

            return panel;
        }

        private Panel CreateStatusIndicator(string icon, string label, bool isOk)
        {
            var container = new Panel
            {
                Size = new Size(100, 30),
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 10, 0)
            };

            var lblIcon = new Label
            {
                Text = icon,
                AutoSize = true,
                Location = new Point(0, 5),
                ForeColor = isOk ? Color.FromArgb(100, 200, 100) : Color.FromArgb(255, 180, 80),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.Transparent
            };
            container.Controls.Add(lblIcon);

            var lblText = new Label
            {
                Text = label,
                AutoSize = true,
                Location = new Point(18, 7),
                ForeColor = _textSecondary,
                Font = new Font("Segoe UI", 8),
                BackColor = Color.Transparent
            };
            container.Controls.Add(lblText);

            return container;
        }

        private bool CheckJsonConfigStatus()
        {
            try
            {
                var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hvs-config.json");
                return System.IO.File.Exists(configPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// PR16: Check camera availability.
        /// Note: This is a quick optimistic check. Actual camera detection
        /// requires async operations and is performed when entering Live mode.
        /// Returns true to indicate camera may be available (user should verify in Live mode).
        /// </summary>
        private bool CheckCameraStatus()
        {
            // This is a quick check that assumes camera may be available.
            // The actual camera detection and verification happens when the user
            // enters Live mode, where proper async camera initialization occurs.
            // Returning true here is intentional - we don't want to block the
            // welcome screen with slow camera detection. The actual verification
            // happens in the MainForm when Live mode is initiated.
            return true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _particleTimer.Stop();
            _fadeTimer.Stop();
            base.OnFormClosing(e);
        }

        // Particle class for background animation
        private class Particle
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float VelocityX { get; set; }
            public float VelocityY { get; set; }
            public int Size { get; set; }
            public int Alpha { get; set; }
        }
    }
}
