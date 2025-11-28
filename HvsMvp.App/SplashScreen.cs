using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR9: Modernized splash screen with improved visuals.
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

        // PR9: Reduced splash time for better UX
        private const int MinDisplayTimeMs = 3000;
        private const int MaxDisplayTimeMs = 5000;
        private const int AnimationIntervalMs = 25;

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
            Size = new Size(520, 340);
            BackColor = Color.FromArgb(12, 18, 28);
            ShowInTaskbar = false;
            Opacity = 0;
            TopMost = true;

            // PR9: Gold accent bar at top (thicker for visual impact)
            var topBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(520, 4),
                BackColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(topBar);

            // PR9: Logo container with rounded appearance effect
            var logoContainer = new Panel
            {
                Location = new Point(185, 35),
                Size = new Size(150, 95),
                BackColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(logoContainer);

            var lblLogoText = new Label
            {
                Text = "TGC",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 32, FontStyle.Bold),
                ForeColor = Color.FromArgb(12, 18, 28),
                BackColor = Color.Transparent
            };
            logoContainer.Controls.Add(lblLogoText);

            // Company name
            var lblCompany = new Label
            {
                Text = "TRILHA GOLD CAPITAL",
                Location = new Point(0, 145),
                Size = new Size(520, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(lblCompany);

            // App name - larger and more prominent
            var lblApp = new Label
            {
                Text = "TGC Metal Analítico – HVS-MVP",
                Location = new Point(0, 180),
                Size = new Size(520, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                ForeColor = Color.WhiteSmoke
            };
            Controls.Add(lblApp);

            // Subtitle
            var lblSubtitle = new Label
            {
                Text = "Análise Microscópica de Metais · Cristais · Gemas",
                Location = new Point(0, 212),
                Size = new Size(520, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(140, 155, 175)
            };
            Controls.Add(lblSubtitle);

            // PR9: Progress bar background (wider, more visible)
            var progressBg = new Panel
            {
                Location = new Point(60, 260),
                Size = new Size(400, 8),
                BackColor = Color.FromArgb(35, 45, 60)
            };
            Controls.Add(progressBg);

            // Progress bar foreground
            _progressBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, 8),
                BackColor = Color.FromArgb(200, 160, 60)
            };
            progressBg.Controls.Add(_progressBar);

            // Status label - more prominent
            _lblStatus = new Label
            {
                Text = "Carregando módulos de análise...",
                Location = new Point(0, 278),
                Size = new Size(520, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(140, 150, 170)
            };
            Controls.Add(_lblStatus);

            // Version label
            var lblVersion = new Label
            {
                Text = $"v{UpdateService.GetCurrentVersion()}",
                Location = new Point(0, 310),
                Size = new Size(520, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(90, 100, 120)
            };
            Controls.Add(lblVersion);

            // PR9: Gold accent bar at bottom
            var bottomBar = new Panel
            {
                Location = new Point(0, 336),
                Size = new Size(520, 4),
                BackColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(bottomBar);
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
                _opacity += 0.12;
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
                _opacity -= 0.1;
                if (_opacity <= 0)
                {
                    _opacity = 0;
                    _animationTimer.Stop();
                    Close();
                    return;
                }
                Opacity = _opacity;
            }

            // Progress bar animation - PR9: faster fill to match shorter display time
            if (!_fadingOut && _progressValue < 400)
            {
                _progressValue += 4;
                if (_progressValue > 400) _progressValue = 400;
                _progressBar.Width = _progressValue;

                // Update status text based on progress
                if (_progressValue < 80)
                    _lblStatus.Text = "Carregando módulos de análise...";
                else if (_progressValue < 160)
                    _lblStatus.Text = "Inicializando câmera...";
                else if (_progressValue < 280)
                    _lblStatus.Text = "Preparando interface...";
                else if (_progressValue < 380)
                    _lblStatus.Text = "Carregando configurações HVS...";
                else
                    _lblStatus.Text = "Pronto!";
            }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // PR9: Draw subtle border with slight gradient effect
            using var pen = new Pen(Color.FromArgb(45, 55, 75), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _animationTimer.Stop();
            _closeTimer.Stop();
            base.OnFormClosing(e);
        }
    }
}
