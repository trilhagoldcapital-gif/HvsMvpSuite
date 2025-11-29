using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR15: Pre-operation checklist service for validating system state before critical operations.
    /// </summary>
    public class PreOperationChecklistService
    {
        private readonly AppSettings _settings;
        private readonly HvsConfig? _config;
        
        public PreOperationChecklistService(AppSettings settings, HvsConfig? config)
        {
            _settings = settings;
            _config = config;
        }
        
        /// <summary>
        /// Performs a comprehensive system check before starting Live mode.
        /// </summary>
        public ChecklistResult CheckBeforeLive(int cameraIndex, int cameraWidth, int cameraHeight)
        {
            var result = new ChecklistResult();
            
            // Check 1: Camera configuration
            var cameraCheck = new ChecklistItem
            {
                Id = "camera_config",
                Name = "Câmera configurada",
                Description = $"Câmera {cameraIndex} · {cameraWidth}x{cameraHeight}",
                IsOk = cameraIndex >= 0 && cameraWidth > 0 && cameraHeight > 0,
                IsRequired = true
            };
            if (!cameraCheck.IsOk)
            {
                cameraCheck.ErrorMessage = "Câmera não configurada corretamente. Verifique índice e resolução.";
            }
            result.Items.Add(cameraCheck);
            
            // Check 2: Configuration JSON loaded
            var configCheck = new ChecklistItem
            {
                Id = "config_loaded",
                Name = "JSON de metais/gemas",
                Description = "Configuração de materiais carregada",
                IsOk = _config?.Materials != null,
                IsRequired = true
            };
            if (!configCheck.IsOk)
            {
                configCheck.ErrorMessage = "JSON de metais/gemas não carregado. Verifique o arquivo hvs-config.json.";
            }
            result.Items.Add(configCheck);
            
            // Check 3: Output directory configured
            var outputCheck = new ChecklistItem
            {
                Id = "output_dir",
                Name = "Pasta de saída",
                Description = !string.IsNullOrWhiteSpace(_settings.ReportsDirectory) 
                    ? _settings.ReportsDirectory 
                    : "(padrão)",
                IsOk = true, // Always OK - has default
                IsRequired = false
            };
            string outputDir = !string.IsNullOrWhiteSpace(_settings.ReportsDirectory)
                ? _settings.ReportsDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
            try
            {
                Directory.CreateDirectory(outputDir);
                outputCheck.IsOk = Directory.Exists(outputDir);
            }
            catch
            {
                outputCheck.IsOk = false;
                outputCheck.ErrorMessage = "Não foi possível criar/acessar a pasta de saída.";
            }
            result.Items.Add(outputCheck);
            
            // Check 4: Materials count
            int metalCount = _config?.Materials?.Metais?.Count ?? 0;
            int crystalCount = _config?.Materials?.Cristais?.Count ?? 0;
            int gemCount = _config?.Materials?.Gemas?.Count ?? 0;
            int totalMaterials = metalCount + crystalCount + gemCount;
            
            var materialsCheck = new ChecklistItem
            {
                Id = "materials_count",
                Name = "Materiais carregados",
                Description = $"{metalCount} metais · {crystalCount} cristais · {gemCount} gemas",
                IsOk = totalMaterials > 0,
                IsRequired = true
            };
            if (!materialsCheck.IsOk)
            {
                materialsCheck.ErrorMessage = "Nenhum material carregado. Verifique hvs-config.json.";
            }
            result.Items.Add(materialsCheck);
            
            // Check 5: Logs directory
            var logsCheck = new ChecklistItem
            {
                Id = "logs_dir",
                Name = "Logs estruturados",
                Description = "Diretório de logs acessível",
                IsOk = true,
                IsRequired = false
            };
            string logsDir = !string.IsNullOrWhiteSpace(_settings.LogsDirectory)
                ? _settings.LogsDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            try
            {
                Directory.CreateDirectory(logsDir);
                logsCheck.IsOk = Directory.Exists(logsDir);
            }
            catch
            {
                logsCheck.IsOk = false;
                logsCheck.ErrorMessage = "Não foi possível criar/acessar o diretório de logs.";
            }
            result.Items.Add(logsCheck);
            
            result.CalculateOverallStatus();
            return result;
        }
        
        /// <summary>
        /// Performs a comprehensive system check before starting analysis.
        /// </summary>
        public ChecklistResult CheckBeforeAnalysis(bool hasImage, string? imagePath)
        {
            var result = new ChecklistResult();
            
            // Check 1: Image loaded
            var imageCheck = new ChecklistItem
            {
                Id = "image_loaded",
                Name = "Imagem carregada",
                Description = hasImage 
                    ? (imagePath != null ? Path.GetFileName(imagePath) : "Frame de câmera")
                    : "Nenhuma imagem",
                IsOk = hasImage,
                IsRequired = true
            };
            if (!imageCheck.IsOk)
            {
                imageCheck.ErrorMessage = "Nenhuma imagem carregada. Abra uma imagem ou inicie o modo Live.";
            }
            result.Items.Add(imageCheck);
            
            // Check 2: Configuration JSON loaded
            var configCheck = new ChecklistItem
            {
                Id = "config_loaded",
                Name = "JSON de metais/gemas",
                Description = "Configuração de materiais carregada",
                IsOk = _config?.Materials != null,
                IsRequired = true
            };
            if (!configCheck.IsOk)
            {
                configCheck.ErrorMessage = "JSON de metais/gemas não carregado. Verifique o arquivo hvs-config.json.";
            }
            result.Items.Add(configCheck);
            
            // Check 3: Output directory configured
            var outputCheck = new ChecklistItem
            {
                Id = "output_dir",
                Name = "Pasta de saída",
                Description = !string.IsNullOrWhiteSpace(_settings.ReportsDirectory) 
                    ? _settings.ReportsDirectory 
                    : "(padrão)",
                IsOk = true,
                IsRequired = false
            };
            result.Items.Add(outputCheck);
            
            // Check 4: Materials count
            int metalCount = _config?.Materials?.Metais?.Count ?? 0;
            var materialsCheck = new ChecklistItem
            {
                Id = "materials_ready",
                Name = "Módulos de análise",
                Description = $"HVS + {metalCount} materiais configurados",
                IsOk = metalCount > 0,
                IsRequired = true
            };
            if (!materialsCheck.IsOk)
            {
                materialsCheck.ErrorMessage = "Nenhum material configurado para análise.";
            }
            result.Items.Add(materialsCheck);
            
            result.CalculateOverallStatus();
            return result;
        }
        
        /// <summary>
        /// PR16: Performs additional checks for ROI/image settings before analysis.
        /// </summary>
        public ChecklistResult CheckWithPr16Features(bool hasImage, string? imagePath, bool hasRoi, bool uvModeActive, double zoomLevel)
        {
            // Start with basic analysis checks
            var result = CheckBeforeAnalysis(hasImage, imagePath);
            
            // PR16 Check: ROI defined
            var roiCheck = new ChecklistItem
            {
                Id = "roi_defined",
                Name = "ROI definida",
                Description = hasRoi ? "Área de amostra delimitada" : "Usando imagem completa",
                IsOk = true, // ROI is optional
                IsRequired = false
            };
            if (!hasRoi)
            {
                roiCheck.Description = "⚠ Sem ROI: analisando imagem completa (fundo pode ser incluído)";
            }
            result.Items.Add(roiCheck);
            
            // PR16 Check: UV mode
            var uvCheck = new ChecklistItem
            {
                Id = "uv_mode",
                Name = "Modo UV",
                Description = uvModeActive ? "UV ativo (ajustes aplicados)" : "Luz visível (normal)",
                IsOk = true, // UV is informational only
                IsRequired = false
            };
            result.Items.Add(uvCheck);
            
            // PR16 Check: Zoom level
            const double RecommendedZoomMin = 0.5;
            const double RecommendedZoomMax = 4.0;
            
            var zoomCheck = new ChecklistItem
            {
                Id = "zoom_level",
                Name = "Nível de zoom",
                Description = $"Zoom: {(int)(zoomLevel * 100)}%",
                IsOk = true, // Zoom is informational only
                IsRequired = false
            };
            if (zoomLevel < RecommendedZoomMin || zoomLevel > RecommendedZoomMax)
            {
                zoomCheck.Description += $" (fora do intervalo recomendado {(int)(RecommendedZoomMin * 100)}%-{(int)(RecommendedZoomMax * 100)}%)";
            }
            result.Items.Add(zoomCheck);
            
            result.CalculateOverallStatus();
            return result;
        }
        
        /// <summary>
        /// Shows the checklist dialog and returns whether the user wants to proceed.
        /// </summary>
        public bool ShowChecklistDialog(IWin32Window owner, ChecklistResult result, string operationName)
        {
            using var dlg = new ChecklistDialog(result, operationName);
            return dlg.ShowDialog(owner) == DialogResult.OK;
        }
    }
    
    /// <summary>
    /// PR15: Result of a pre-operation checklist.
    /// </summary>
    public class ChecklistResult
    {
        public List<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
        public bool AllRequiredOk { get; private set; }
        public bool AllOk { get; private set; }
        public int OkCount { get; private set; }
        public int WarningCount { get; private set; }
        public int ErrorCount { get; private set; }
        
        public void CalculateOverallStatus()
        {
            OkCount = 0;
            WarningCount = 0;
            ErrorCount = 0;
            AllRequiredOk = true;
            AllOk = true;
            
            foreach (var item in Items)
            {
                if (item.IsOk)
                {
                    OkCount++;
                }
                else
                {
                    if (item.IsRequired)
                    {
                        ErrorCount++;
                        AllRequiredOk = false;
                    }
                    else
                    {
                        WarningCount++;
                    }
                    AllOk = false;
                }
            }
        }
    }
    
    /// <summary>
    /// PR15: Single item in a pre-operation checklist.
    /// </summary>
    public class ChecklistItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsOk { get; set; }
        public bool IsRequired { get; set; }
        public string? ErrorMessage { get; set; }
        
        public string GetStatusIcon()
        {
            return IsOk ? "✔" : (IsRequired ? "❌" : "⚠");
        }
        
        public Color GetStatusColor()
        {
            return IsOk ? Color.FromArgb(100, 200, 100) 
                : (IsRequired ? Color.FromArgb(220, 100, 100) : Color.FromArgb(220, 180, 80));
        }
    }
    
    /// <summary>
    /// PR15: Dialog to display pre-operation checklist.
    /// </summary>
    public class ChecklistDialog : Form
    {
        private readonly ChecklistResult _result;
        private readonly string _operationName;
        
        public ChecklistDialog(ChecklistResult result, string operationName)
        {
            _result = result;
            _operationName = operationName;
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            Text = $"Checklist - {_operationName}";
            Size = new Size(450, 350);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(25, 30, 40);
            ForeColor = Color.WhiteSmoke;
            
            // Title
            var lblTitle = new Label
            {
                Text = $"Verificação antes de: {_operationName}",
                Location = new Point(15, 15),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 160, 60)
            };
            Controls.Add(lblTitle);
            
            // Checklist panel
            var checklistPanel = new Panel
            {
                Location = new Point(15, 50),
                Size = new Size(400, 180),
                AutoScroll = true,
                BackColor = Color.FromArgb(18, 22, 32)
            };
            Controls.Add(checklistPanel);
            
            int y = 10;
            foreach (var item in _result.Items)
            {
                var lblIcon = new Label
                {
                    Text = item.GetStatusIcon(),
                    Location = new Point(10, y),
                    Size = new Size(25, 22),
                    Font = new Font("Segoe UI", 12),
                    ForeColor = item.GetStatusColor()
                };
                checklistPanel.Controls.Add(lblIcon);
                
                var lblName = new Label
                {
                    Text = item.Name,
                    Location = new Point(40, y),
                    Size = new Size(150, 22),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.WhiteSmoke
                };
                checklistPanel.Controls.Add(lblName);
                
                var lblDesc = new Label
                {
                    Text = item.Description,
                    Location = new Point(200, y),
                    Size = new Size(180, 22),
                    Font = new Font("Segoe UI", 8),
                    ForeColor = Color.FromArgb(160, 170, 190),
                    AutoEllipsis = true
                };
                checklistPanel.Controls.Add(lblDesc);
                
                if (!item.IsOk && !string.IsNullOrEmpty(item.ErrorMessage))
                {
                    y += 22;
                    var lblError = new Label
                    {
                        Text = item.ErrorMessage,
                        Location = new Point(40, y),
                        Size = new Size(340, 20),
                        Font = new Font("Segoe UI", 8, FontStyle.Italic),
                        ForeColor = item.GetStatusColor()
                    };
                    checklistPanel.Controls.Add(lblError);
                }
                
                y += 28;
            }
            
            // Summary
            string summary;
            Color summaryColor;
            if (_result.AllOk)
            {
                summary = "✔ Todos os itens verificados. Pronto para iniciar.";
                summaryColor = Color.FromArgb(100, 200, 100);
            }
            else if (_result.AllRequiredOk)
            {
                summary = $"⚠ {_result.WarningCount} aviso(s), mas os itens essenciais estão OK.";
                summaryColor = Color.FromArgb(220, 180, 80);
            }
            else
            {
                summary = $"❌ {_result.ErrorCount} erro(s) crítico(s) encontrado(s).";
                summaryColor = Color.FromArgb(220, 100, 100);
            }
            
            var lblSummary = new Label
            {
                Text = summary,
                Location = new Point(15, 240),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 10),
                ForeColor = summaryColor
            };
            Controls.Add(lblSummary);
            
            // Buttons
            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(230, 275),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(60, 65, 75),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(80, 85, 95);
            Controls.Add(btnCancel);
            
            var btnProceed = new Button
            {
                Text = _result.AllRequiredOk ? "Iniciar" : "Forçar início",
                Location = new Point(330, 275),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK,
                BackColor = _result.AllRequiredOk 
                    ? Color.FromArgb(40, 100, 60) 
                    : Color.FromArgb(140, 80, 40),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            btnProceed.FlatAppearance.BorderColor = _result.AllRequiredOk 
                ? Color.FromArgb(60, 140, 80) 
                : Color.FromArgb(180, 100, 50);
            Controls.Add(btnProceed);
            
            AcceptButton = btnProceed;
            CancelButton = btnCancel;
        }
    }
}
