using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HvsMvp.App
{
    /// <summary>
    /// PR16: Type of ROI shape.
    /// </summary>
    public enum RoiShape
    {
        /// <summary>No ROI defined - use full image.</summary>
        None,
        /// <summary>Rectangular ROI.</summary>
        Rectangle,
        /// <summary>Elliptical ROI.</summary>
        Ellipse,
        /// <summary>Free-form polygon ROI.</summary>
        Polygon
    }

    /// <summary>
    /// PR16: Region of Interest definition for sample/background separation.
    /// </summary>
    public class RoiDefinition
    {
        /// <summary>ROI shape type.</summary>
        public RoiShape Shape { get; set; } = RoiShape.None;

        /// <summary>For Rectangle/Ellipse: bounding rectangle.</summary>
        public Rectangle Bounds { get; set; }

        /// <summary>For Polygon: list of vertices.</summary>
        public List<Point> PolygonPoints { get; set; } = new List<Point>();

        /// <summary>Whether to invert the ROI (exclude interior, include exterior).</summary>
        public bool Inverted { get; set; }

        /// <summary>
        /// Check if a point is inside the ROI.
        /// </summary>
        public bool Contains(int x, int y)
        {
            bool inside = false;

            switch (Shape)
            {
                case RoiShape.None:
                    inside = true;
                    break;

                case RoiShape.Rectangle:
                    inside = Bounds.Contains(x, y);
                    break;

                case RoiShape.Ellipse:
                    inside = IsInEllipse(x, y, Bounds);
                    break;

                case RoiShape.Polygon:
                    inside = IsInPolygon(x, y, PolygonPoints);
                    break;
            }

            return Inverted ? !inside : inside;
        }

        private static bool IsInEllipse(int x, int y, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return false;

            double cx = bounds.X + bounds.Width / 2.0;
            double cy = bounds.Y + bounds.Height / 2.0;
            double rx = bounds.Width / 2.0;
            double ry = bounds.Height / 2.0;

            double dx = (x - cx) / rx;
            double dy = (y - cy) / ry;

            return (dx * dx + dy * dy) <= 1.0;
        }

        private static bool IsInPolygon(int x, int y, List<Point> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;

            // Ray casting algorithm
            bool inside = false;
            int n = polygon.Count;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((polygon[i].Y > y) != (polygon[j].Y > y)) &&
                    (x < (polygon[j].X - polygon[i].X) * (y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Create a GraphicsPath for the ROI (for drawing).
        /// </summary>
        public GraphicsPath? ToPath()
        {
            var path = new GraphicsPath();

            switch (Shape)
            {
                case RoiShape.Rectangle:
                    if (Bounds.Width > 0 && Bounds.Height > 0)
                        path.AddRectangle(Bounds);
                    break;

                case RoiShape.Ellipse:
                    if (Bounds.Width > 0 && Bounds.Height > 0)
                        path.AddEllipse(Bounds);
                    break;

                case RoiShape.Polygon:
                    if (PolygonPoints.Count >= 3)
                        path.AddPolygon(PolygonPoints.ToArray());
                    break;

                default:
                    return null;
            }

            return path;
        }
    }

    /// <summary>
    /// PR16: Service for ROI-based sample/background separation.
    /// </summary>
    public class RoiService
    {
        private RoiDefinition? _currentRoi;
        private Bitmap? _backgroundReference;
        private double[,]? _backgroundGray;

        /// <summary>Current ROI definition.</summary>
        public RoiDefinition? CurrentRoi => _currentRoi;

        /// <summary>Whether a valid ROI is defined.</summary>
        public bool HasRoi => _currentRoi != null && _currentRoi.Shape != RoiShape.None;

        /// <summary>Whether a background reference is captured.</summary>
        public bool HasBackgroundReference => _backgroundReference != null;

        /// <summary>
        /// Set a rectangular ROI.
        /// </summary>
        public void SetRectangleRoi(Rectangle bounds)
        {
            _currentRoi = new RoiDefinition
            {
                Shape = RoiShape.Rectangle,
                Bounds = bounds
            };
        }

        /// <summary>
        /// Set an elliptical ROI.
        /// </summary>
        public void SetEllipseRoi(Rectangle bounds)
        {
            _currentRoi = new RoiDefinition
            {
                Shape = RoiShape.Ellipse,
                Bounds = bounds
            };
        }

        /// <summary>
        /// Set a polygon ROI.
        /// </summary>
        public void SetPolygonRoi(List<Point> points)
        {
            _currentRoi = new RoiDefinition
            {
                Shape = RoiShape.Polygon,
                PolygonPoints = new List<Point>(points)
            };
        }

        /// <summary>
        /// Clear the current ROI (use full image).
        /// </summary>
        public void ClearRoi()
        {
            _currentRoi = null;
        }

        /// <summary>
        /// Invert the ROI (exclude sample area, include background).
        /// </summary>
        public void InvertRoi()
        {
            if (_currentRoi != null)
            {
                _currentRoi.Inverted = !_currentRoi.Inverted;
            }
        }

        /// <summary>
        /// Capture a background reference image for subtraction.
        /// </summary>
        public void CaptureBackgroundReference(Bitmap backgroundImage)
        {
            _backgroundReference?.Dispose();
            _backgroundReference = (Bitmap)backgroundImage.Clone();

            // Pre-compute grayscale values for fast subtraction
            int w = backgroundImage.Width;
            int h = backgroundImage.Height;
            _backgroundGray = new double[w, h];

            var rect = new Rectangle(0, 0, w, h);
            var data = backgroundImage.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, 
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            try
            {
                int stride = data.Stride;
                byte[] buf = new byte[stride * h];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;
                        _backgroundGray[x, y] = 0.299 * buf[off + 2] + 0.587 * buf[off + 1] + 0.114 * buf[off + 0];
                    }
                }
            }
            finally
            {
                backgroundImage.UnlockBits(data);
            }
        }

        /// <summary>
        /// Clear the background reference.
        /// </summary>
        public void ClearBackgroundReference()
        {
            _backgroundReference?.Dispose();
            _backgroundReference = null;
            _backgroundGray = null;
        }

        /// <summary>
        /// Check if a pixel is within the ROI (sample area).
        /// </summary>
        public bool IsInSampleArea(int x, int y)
        {
            if (_currentRoi == null || _currentRoi.Shape == RoiShape.None)
                return true; // No ROI = full image is sample

            return _currentRoi.Contains(x, y);
        }

        /// <summary>
        /// Apply ROI mask to an existing sample mask.
        /// Pixels outside ROI are marked as background.
        /// </summary>
        public void ApplyRoiToMask(SampleMaskClass?[,] mask, int width, int height)
        {
            if (_currentRoi == null || _currentRoi.Shape == RoiShape.None)
                return;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!_currentRoi.Contains(x, y))
                    {
                        if (mask[x, y] != null)
                        {
                            mask[x, y]!.IsSample = false;
                            mask[x, y]!.IsBackground = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if a pixel should be considered background based on background subtraction.
        /// Returns true if the pixel is similar to the background reference.
        /// </summary>
        public bool IsBackgroundBySubtraction(int x, int y, byte r, byte g, byte b, double threshold = 30)
        {
            if (_backgroundGray == null) return false;
            if (x < 0 || y < 0 || x >= _backgroundGray.GetLength(0) || y >= _backgroundGray.GetLength(1))
                return false;

            double gray = 0.299 * r + 0.587 * g + 0.114 * b;
            double diff = Math.Abs(gray - _backgroundGray[x, y]);

            return diff < threshold;
        }

        /// <summary>
        /// Draw the ROI overlay on an image.
        /// </summary>
        public Bitmap DrawRoiOverlay(Bitmap source, Color roiColor, float borderWidth = 2f, int fillAlpha = 40)
        {
            var result = (Bitmap)source.Clone();

            if (_currentRoi == null || _currentRoi.Shape == RoiShape.None)
                return result;

            using var g = Graphics.FromImage(result);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var path = _currentRoi.ToPath();
            if (path == null) return result;

            // Fill with semi-transparent color
            using var fillBrush = new SolidBrush(Color.FromArgb(fillAlpha, roiColor));
            if (_currentRoi.Inverted)
            {
                // Fill outside the ROI
                var region = new Region(new Rectangle(0, 0, source.Width, source.Height));
                region.Exclude(path);
                g.FillRegion(fillBrush, region);
            }
            else
            {
                // Fill inside the ROI
                g.FillPath(fillBrush, path);
            }

            // Draw border
            using var borderPen = new Pen(roiColor, borderWidth);
            borderPen.DashStyle = DashStyle.Dash;
            g.DrawPath(borderPen, path);

            path.Dispose();

            return result;
        }

        /// <summary>
        /// Get ROI status description for UI.
        /// </summary>
        public string GetRoiStatusText()
        {
            if (_currentRoi == null || _currentRoi.Shape == RoiShape.None)
                return "ROI: Não definida (imagem completa)";

            string shapeText = _currentRoi.Shape switch
            {
                RoiShape.Rectangle => "Retângulo",
                RoiShape.Ellipse => "Elipse",
                RoiShape.Polygon => $"Polígono ({_currentRoi.PolygonPoints.Count} pts)",
                _ => "?"
            };

            string invertText = _currentRoi.Inverted ? " (invertida)" : "";
            return $"ROI: {shapeText}{invertText}";
        }
    }

    /// <summary>
    /// PR16: Panel for ROI selection with mouse interaction.
    /// </summary>
    public class RoiSelectionOverlay : Panel
    {
        private readonly RoiService _roiService;
        private RoiShape _currentDrawingShape = RoiShape.None;
        private bool _isDrawing;
        private Point _drawStart;
        private Point _drawEnd;
        private List<Point> _polygonPoints = new List<Point>();

        // Events
        public event EventHandler? RoiChanged;

        // Colors
        private readonly Color _roiColor = Color.FromArgb(100, 200, 255);
        private readonly Color _drawingColor = Color.FromArgb(255, 200, 60);

        public RoiSelectionOverlay(RoiService roiService)
        {
            _roiService = roiService;
            DoubleBuffered = true;
            BackColor = Color.Transparent;

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseDoubleClick += OnMouseDoubleClick;
        }

        /// <summary>Start drawing a rectangle ROI.</summary>
        public void StartRectangleSelection()
        {
            _currentDrawingShape = RoiShape.Rectangle;
            _isDrawing = false;
            _polygonPoints.Clear();
            Cursor = Cursors.Cross;
        }

        /// <summary>Start drawing an ellipse ROI.</summary>
        public void StartEllipseSelection()
        {
            _currentDrawingShape = RoiShape.Ellipse;
            _isDrawing = false;
            _polygonPoints.Clear();
            Cursor = Cursors.Cross;
        }

        /// <summary>Start drawing a polygon ROI.</summary>
        public void StartPolygonSelection()
        {
            _currentDrawingShape = RoiShape.Polygon;
            _isDrawing = false;
            _polygonPoints.Clear();
            Cursor = Cursors.Cross;
        }

        /// <summary>Cancel current selection.</summary>
        public void CancelSelection()
        {
            _currentDrawingShape = RoiShape.None;
            _isDrawing = false;
            _polygonPoints.Clear();
            Cursor = Cursors.Default;
            Invalidate();
        }

        /// <summary>Clear the ROI.</summary>
        public void ClearRoi()
        {
            _roiService.ClearRoi();
            CancelSelection();
            RoiChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (_currentDrawingShape == RoiShape.None) return;

            if (_currentDrawingShape == RoiShape.Polygon)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _polygonPoints.Add(e.Location);
                    _isDrawing = true;
                    Invalidate();
                }
            }
            else
            {
                _drawStart = e.Location;
                _drawEnd = e.Location;
                _isDrawing = true;
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isDrawing) return;

            if (_currentDrawingShape == RoiShape.Polygon)
            {
                // Show preview line to cursor
                Invalidate();
            }
            else
            {
                _drawEnd = e.Location;
                Invalidate();
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            if (!_isDrawing || _currentDrawingShape == RoiShape.Polygon) return;

            _drawEnd = e.Location;
            _isDrawing = false;

            // Create the ROI
            var bounds = GetBoundsFromPoints(_drawStart, _drawEnd);
            if (bounds.Width > 5 && bounds.Height > 5)
            {
                if (_currentDrawingShape == RoiShape.Rectangle)
                    _roiService.SetRectangleRoi(bounds);
                else if (_currentDrawingShape == RoiShape.Ellipse)
                    _roiService.SetEllipseRoi(bounds);

                RoiChanged?.Invoke(this, EventArgs.Empty);
            }

            _currentDrawingShape = RoiShape.None;
            Cursor = Cursors.Default;
            Invalidate();
        }

        private void OnMouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (_currentDrawingShape == RoiShape.Polygon && _polygonPoints.Count >= 3)
            {
                // Complete the polygon
                _roiService.SetPolygonRoi(_polygonPoints);
                _polygonPoints.Clear();
                _isDrawing = false;
                _currentDrawingShape = RoiShape.None;
                Cursor = Cursors.Default;
                RoiChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        private Rectangle GetBoundsFromPoints(Point p1, Point p2)
        {
            int x = Math.Min(p1.X, p2.X);
            int y = Math.Min(p1.Y, p2.Y);
            int w = Math.Abs(p2.X - p1.X);
            int h = Math.Abs(p2.Y - p1.Y);
            return new Rectangle(x, y, w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw current ROI
            if (_roiService.HasRoi)
            {
                var roiPath = _roiService.CurrentRoi?.ToPath();
                if (roiPath != null)
                {
                    using var fillBrush = new SolidBrush(Color.FromArgb(30, _roiColor));
                    g.FillPath(fillBrush, roiPath);

                    using var borderPen = new Pen(_roiColor, 2f);
                    borderPen.DashStyle = DashStyle.Dash;
                    g.DrawPath(borderPen, roiPath);

                    roiPath.Dispose();
                }
            }

            // Draw in-progress selection
            if (_isDrawing)
            {
                using var drawPen = new Pen(_drawingColor, 2f);
                drawPen.DashStyle = DashStyle.Dot;

                if (_currentDrawingShape == RoiShape.Rectangle)
                {
                    var bounds = GetBoundsFromPoints(_drawStart, _drawEnd);
                    g.DrawRectangle(drawPen, bounds);
                }
                else if (_currentDrawingShape == RoiShape.Ellipse)
                {
                    var bounds = GetBoundsFromPoints(_drawStart, _drawEnd);
                    g.DrawEllipse(drawPen, bounds);
                }
                else if (_currentDrawingShape == RoiShape.Polygon && _polygonPoints.Count > 0)
                {
                    // Draw completed segments
                    for (int i = 1; i < _polygonPoints.Count; i++)
                    {
                        g.DrawLine(drawPen, _polygonPoints[i - 1], _polygonPoints[i]);
                    }

                    // Draw line to cursor
                    var cursorPos = PointToClient(Cursor.Position);
                    g.DrawLine(drawPen, _polygonPoints[^1], cursorPos);

                    // Draw points
                    foreach (var pt in _polygonPoints)
                    {
                        g.FillEllipse(Brushes.Yellow, pt.X - 4, pt.Y - 4, 8, 8);
                    }
                }
            }
        }
    }
}
