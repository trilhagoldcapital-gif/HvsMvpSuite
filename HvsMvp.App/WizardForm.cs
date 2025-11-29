using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR15: Base form for step-by-step guided wizards.
    /// </summary>
    public class WizardForm : Form
    {
        protected readonly AppSettings _settings;
        protected readonly List<WizardStep> _steps = new List<WizardStep>();
        protected int _currentStep = 0;
        
        // UI Elements
        protected Panel _headerPanel = null!;
        protected Label _lblTitle = null!;
        protected Label _lblDescription = null!;
        protected Panel _stepsPanel = null!;
        protected Panel _contentPanel = null!;
        protected Panel _footerPanel = null!;
        protected Button _btnBack = null!;
        protected Button _btnNext = null!;
        protected Button _btnCancel = null!;
        protected Label _lblStepProgress = null!;
        
        // Colors
        protected readonly Color _bgColor = Color.FromArgb(20, 25, 35);
        protected readonly Color _headerColor = Color.FromArgb(200, 160, 60);
        protected readonly Color _textColor = Color.WhiteSmoke;
        protected readonly Color _accentColor = Color.FromArgb(60, 180, 200);
        protected readonly Color _stepActiveColor = Color.FromArgb(100, 200, 100);
        protected readonly Color _stepInactiveColor = Color.FromArgb(80, 90, 110);
        protected readonly Color _stepCompletedColor = Color.FromArgb(60, 140, 80);
        
        public WizardForm(AppSettings settings, string title)
        {
            _settings = settings;
            InitializeBaseLayout(title);
        }
        
        private void InitializeBaseLayout(string title)
        {
            Text = title;
            Size = new Size(700, 550);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = _bgColor;
            ForeColor = _textColor;
            
            // Header panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(15, 20, 30)
            };
            Controls.Add(_headerPanel);
            
            // Gold accent bar
            var accentBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 4,
                BackColor = _headerColor
            };
            _headerPanel.Controls.Add(accentBar);
            
            _lblTitle = new Label
            {
                Text = title,
                Location = new Point(20, 18),
                Size = new Size(550, 28),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = _textColor
            };
            _headerPanel.Controls.Add(_lblTitle);
            
            _lblDescription = new Label
            {
                Location = new Point(20, 48),
                Size = new Size(550, 22),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(160, 170, 190)
            };
            _headerPanel.Controls.Add(_lblDescription);
            
            // Steps panel (left sidebar)
            _stepsPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 180,
                BackColor = Color.FromArgb(12, 16, 24),
                Padding = new Padding(15, 15, 15, 15)
            };
            Controls.Add(_stepsPanel);
            
            // Content panel
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 15, 20, 15),
                BackColor = _bgColor
            };
            Controls.Add(_contentPanel);
            _contentPanel.BringToFront();
            
            // Footer panel
            _footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(15, 20, 30),
                Padding = new Padding(15, 12, 15, 12)
            };
            Controls.Add(_footerPanel);
            
            _lblStepProgress = new Label
            {
                Location = new Point(15, 20),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(140, 150, 170)
            };
            _footerPanel.Controls.Add(_lblStepProgress);
            
            _btnCancel = CreateFooterButton("Cancelar", 440, DialogResult.Cancel);
            _btnCancel.Click += (s, e) => Close();
            
            _btnBack = CreateFooterButton("← Voltar", 520, DialogResult.None);
            _btnBack.Click += BtnBack_Click;
            
            _btnNext = CreateFooterButton("Próximo →", 600, DialogResult.None);
            _btnNext.BackColor = Color.FromArgb(40, 100, 60);
            _btnNext.FlatAppearance.BorderColor = Color.FromArgb(60, 140, 80);
            _btnNext.Click += BtnNext_Click;
            
            _footerPanel.Controls.Add(_btnCancel);
            _footerPanel.Controls.Add(_btnBack);
            _footerPanel.Controls.Add(_btnNext);
            
            CancelButton = _btnCancel;
        }
        
        private Button CreateFooterButton(string text, int right, DialogResult result)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(90, 32),
                Location = new Point(right - 90, 12),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                DialogResult = result,
                BackColor = Color.FromArgb(50, 55, 65),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(70, 75, 85);
            return btn;
        }
        
        /// <summary>
        /// Adds a step to the wizard.
        /// </summary>
        protected void AddStep(WizardStep step)
        {
            _steps.Add(step);
        }
        
        /// <summary>
        /// Initializes the wizard after all steps are added.
        /// </summary>
        protected void InitializeWizard()
        {
            UpdateStepsPanel();
            ShowStep(0);
        }
        
        private void UpdateStepsPanel()
        {
            _stepsPanel.Controls.Clear();
            
            var lblStepsTitle = new Label
            {
                Text = "Passos",
                Location = new Point(15, 15),
                Size = new Size(150, 22),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = _headerColor
            };
            _stepsPanel.Controls.Add(lblStepsTitle);
            
            int y = 50;
            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                bool isCurrent = i == _currentStep;
                bool isCompleted = i < _currentStep;
                
                var stepNumber = new Label
                {
                    Text = isCompleted ? "✓" : $"{i + 1}",
                    Location = new Point(15, y),
                    Size = new Size(25, 25),
                    Font = new Font("Segoe UI", 10, isCompleted ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = isCompleted ? _stepCompletedColor 
                        : (isCurrent ? _stepActiveColor : _stepInactiveColor),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                _stepsPanel.Controls.Add(stepNumber);
                
                var stepLabel = new Label
                {
                    Text = step.Title,
                    Location = new Point(45, y + 2),
                    Size = new Size(120, 20),
                    Font = new Font("Segoe UI", 9, isCurrent ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = isCurrent ? _textColor 
                        : (isCompleted ? Color.FromArgb(140, 170, 140) : Color.FromArgb(110, 120, 140))
                };
                _stepsPanel.Controls.Add(stepLabel);
                
                y += 35;
            }
        }
        
        private void ShowStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= _steps.Count)
                return;
            
            _currentStep = stepIndex;
            var step = _steps[stepIndex];
            
            // Update header
            _lblDescription.Text = step.Description;
            
            // Update step progress
            _lblStepProgress.Text = $"Passo {stepIndex + 1} de {_steps.Count}";
            
            // Update buttons
            _btnBack.Enabled = stepIndex > 0;
            _btnNext.Text = stepIndex == _steps.Count - 1 ? "Concluir" : "Próximo →";
            
            // Update steps panel
            UpdateStepsPanel();
            
            // Load step content
            _contentPanel.Controls.Clear();
            step.BuildContent?.Invoke(_contentPanel);
        }
        
        private void BtnBack_Click(object? sender, EventArgs e)
        {
            if (_currentStep > 0)
            {
                // Call OnLeaving for current step
                _steps[_currentStep].OnLeaving?.Invoke();
                
                ShowStep(_currentStep - 1);
            }
        }
        
        private void BtnNext_Click(object? sender, EventArgs e)
        {
            var currentStepObj = _steps[_currentStep];
            
            // Validate current step
            if (currentStepObj.Validate != null && !currentStepObj.Validate())
            {
                return;
            }
            
            // Execute step action if any
            currentStepObj.OnCompleted?.Invoke();
            
            if (_currentStep < _steps.Count - 1)
            {
                ShowStep(_currentStep + 1);
            }
            else
            {
                // Final step - complete wizard
                OnWizardCompleted();
            }
        }
        
        /// <summary>
        /// Called when the wizard is completed (last step's Next is clicked).
        /// Override in derived classes to perform final actions.
        /// </summary>
        protected virtual void OnWizardCompleted()
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
    
    /// <summary>
    /// PR15: Represents a step in a wizard.
    /// </summary>
    public class WizardStep
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public Action<Panel>? BuildContent { get; set; }
        public Func<bool>? Validate { get; set; }
        public Action? OnCompleted { get; set; }
        public Action? OnLeaving { get; set; }
    }
}
