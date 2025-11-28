using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// Settings form with tabs for General, Camera, Analysis, Updates, and Profile settings.
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private TabControl _tabControl = null!;

        // General tab controls
        private TextBox _txtImagesDir = null!;
        private TextBox _txtReportsDir = null!;
        private TextBox _txtSessionsDir = null!;
        private TextBox _txtLogsDir = null!;

        // Camera tab controls
        private ComboBox _cbCameraIndex = null!;
        private ComboBox _cbResolution = null!;

        // Analysis tab controls
        private TrackBar _trackMaskSensitivity = null!;
        private Label _lblMaskValue = null!;
        private NumericUpDown _nudFocusThreshold = null!;
        private NumericUpDown _nudClippingThreshold = null!;
        private CheckBox _chkStrongWarnings = null!;

        // Updates tab controls
        private CheckBox _chkAutoUpdate = null!;
        private Label _lblUpdateFrequency = null!;
        private Label _lblLastCheck = null!;
        private Label _lblLastResult = null!;
        private Button _btnCheckNow = null!;

        // Profile tab controls
        private TextBox _txtLabName = null!;
        private TextBox _txtLogoPath = null!;
        private TextBox _txtOperator = null!;
        private TextBox _txtWhatsAppContact = null!;

        // Paths info
        private Label _lblCurrentPaths = null!;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            LoadSettingsToUI();
        }

        private void InitializeComponent()
        {
            Text = "Configurações";
            Size = new Size(600, 550);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 35, 45);
            ForeColor = Color.WhiteSmoke;

            _tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(565, 400),
                Font = new Font("Segoe UI", 9),
                Padding = new Point(10, 5)
            };

            CreateGeneralTab();
            CreateCameraTab();
            CreateAnalysisTab();
            CreateUpdatesTab();
            CreateProfileTab();

            Controls.Add(_tabControl);

            // Current paths info
            _lblCurrentPaths = new Label
            {
                Location = new Point(10, 420),
                Size = new Size(565, 45),
                ForeColor = Color.FromArgb(150, 160, 180),
                Font = new Font("Segoe UI", 8),
                Text = "Caminhos em uso serão atualizados ao salvar."
            };
            Controls.Add(_lblCurrentPaths);

            // Buttons
            var btnRestoreDefaults = new Button
            {
                Text = "🔄 Restaurar padrão",
                Location = new Point(10, 470),
                Size = new Size(150, 32),
                BackColor = Color.FromArgb(80, 50, 50),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            btnRestoreDefaults.FlatAppearance.BorderColor = Color.FromArgb(120, 80, 80);
            btnRestoreDefaults.Click += BtnRestoreDefaults_Click;
            Controls.Add(btnRestoreDefaults);

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(380, 470),
                Size = new Size(90, 32),
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(50, 55, 65),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(80, 85, 95);
            Controls.Add(btnCancel);

            var btnSave = new Button
            {
                Text = "💾 Salvar",
                Location = new Point(480, 470),
                Size = new Size(90, 32),
                BackColor = Color.FromArgb(40, 100, 60),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 140, 80);
            btnSave.Click += BtnSave_Click;
            Controls.Add(btnSave);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private TabPage CreateTabPage(string title)
        {
            var page = new TabPage(title)
            {
                BackColor = Color.FromArgb(35, 40, 50),
                ForeColor = Color.WhiteSmoke,
                Padding = new Padding(15)
            };
            return page;
        }

        private void CreateGeneralTab()
        {
            var tab = CreateTabPage("Geral");

            int y = 20;
            int labelWidth = 140;
            int textWidth = 320;

            AddLabel(tab, "Diretório de imagens:", 15, y);
            _txtImagesDir = AddTextBox(tab, labelWidth + 20, y - 3, textWidth);
            AddBrowseButton(tab, labelWidth + textWidth + 25, y - 3, () => BrowseFolder(_txtImagesDir));

            y += 40;
            AddLabel(tab, "Diretório de laudos:", 15, y);
            _txtReportsDir = AddTextBox(tab, labelWidth + 20, y - 3, textWidth);
            AddBrowseButton(tab, labelWidth + textWidth + 25, y - 3, () => BrowseFolder(_txtReportsDir));

            y += 40;
            AddLabel(tab, "Diretório de sessões:", 15, y);
            _txtSessionsDir = AddTextBox(tab, labelWidth + 20, y - 3, textWidth);
            AddBrowseButton(tab, labelWidth + textWidth + 25, y - 3, () => BrowseFolder(_txtSessionsDir));

            y += 40;
            AddLabel(tab, "Diretório de logs:", 15, y);
            _txtLogsDir = AddTextBox(tab, labelWidth + 20, y - 3, textWidth);
            AddBrowseButton(tab, labelWidth + textWidth + 25, y - 3, () => BrowseFolder(_txtLogsDir));

            _tabControl.TabPages.Add(tab);
        }

        private void CreateCameraTab()
        {
            var tab = CreateTabPage("Câmera");

            int y = 20;

            AddLabel(tab, "Índice de câmera padrão:", 15, y);
            _cbCameraIndex = new ComboBox
            {
                Location = new Point(180, y - 3),
                Size = new Size(80, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.WhiteSmoke
            };
            for (int i = 0; i <= 4; i++)
                _cbCameraIndex.Items.Add(i.ToString());
            tab.Controls.Add(_cbCameraIndex);

            y += 45;
            AddLabel(tab, "Resolução preferida:", 15, y);
            _cbResolution = new ComboBox
            {
                Location = new Point(180, y - 3),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.WhiteSmoke
            };
            _cbResolution.Items.AddRange(new[] { "640x480", "800x600", "1280x720", "1920x1080" });
            tab.Controls.Add(_cbResolution);

            _tabControl.TabPages.Add(tab);
        }

        private void CreateAnalysisTab()
        {
            var tab = CreateTabPage("Análise");

            int y = 20;

            AddLabel(tab, "Sensibilidade da máscara:", 15, y);
            _trackMaskSensitivity = new TrackBar
            {
                Location = new Point(180, y - 5),
                Size = new Size(250, 45),
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10,
                Value = 30,
                BackColor = Color.FromArgb(35, 40, 50)
            };
            _trackMaskSensitivity.ValueChanged += (s, e) =>
            {
                _lblMaskValue.Text = $"{_trackMaskSensitivity.Value / 100.0:F2}";
            };
            tab.Controls.Add(_trackMaskSensitivity);

            _lblMaskValue = new Label
            {
                Location = new Point(440, y),
                Size = new Size(50, 20),
                Text = "0.30",
                ForeColor = Color.Gainsboro
            };
            tab.Controls.Add(_lblMaskValue);

            y += 55;
            AddLabel(tab, "Limiar de foco mínimo:", 15, y);
            _nudFocusThreshold = new NumericUpDown
            {
                Location = new Point(180, y - 3),
                Size = new Size(80, 25),
                DecimalPlaces = 2,
                Increment = 0.01M,
                Minimum = 0,
                Maximum = 1,
                Value = 0.15M,
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.WhiteSmoke
            };
            tab.Controls.Add(_nudFocusThreshold);

            y += 40;
            AddLabel(tab, "Limiar de clipping (%):", 15, y);
            _nudClippingThreshold = new NumericUpDown
            {
                Location = new Point(180, y - 3),
                Size = new Size(80, 25),
                DecimalPlaces = 3,
                Increment = 0.005M,
                Minimum = 0,
                Maximum = 0.5M,
                Value = 0.025M,
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.WhiteSmoke
            };
            tab.Controls.Add(_nudClippingThreshold);

            y += 45;
            _chkStrongWarnings = new CheckBox
            {
                Location = new Point(15, y),
                Size = new Size(400, 25),
                Text = "Ativar avisos fortes (bloquear ao invés de apenas alertar)",
                ForeColor = Color.Gainsboro
            };
            tab.Controls.Add(_chkStrongWarnings);

            _tabControl.TabPages.Add(tab);
        }

        private void CreateUpdatesTab()
        {
            var tab = CreateTabPage("Atualizações");

            int y = 20;

            _chkAutoUpdate = new CheckBox
            {
                Location = new Point(15, y),
                Size = new Size(400, 25),
                Text = "Verificar atualizações automaticamente ao iniciar",
                ForeColor = Color.Gainsboro
            };
            tab.Controls.Add(_chkAutoUpdate);

            y += 35;
            _lblUpdateFrequency = new Label
            {
                Location = new Point(15, y),
                Size = new Size(450, 20),
                Text = "Frequência: a cada 24 horas (configuração fixa)",
                ForeColor = Color.FromArgb(150, 160, 180)
            };
            tab.Controls.Add(_lblUpdateFrequency);

            y += 35;
            _lblLastCheck = new Label
            {
                Location = new Point(15, y),
                Size = new Size(450, 20),
                Text = "Última verificação: nunca",
                ForeColor = Color.Gainsboro
            };
            tab.Controls.Add(_lblLastCheck);

            y += 25;
            _lblLastResult = new Label
            {
                Location = new Point(15, y),
                Size = new Size(450, 20),
                Text = "Resultado: não verificado",
                ForeColor = Color.Gainsboro
            };
            tab.Controls.Add(_lblLastResult);

            y += 40;
            _btnCheckNow = new Button
            {
                Location = new Point(15, y),
                Size = new Size(180, 32),
                Text = "🔍 Verificar agora",
                BackColor = Color.FromArgb(50, 80, 110),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            _btnCheckNow.FlatAppearance.BorderColor = Color.FromArgb(70, 100, 140);
            _btnCheckNow.Click += BtnCheckNow_Click;
            tab.Controls.Add(_btnCheckNow);

            _tabControl.TabPages.Add(tab);
        }

        private void CreateProfileTab()
        {
            var tab = CreateTabPage("Perfil");

            int y = 20;
            int labelWidth = 160;
            int textWidth = 320;

            AddLabel(tab, "Nome do laboratório:", 15, y);
            _txtLabName = AddTextBox(tab, labelWidth + 10, y - 3, textWidth);

            y += 40;
            AddLabel(tab, "Caminho do logo:", 15, y);
            _txtLogoPath = AddTextBox(tab, labelWidth + 10, y - 3, textWidth - 80);
            AddBrowseButton(tab, labelWidth + textWidth - 65, y - 3, () => BrowseLogoFile());

            y += 40;
            AddLabel(tab, "Operador padrão:", 15, y);
            _txtOperator = AddTextBox(tab, labelWidth + 10, y - 3, textWidth);

            y += 40;
            AddLabel(tab, "Contato WhatsApp:", 15, y);
            _txtWhatsAppContact = AddTextBox(tab, labelWidth + 10, y - 3, textWidth);

            y += 30;
            var lblWhatsAppHint = new Label
            {
                Location = new Point(labelWidth + 10, y),
                Size = new Size(textWidth, 35),
                Text = "Formato: +5511999999999 ou nome do grupo.\nSerá incluído na mensagem padrão de compartilhamento.",
                ForeColor = Color.FromArgb(150, 160, 180),
                Font = new Font("Segoe UI", 8)
            };
            tab.Controls.Add(lblWhatsAppHint);

            _tabControl.TabPages.Add(tab);
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

        private TextBox AddTextBox(Control parent, int x, int y, int width)
        {
            var txt = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 25),
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(txt);
            return txt;
        }

        private Button AddBrowseButton(Control parent, int x, int y, Action onClick)
        {
            var btn = new Button
            {
                Text = "...",
                Location = new Point(x, y),
                Size = new Size(35, 25),
                BackColor = Color.FromArgb(50, 55, 65),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 85, 95);
            btn.Click += (s, e) => onClick();
            parent.Controls.Add(btn);
            return btn;
        }

        private void BrowseFolder(TextBox targetTextBox)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Selecione um diretório",
                ShowNewFolderButton = true
            };
            if (!string.IsNullOrWhiteSpace(targetTextBox.Text) && Directory.Exists(targetTextBox.Text))
                dlg.SelectedPath = targetTextBox.Text;

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                targetTextBox.Text = dlg.SelectedPath;
            }
        }

        private void BrowseLogoFile()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar logo",
                Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos os arquivos|*.*"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _txtLogoPath.Text = dlg.FileName;
            }
        }

        private void LoadSettingsToUI()
        {
            // General
            _txtImagesDir.Text = _settings.DefaultImagesDirectory;
            _txtReportsDir.Text = _settings.ReportsDirectory;
            _txtSessionsDir.Text = _settings.SessionsDirectory;
            _txtLogsDir.Text = _settings.LogsDirectory;

            // Camera
            _cbCameraIndex.SelectedItem = _settings.DefaultCameraIndex.ToString();
            if (_cbCameraIndex.SelectedIndex < 0) _cbCameraIndex.SelectedIndex = 0;

            var resIdx = _cbResolution.Items.IndexOf(_settings.PreferredResolution);
            _cbResolution.SelectedIndex = resIdx >= 0 ? resIdx : 3;

            // Analysis
            int maskVal = (int)(_settings.MaskSensitivity * 100);
            _trackMaskSensitivity.Value = Math.Max(0, Math.Min(100, maskVal));
            _lblMaskValue.Text = $"{_settings.MaskSensitivity:F2}";

            _nudFocusThreshold.Value = (decimal)Math.Max(0, Math.Min(1, _settings.FocusThreshold));
            _nudClippingThreshold.Value = (decimal)Math.Max(0, Math.Min(0.5, _settings.ClippingThreshold));
            _chkStrongWarnings.Checked = _settings.StrongWarningsEnabled;

            // Updates
            _chkAutoUpdate.Checked = _settings.AutoUpdateCheckEnabled;
            UpdateLastCheckDisplay();

            // Profile
            _txtLabName.Text = _settings.LabName;
            _txtLogoPath.Text = _settings.LogoPath;
            _txtOperator.Text = _settings.DefaultOperator;
            _txtWhatsAppContact.Text = _settings.WhatsAppContact;

            UpdateCurrentPathsDisplay();
        }

        private void UpdateLastCheckDisplay()
        {
            if (_settings.LastUpdateCheck.HasValue)
            {
                _lblLastCheck.Text = $"Última verificação: {_settings.LastUpdateCheck.Value:dd/MM/yyyy HH:mm}";
            }
            else
            {
                _lblLastCheck.Text = "Última verificação: nunca";
            }

            _lblLastResult.Text = _settings.LastUpdateResult switch
            {
                "NotChecked" => "Resultado: não verificado",
                "NoUpdate" => "Resultado: você está na versão mais recente",
                "UpdateAvailable" => $"Resultado: nova versão disponível ({_settings.LatestVersionFound ?? "?"})",
                "Error" => "Resultado: erro ao verificar",
                _ => $"Resultado: {_settings.LastUpdateResult}"
            };
        }

        private void UpdateCurrentPathsDisplay()
        {
            _lblCurrentPaths.Text =
                $"Imagens: {_txtImagesDir.Text}\n" +
                $"Laudos: {_txtReportsDir.Text}";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // General
            _settings.DefaultImagesDirectory = _txtImagesDir.Text;
            _settings.ReportsDirectory = _txtReportsDir.Text;
            _settings.SessionsDirectory = _txtSessionsDir.Text;
            _settings.LogsDirectory = _txtLogsDir.Text;

            // Camera
            if (int.TryParse(_cbCameraIndex.SelectedItem?.ToString(), out int camIdx))
                _settings.DefaultCameraIndex = camIdx;
            _settings.PreferredResolution = _cbResolution.SelectedItem?.ToString() ?? "1920x1080";

            // Analysis
            _settings.MaskSensitivity = _trackMaskSensitivity.Value / 100.0;
            _settings.FocusThreshold = (double)_nudFocusThreshold.Value;
            _settings.ClippingThreshold = (double)_nudClippingThreshold.Value;
            _settings.StrongWarningsEnabled = _chkStrongWarnings.Checked;

            // Updates
            _settings.AutoUpdateCheckEnabled = _chkAutoUpdate.Checked;

            // Profile
            _settings.LabName = _txtLabName.Text;
            _settings.LogoPath = _txtLogoPath.Text;
            _settings.DefaultOperator = _txtOperator.Text;
            _settings.WhatsAppContact = _txtWhatsAppContact.Text;

            _settings.Save();

            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnRestoreDefaults_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                this,
                "Deseja restaurar todas as configurações para os valores padrão?\n\nEsta ação não pode ser desfeita.",
                "Restaurar padrão",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _settings.RestoreDefaults();
                LoadSettingsToUI();
            }
        }

        private async void BtnCheckNow_Click(object? sender, EventArgs e)
        {
            _btnCheckNow.Enabled = false;
            _btnCheckNow.Text = "Verificando...";

            try
            {
                var updateService = new UpdateService();
                var (hasUpdate, latestVersion, errorMessage) = await updateService.CheckForUpdatesAsync();

                _settings.LastUpdateCheck = DateTime.Now;

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    _settings.LastUpdateResult = "Error";
                    MessageBox.Show(
                        this,
                        $"Erro ao verificar atualizações:\n\n{errorMessage}",
                        "Verificação de Atualizações",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else if (hasUpdate)
                {
                    _settings.LastUpdateResult = "UpdateAvailable";
                    _settings.LatestVersionFound = latestVersion;

                    var openPage = MessageBox.Show(
                        this,
                        $"Nova versão disponível: {latestVersion}\n\nDeseja abrir a página de download?",
                        "Atualização Disponível",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (openPage == DialogResult.Yes)
                    {
                        updateService.OpenReleasesPage();
                    }
                }
                else
                {
                    _settings.LastUpdateResult = "NoUpdate";
                    MessageBox.Show(
                        this,
                        "Você está usando a versão mais recente.",
                        "Verificação de Atualizações",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                UpdateLastCheckDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Erro ao verificar atualizações:\n\n{ex.Message}",
                    "Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _btnCheckNow.Enabled = true;
                _btnCheckNow.Text = "🔍 Verificar agora";
            }
        }
    }
}
