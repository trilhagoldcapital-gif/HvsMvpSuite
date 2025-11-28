using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR10: Modernized About dialog with futuristic design.
    /// Shows app info, version, logo, copyright and contact.
    /// </summary>
    public class AboutForm : Form
    {
        private readonly AppSettings _settings;

        // Color palette (consistent with WelcomeScreen)
        private readonly Color _bgColor = Color.FromArgb(12, 20, 32);
        private readonly Color _cardBg = Color.FromArgb(20, 32, 48);
        private readonly Color _goldAccent = Color.FromArgb(200, 160, 60);
        private readonly Color _cyanAccent = Color.FromArgb(60, 180, 200);
        private readonly Color _textPrimary = Color.FromArgb(230, 235, 245);
        private readonly Color _textSecondary = Color.FromArgb(150, 165, 185);

        public AboutForm(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Sobre o MicroLab HVS-MVP";
            Size = new Size(520, 440);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = _bgColor;
            ForeColor = _textPrimary;

            // Top accent bar
            var topBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(520, 4),
                BackColor = _goldAccent
            };
            Controls.Add(topBar);

            int y = 25;

            // Logo area with gold background
            var pnlLogo = new Panel
            {
                Location = new Point(30, y),
                Size = new Size(90, 90),
                BackColor = _goldAccent
            };
            Controls.Add(pnlLogo);

            // Try to load custom logo
            PictureBox? picLogo = null;
            if (!string.IsNullOrWhiteSpace(_settings.LogoPath) && File.Exists(_settings.LogoPath))
            {
                try
                {
                    picLogo = new PictureBox
                    {
                        Location = new Point(0, 0),
                        Size = new Size(90, 90),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = Image.FromFile(_settings.LogoPath)
                    };
                    pnlLogo.Controls.Add(picLogo);
                }
                catch
                {
                    // Use default styling if logo fails to load
                }
            }

            if (picLogo == null)
            {
                // Default logo text
                var lblLogoText = new Label
                {
                    Text = "TGC",
                    Location = new Point(0, 25),
                    Size = new Size(90, 40),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 22, FontStyle.Bold),
                    ForeColor = Color.FromArgb(20, 28, 40),
                    BackColor = Color.Transparent
                };
                pnlLogo.Controls.Add(lblLogoText);
            }

            // App name
            var lblAppName = new Label
            {
                Text = "MicroLab HVS-MVP",
                Location = new Point(140, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = _goldAccent
            };
            Controls.Add(lblAppName);

            y += 38;

            // Subtitle
            var lblSubtitle = new Label
            {
                Text = "TGC Metal Analítico",
                Location = new Point(140, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 12),
                ForeColor = _textPrimary
            };
            Controls.Add(lblSubtitle);

            y += 28;

            // Version
            string version = UpdateService.GetCurrentVersion();
            var lblVersion = new Label
            {
                Text = $"Versão {version}",
                Location = new Point(140, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = _textSecondary
            };
            Controls.Add(lblVersion);

            y = 135;

            // Separator
            var pnlSeparator = new Panel
            {
                Location = new Point(30, y),
                Size = new Size(445, 1),
                BackColor = Color.FromArgb(50, 65, 85)
            };
            Controls.Add(pnlSeparator);

            y += 20;

            // Description card
            var descCard = new Panel
            {
                Location = new Point(30, y),
                Size = new Size(445, 80),
                BackColor = _cardBg
            };
            descCard.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(45, 60, 80), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, descCard.Width - 1, descCard.Height - 1);
            };
            Controls.Add(descCard);

            var lblDescription = new Label
            {
                Text = "Sistema profissional de análise microscópica de metais,\n" +
                       "cristais e gemas com foco em detecção de metais nobres\n" +
                       "(Ouro, Platina, PGMs). Inclui IA, análise seletiva e laudos.",
                Location = new Point(15, 12),
                Size = new Size(415, 60),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textPrimary
            };
            descCard.Controls.Add(lblDescription);

            y += 95;

            // Lab name (if configured)
            if (!string.IsNullOrWhiteSpace(_settings.LabName))
            {
                var lblLab = new Label
                {
                    Text = $"Laboratório: {_settings.LabName}",
                    Location = new Point(30, y),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = _textSecondary
                };
                Controls.Add(lblLab);
                y += 26;
            }

            y += 10;

            // Main copyright (prominent)
            var lblCopyright = new Label
            {
                Text = "© 2025 – Desenvolvido por Basel Ibrahim Al Jughami",
                Location = new Point(30, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = _goldAccent
            };
            Controls.Add(lblCopyright);

            y += 26;

            // Company
            var lblCompany = new Label
            {
                Text = "Trilha Gold Capital",
                Location = new Point(30, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = _textPrimary
            };
            Controls.Add(lblCompany);

            y += 30;

            // Contact
            var lblContact = new Label
            {
                Text = "📧 suporte@trilhagoldcapital.com",
                Location = new Point(30, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                ForeColor = _textSecondary
            };
            Controls.Add(lblContact);

            y += 26;

            // Website link
            var lblWebsite = new LinkLabel
            {
                Text = "🔗 github.com/trilhagoldcapital-gif/HvsMvpSuite",
                Location = new Point(30, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                LinkColor = _cyanAccent,
                ActiveLinkColor = Color.FromArgb(100, 210, 230)
            };
            lblWebsite.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/trilhagoldcapital-gif/HvsMvpSuite",
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            Controls.Add(lblWebsite);

            // OK button (styled)
            var btnOk = new Button
            {
                Text = "OK",
                Location = new Point(395, 360),
                Size = new Size(90, 34),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(45, 70, 100),
                ForeColor = _textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(65, 95, 130);
            btnOk.FlatAppearance.BorderSize = 1;
            btnOk.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 90, 125);
            Controls.Add(btnOk);

            AcceptButton = btnOk;

            // Bottom accent bar
            var bottomBar = new Panel
            {
                Location = new Point(0, 404),
                Size = new Size(520, 3),
                BackColor = _goldAccent
            };
            Controls.Add(bottomBar);
        }
    }
}
