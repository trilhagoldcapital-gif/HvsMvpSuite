using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR16: Control that provides real zoom and pan functionality for images.
    /// Supports mouse wheel zoom, click-drag pan, and zoom buttons.
    /// </summary>
    public class ZoomPanControl : Panel
    {
        // Image and transform state
        private Bitmap? _image;
        private float _zoomLevel = 1.0f;
        private PointF _panOffset = PointF.Empty;
        private bool _isPanning;
        private Point _panStartMouse;
        private PointF _panStartOffset;

        // Zoom limits
        private const float MinZoom = 0.1f;
        private const float MaxZoom = 20.0f;
        private const float ZoomStep = 0.15f;

        // Events
        public event EventHandler? ZoomChanged;
        public event EventHandler? PanChanged;
        public event EventHandler<Point>? PixelClicked;

        // Colors
        private readonly Color _bgColor = Color.FromArgb(15, 25, 40);
        private readonly Color _gridColor = Color.FromArgb(25, 35, 55);

        public ZoomPanControl()
        {
            DoubleBuffered = true;
            BackColor = _bgColor;

            // Enable mouse events
            MouseWheel += OnMouseWheel;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseClick += OnMouseClick;
            Resize += (s, e) => Invalidate();
        }

        /// <summary>Current zoom level (1.0 = 100%).</summary>
        public float ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                var newZoom = Math.Clamp(value, MinZoom, MaxZoom);
                if (Math.Abs(newZoom - _zoomLevel) > 0.001f)
                {
                    _zoomLevel = newZoom;
                    ClampPanOffset();
                    Invalidate();
                    ZoomChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>Current pan offset in image coordinates.</summary>
        public PointF PanOffset => _panOffset;

        /// <summary>Current image being displayed.</summary>
        public Bitmap? Image
        {
            get => _image;
            set
            {
                _image = value;
                ResetView();
            }
        }

        /// <summary>Get zoom percentage string.</summary>
        public string ZoomPercentageText => $"{(int)(_zoomLevel * 100)}%";

        /// <summary>
        /// Set a new image without resetting view (for frame updates).
        /// </summary>
        public void UpdateImage(Bitmap newImage)
        {
            _image = newImage;
            Invalidate();
        }

        /// <summary>
        /// Reset view to fit image in control.
        /// </summary>
        public void ResetView()
        {
            _panOffset = PointF.Empty;

            if (_image != null && Width > 0 && Height > 0)
            {
                // Calculate zoom to fit
                float scaleX = (float)Width / _image.Width;
                float scaleY = (float)Height / _image.Height;
                _zoomLevel = Math.Min(scaleX, scaleY);
                _zoomLevel = Math.Clamp(_zoomLevel, MinZoom, MaxZoom);
            }
            else
            {
                _zoomLevel = 1.0f;
            }

            Invalidate();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Zoom to 100% (actual pixels).
        /// </summary>
        public void ZoomToActualSize()
        {
            ZoomLevel = 1.0f;
        }

        /// <summary>
        /// Zoom to fit image in control.
        /// </summary>
        public void ZoomToFit()
        {
            ResetView();
        }

        /// <summary>
        /// Zoom in by one step.
        /// </summary>
        public void ZoomIn()
        {
            ZoomLevel *= (1 + ZoomStep);
        }

        /// <summary>
        /// Zoom out by one step.
        /// </summary>
        public void ZoomOut()
        {
            ZoomLevel *= (1 - ZoomStep);
        }

        /// <summary>
        /// Center the view on a specific image coordinate.
        /// </summary>
        public void CenterOn(int imageX, int imageY)
        {
            if (_image == null) return;

            _panOffset = new PointF(
                Width / 2f - imageX * _zoomLevel,
                Height / 2f - imageY * _zoomLevel
            );
            ClampPanOffset();
            Invalidate();
            PanChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Convert screen coordinates to image coordinates.
        /// </summary>
        public Point ScreenToImage(Point screenPoint)
        {
            if (_image == null) return Point.Empty;

            int imageX = (int)((screenPoint.X - _panOffset.X) / _zoomLevel);
            int imageY = (int)((screenPoint.Y - _panOffset.Y) / _zoomLevel);

            return new Point(
                Math.Clamp(imageX, 0, _image.Width - 1),
                Math.Clamp(imageY, 0, _image.Height - 1)
            );
        }

        /// <summary>
        /// Convert image coordinates to screen coordinates.
        /// </summary>
        public Point ImageToScreen(Point imagePoint)
        {
            return new Point(
                (int)(imagePoint.X * _zoomLevel + _panOffset.X),
                (int)(imagePoint.Y * _zoomLevel + _panOffset.Y)
            );
        }

        private void OnMouseWheel(object? sender, MouseEventArgs e)
        {
            if (_image == null) return;

            // Get mouse position relative to image before zoom
            var mouseImagePos = ScreenToImage(e.Location);

            // Apply zoom
            float zoomDelta = e.Delta > 0 ? (1 + ZoomStep) : (1 - ZoomStep);
            float newZoom = Math.Clamp(_zoomLevel * zoomDelta, MinZoom, MaxZoom);

            if (Math.Abs(newZoom - _zoomLevel) > 0.001f)
            {
                // Zoom towards mouse position
                _zoomLevel = newZoom;

                // Adjust pan to keep mouse over same image point
                _panOffset = new PointF(
                    e.X - mouseImagePos.X * _zoomLevel,
                    e.Y - mouseImagePos.Y * _zoomLevel
                );

                ClampPanOffset();
                Invalidate();
                ZoomChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
            {
                _isPanning = true;
                _panStartMouse = e.Location;
                _panStartOffset = _panOffset;
                Cursor = Cursors.SizeAll;
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (_isPanning && _image != null)
            {
                int dx = e.X - _panStartMouse.X;
                int dy = e.Y - _panStartMouse.Y;

                _panOffset = new PointF(
                    _panStartOffset.X + dx,
                    _panStartOffset.Y + dy
                );

                ClampPanOffset();
                Invalidate();
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                Cursor = Cursors.Default;
                PanChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _image != null)
            {
                var imagePos = ScreenToImage(e.Location);
                PixelClicked?.Invoke(this, imagePos);
            }
        }

        private void ClampPanOffset()
        {
            if (_image == null) return;

            float scaledWidth = _image.Width * _zoomLevel;
            float scaledHeight = _image.Height * _zoomLevel;

            // Allow some margin for panning beyond image edges
            float marginX = Width * 0.8f;
            float marginY = Height * 0.8f;

            _panOffset = new PointF(
                Math.Clamp(_panOffset.X, -scaledWidth + marginX, Width - marginX),
                Math.Clamp(_panOffset.Y, -scaledHeight + marginY, Height - marginY)
            );
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;

            // Draw background grid
            DrawBackgroundGrid(g);

            if (_image == null) return;

            // Set interpolation mode based on zoom
            g.InterpolationMode = _zoomLevel < 1.0f
                ? InterpolationMode.HighQualityBicubic
                : InterpolationMode.NearestNeighbor;

            g.PixelOffsetMode = PixelOffsetMode.Half;

            // Calculate destination rectangle
            float scaledWidth = _image.Width * _zoomLevel;
            float scaledHeight = _image.Height * _zoomLevel;

            var destRect = new RectangleF(
                _panOffset.X,
                _panOffset.Y,
                scaledWidth,
                scaledHeight
            );

            // Draw image
            g.DrawImage(_image, destRect);

            // Draw image border
            using var borderPen = new Pen(Color.FromArgb(80, 100, 120), 1);
            g.DrawRectangle(borderPen, destRect.X, destRect.Y, destRect.Width, destRect.Height);

            // Draw zoom indicator
            DrawZoomIndicator(g);
        }

        private void DrawBackgroundGrid(Graphics g)
        {
            using var gridPen = new Pen(_gridColor, 1);
            int gridSize = 20;

            for (int x = 0; x < Width; x += gridSize)
            {
                g.DrawLine(gridPen, x, 0, x, Height);
            }
            for (int y = 0; y < Height; y += gridSize)
            {
                g.DrawLine(gridPen, 0, y, Width, y);
            }
        }

        private void DrawZoomIndicator(Graphics g)
        {
            string zoomText = ZoomPercentageText;
            using var font = new Font("Segoe UI", 9, FontStyle.Bold);
            var textSize = g.MeasureString(zoomText, font);

            int padding = 4;
            int x = Width - (int)textSize.Width - padding * 2 - 8;
            int y = Height - (int)textSize.Height - padding * 2 - 8;

            // Background
            using var bgBrush = new SolidBrush(Color.FromArgb(180, 20, 30, 45));
            g.FillRectangle(bgBrush, x, y, textSize.Width + padding * 2, textSize.Height + padding * 2);

            // Border
            using var borderPen = new Pen(Color.FromArgb(100, 60, 80, 110), 1);
            g.DrawRectangle(borderPen, x, y, textSize.Width + padding * 2, textSize.Height + padding * 2);

            // Text
            using var textBrush = new SolidBrush(Color.FromArgb(220, 230, 245));
            g.DrawString(zoomText, font, textBrush, x + padding, y + padding);
        }
    }

    /// <summary>
    /// PR16: Toolbar panel with zoom controls (+, -, fit, 100%).
    /// </summary>
    public class ZoomToolbar : Panel
    {
        private readonly ZoomPanControl _zoomControl;
        private Label _lblZoom = null!;

        public ZoomToolbar(ZoomPanControl zoomControl)
        {
            _zoomControl = zoomControl;
            _zoomControl.ZoomChanged += (s, e) => UpdateZoomLabel();

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            Height = 32;
            BackColor = Color.FromArgb(18, 28, 45);
            Padding = new Padding(4);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false
            };

            // Zoom out button
            var btnZoomOut = CreateToolButton("−", "Zoom Out (diminuir)");
            btnZoomOut.Click += (s, e) => _zoomControl.ZoomOut();
            flow.Controls.Add(btnZoomOut);

            // Zoom label
            _lblZoom = new Label
            {
                Text = "100%",
                AutoSize = false,
                Width = 55,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(200, 210, 225),
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(2, 2, 2, 0)
            };
            flow.Controls.Add(_lblZoom);

            // Zoom in button
            var btnZoomIn = CreateToolButton("+", "Zoom In (ampliar)");
            btnZoomIn.Click += (s, e) => _zoomControl.ZoomIn();
            flow.Controls.Add(btnZoomIn);

            // Separator
            flow.Controls.Add(CreateSeparator());

            // Fit button
            var btnFit = CreateToolButton("⊡", "Ajustar à janela");
            btnFit.Click += (s, e) => _zoomControl.ZoomToFit();
            flow.Controls.Add(btnFit);

            // 100% button
            var btn100 = CreateToolButton("1:1", "Tamanho real (100%)");
            btn100.Width = 36;
            btn100.Click += (s, e) => _zoomControl.ZoomToActualSize();
            flow.Controls.Add(btn100);

            Controls.Add(flow);
        }

        private Button CreateToolButton(string text, string tooltip)
        {
            var btn = new Button
            {
                Text = text,
                Width = 28,
                Height = 24,
                Margin = new Padding(2, 2, 2, 0),
                BackColor = Color.FromArgb(30, 45, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(50, 70, 95);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 65, 90);

            var tt = new ToolTip();
            tt.SetToolTip(btn, tooltip);

            return btn;
        }

        private Panel CreateSeparator()
        {
            return new Panel
            {
                Width = 1,
                Height = 20,
                BackColor = Color.FromArgb(50, 65, 85),
                Margin = new Padding(6, 4, 6, 0)
            };
        }

        private void UpdateZoomLabel()
        {
            _lblZoom.Text = _zoomControl.ZoomPercentageText;
        }
    }
}
