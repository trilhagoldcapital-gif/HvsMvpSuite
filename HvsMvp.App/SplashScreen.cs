using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR10: Modernized splash screen with futuristic 3D-style design.
    /// </summary>
    public class SplashScreen : Form
    {
        private readonly System.Windows.Forms.Timer _animationTimer;
        private readonly System.Windows.Forms.Timer _closeTimer;
        private int _progressValue = 0;
        private Label _lblStatus = null!;
        private Panel _progressBar = null!;
        private double _opacity = 0;
        private bool _fadingIn = true;
        private bool _fadingOut = false;

        // PR10: Faster splash for better UX
        private const int MinDisplayTimeMs = 2000;
        private const int MaxDisplayTimeMs = 4000;
        private const int AnimationIntervalMs = 20;

        // Color palette
        private readonly Color _bgColor = Color.FromArgb(8, 14, 24);
        private readonly Color _goldAccent = Color.FromArgb(200, 160, 60);
        private readonly Color _textPrimary = Color.FromArgb(230, 235, 245);
        private readonly Color _textSecondary = Color.FromArgb(150, 165, 185);

        public SplashScreen()
        {
            InitializeComponent();

            _animationTimer = new System.Windows.Forms.Timer { Interval = AnimationIntervalMs };
            _animationTimer.Tick += AnimationTimer_Tick;

            _closeTimer = new System.Windows.Forms.Timer { Interval = MinDisplayTimeMs };
            _closeTimer.Tick += CloseTimer_Tick;
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(560, 360);
            BackColor = _bgColor;
            ShowInTaskbar = false;
            Opacity = 0;
            TopMost = true;
            DoubleBuffered = true;

            // Logo container with gold background
            var logoContainer = new Panel
            {
                Location = new Point(205, 35),
                Size = new Size(150, 95),
                BackColor = _goldAccent
            };
            Controls.Add(logoContainer);

            var lblLogoText = new Label
            {
                Text = "TGC",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 36, FontStyle.Bold),
                ForeColor = Color.FromArgb(12, 18, 28),
                BackColor = Color.Transparent
            };
            logoContainer.Controls.Add(lblLogoText);

            // Company name
            var lblCompany = new Label
            {
                Text = "TRILHA GOLD CAPITAL",
                Location = new Point(0, 145),
                Size = new Size(560, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = _goldAccent
            };
            Controls.Add(lblCompany);

            // App name
            var lblApp = new Label
            {
                Text = "TGC Metal Analítico – HVS-MVP",
                Location = new Point(0, 182),
                Size = new Size(560, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14),
                ForeColor = _textPrimary
            };
            Controls.Add(lblApp);

            // Subtitle
            var lblSubtitle = new Label
            {
                Text = "Análise Microscópica · Metais · Cristais · Gemas",
                Location = new Point(0, 215),
                Size = new Size(560, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textSecondary
            };
            Controls.Add(lblSubtitle);

            // Progress bar background
            var progressBg = new Panel
            {
                Location = new Point(80, 265),
                Size = new Size(400, 8),
                BackColor = Color.FromArgb(25, 35, 50)
            };
            Controls.Add(progressBg);

            // Progress bar foreground
            _progressBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, 8),
                BackColor = _goldAccent
            };
            progressBg.Controls.Add(_progressBar);

            // Status label
            _lblStatus = new Label
            {
                Text = "Carregando módulos de análise...",
                Location = new Point(0, 283),
                Size = new Size(560, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                ForeColor = _textSecondary
            };
            Controls.Add(_lblStatus);

            // Version label
            var lblVersion = new Label
            {
                Text = $"v{UpdateService.GetCurrentVersion()}",
                Location = new Point(0, 320),
                Size = new Size(560, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(90, 100, 120)
            };
            Controls.Add(lblVersion);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background gradient
            using var bgBrush = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(8, 14, 24),
                Color.FromArgb(12, 22, 36),
                LinearGradientMode.Vertical);
            g.FillRectangle(bgBrush, ClientRectangle);

            // Top gold accent bar
            using var accentBrush = new SolidBrush(_goldAccent);
            g.FillRectangle(accentBrush, 0, 0, Width, 4);

            // Bottom gold accent bar
            g.FillRectangle(accentBrush, 0, Height - 4, Width, 4);

            // Subtle border
            using var borderPen = new Pen(Color.FromArgb(40, 55, 75), 1);
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

        public void ShowSplash()
        {
            Show();
            _animationTimer.Start();
            _closeTimer.Start();
        }

        public void CloseSplash()
        {
            if (_fadingOut) return;

            _closeTimer.Stop();
            _fadingOut = true;
            _fadingIn = false;
        }

        public void UpdateStatus(string status)
        {
            if (_lblStatus.InvokeRequired)
            {
                _lblStatus.Invoke(new Action(() => _lblStatus.Text = status));
            }
            else
            {
                _lblStatus.Text = status;
            }
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            // Fade in
            if (_fadingIn)
            {
                _opacity += 0.15;
                if (_opacity >= 1.0)
                {
                    _opacity = 1.0;
                    _fadingIn = false;
                }
                Opacity = _opacity;
            }

            // Fade out
            if (_fadingOut)
            {
                _opacity -= 0.12;
                if (_opacity <= 0)
                {
                    _opacity = 0;
                    _animationTimer.Stop();
                    Close();
                    return;
                }
                Opacity = _opacity;
            }

            // Progress bar animation - faster fill
            if (!_fadingOut && _progressValue < 400)
            {
                _progressValue += 6;
                if (_progressValue > 400) _progressValue = 400;
                _progressBar.Width = _progressValue;

                // Update status text based on progress
                if (_progressValue < 100)
                    _lblStatus.Text = "Carregando módulos de análise...";
                else if (_progressValue < 200)
                    _lblStatus.Text = "Inicializando serviços de IA...";
                else if (_progressValue < 300)
                    _lblStatus.Text = "Preparando interface...";
                else if (_progressValue < 380)
                    _lblStatus.Text = "Carregando configurações HVS...";
                else
                    _lblStatus.Text = "Pronto!";
            }

            Invalidate(false);
        }

        private void CloseTimer_Tick(object? sender, EventArgs e)
        {
            _closeTimer.Stop();
            CloseSplash();
        }

        /// <summary>
        /// Set maximum display time safety timeout.
        /// </summary>
        public void SetMaxTimeout(int milliseconds = MaxDisplayTimeMs)
        {
            Task.Delay(milliseconds).ContinueWith(_ =>
            {
                try
                {
                    if (!IsDisposed && Visible && IsHandleCreated)
                    {
                        BeginInvoke(new Action(CloseSplash));
                    }
                }
                catch
                {
                    // Ignore errors during shutdown
                }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _animationTimer.Stop();
            _closeTimer.Stop();
            base.OnFormClosing(e);
        }
    }
}
