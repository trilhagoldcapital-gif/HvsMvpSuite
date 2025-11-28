using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// About dialog showing app info, version, logo, copyright and contact.
    /// </summary>
    public class AboutForm : Form
    {
        private readonly AppSettings _settings;

        public AboutForm(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Sobre o MicroLab HVS-MVP";
            Size = new Size(450, 380);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(20, 25, 35);
            ForeColor = Color.WhiteSmoke;

            int y = 20;

            // Logo area
            var pnlLogo = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(80, 80),
                BackColor = Color.FromArgb(200, 160, 60),
                BorderStyle = BorderStyle.FixedSingle
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
                        Size = new Size(80, 80),
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
                    Size = new Size(80, 30),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 16, FontStyle.Bold),
                    ForeColor = Color.FromArgb(20, 20, 30),
                    BackColor = Color.Transparent
                };
                pnlLogo.Controls.Add(lblLogoText);
            }

            // App name
            var lblAppName = new Label
            {
                Text = "MicroLab HVS-MVP",
                Location = new Point(115, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(lblAppName);

            y += 35;

            // Subtitle
            var lblSubtitle = new Label
            {
                Text = "TGC Metal Analítico",
                Location = new Point(115, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.Gainsboro
            };
            Controls.Add(lblSubtitle);

            y += 25;

            // Version
            string version = UpdateService.GetCurrentVersion();
            var lblVersion = new Label
            {
                Text = $"Versão {version}",
                Location = new Point(115, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(150, 160, 180)
            };
            Controls.Add(lblVersion);

            y = 120;

            // Separator
            var pnlSeparator = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(395, 1),
                BackColor = Color.FromArgb(60, 70, 90)
            };
            Controls.Add(pnlSeparator);

            y += 20;

            // Description
            var lblDescription = new Label
            {
                Text = "Sistema profissional de análise microscópica de metais,\n" +
                       "cristais e gemas com foco em detecção de metais nobres\n" +
                       "(Ouro, Platina, PGMs).",
                Location = new Point(20, y),
                Size = new Size(400, 55),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.Gainsboro
            };
            Controls.Add(lblDescription);

            y += 65;

            // Lab name (if configured)
            if (!string.IsNullOrWhiteSpace(_settings.LabName))
            {
                var lblLab = new Label
                {
                    Text = $"Laboratório: {_settings.LabName}",
                    Location = new Point(20, y),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.FromArgb(180, 190, 210)
                };
                Controls.Add(lblLab);
                y += 25;
            }

            y += 10;

            // Copyright
            var lblCopyright = new Label
            {
                Text = "© 2025 Trilha Gold Capital – MicroLab",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 160, 180)
            };
            Controls.Add(lblCopyright);

            y += 25;

            // Contact
            var lblContact = new Label
            {
                Text = "Contato: suporte@trilhagoldcapital.com",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 160, 180)
            };
            Controls.Add(lblContact);

            y += 25;

            // Website
            var lblWebsite = new Label
            {
                Text = "github.com/trilhagoldcapital-gif/HvsMvpSuite",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(100, 140, 200),
                Cursor = Cursors.Hand
            };
            lblWebsite.Click += (s, e) =>
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

            // OK button
            var btnOk = new Button
            {
                Text = "OK",
                Location = new Point(335, 300),
                Size = new Size(80, 32),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(50, 80, 110),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(70, 100, 140);
            Controls.Add(btnOk);

            AcceptButton = btnOk;
        }
    }
}
