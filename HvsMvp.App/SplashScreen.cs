using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// Splash screen / welcome screen shown during app startup.
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

        private const int MinDisplayTimeMs = 2000;
        private const int MaxDisplayTimeMs = 7000;
        private const int AnimationIntervalMs = 30;

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
            Size = new Size(500, 320);
            BackColor = Color.FromArgb(15, 20, 30);
            ShowInTaskbar = false;
            Opacity = 0;
            TopMost = true;

            // Gold accent bar at top
            var topBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(500, 6),
                BackColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(topBar);

            // Logo area
            var pnlLogo = new Panel
            {
                Location = new Point(190, 40),
                Size = new Size(120, 80),
                BackColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(pnlLogo);

            var lblLogoText = new Label
            {
                Text = "TGC",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 20, 30),
                BackColor = Color.Transparent
            };
            pnlLogo.Controls.Add(lblLogoText);

            // Company name
            var lblCompany = new Label
            {
                Text = "TRILHA GOLD CAPITAL",
                Location = new Point(0, 135),
                Size = new Size(500, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(lblCompany);

            // App name
            var lblApp = new Label
            {
                Text = "MicroLab HVS Minerais",
                Location = new Point(0, 168),
                Size = new Size(500, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13),
                ForeColor = Color.WhiteSmoke
            };
            Controls.Add(lblApp);

            // Subtitle
            var lblSubtitle = new Label
            {
                Text = "Análise Microscópica de Metais · Cristais · Gemas",
                Location = new Point(0, 198),
                Size = new Size(500, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 160, 180)
            };
            Controls.Add(lblSubtitle);

            // Progress bar background
            var progressBg = new Panel
            {
                Location = new Point(50, 250),
                Size = new Size(400, 6),
                BackColor = Color.FromArgb(40, 50, 65)
            };
            Controls.Add(progressBg);

            // Progress bar foreground
            _progressBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, 6),
                BackColor = Color.FromArgb(200, 160, 60)
            };
            progressBg.Controls.Add(_progressBar);

            // Status label
            _lblStatus = new Label
            {
                Text = "Carregando...",
                Location = new Point(0, 268),
                Size = new Size(500, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(120, 130, 150)
            };
            Controls.Add(_lblStatus);

            // Version label
            var lblVersion = new Label
            {
                Text = $"v{UpdateService.GetCurrentVersion()}",
                Location = new Point(0, 295),
                Size = new Size(500, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(80, 90, 110)
            };
            Controls.Add(lblVersion);

            // Gold accent bar at bottom
            var bottomBar = new Panel
            {
                Location = new Point(0, 314),
                Size = new Size(500, 6),
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
                _opacity += 0.08;
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
                _opacity -= 0.08;
                if (_opacity <= 0)
                {
                    _opacity = 0;
                    _animationTimer.Stop();
                    Close();
                    return;
                }
                Opacity = _opacity;
            }

            // Progress bar animation
            if (!_fadingOut && _progressValue < 400)
            {
                _progressValue += 8;
                if (_progressValue > 400) _progressValue = 400;
                _progressBar.Width = _progressValue;

                // Update status text based on progress
                if (_progressValue < 100)
                    _lblStatus.Text = "Carregando configurações...";
                else if (_progressValue < 200)
                    _lblStatus.Text = "Inicializando serviços...";
                else if (_progressValue < 300)
                    _lblStatus.Text = "Preparando interface...";
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

            // Draw subtle border
            using var pen = new Pen(Color.FromArgb(50, 60, 80), 1);
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
