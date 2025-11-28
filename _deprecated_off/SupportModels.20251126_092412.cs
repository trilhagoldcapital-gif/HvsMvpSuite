using System;
using System.Collections.Generic;
using System.Text;

namespace HvsMvp.App
{
    // Modelos mínimos (ajuste se já houver versões mais completas)
    public class HvsConfig
    {
        public AppInfo App { get; set; }
        public MaterialCatalog Materials { get; set; }
    }

    public class AppInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }

    public class MaterialCatalog
    {
        public List<HvsMaterial> Metais { get; set; }
        public List<HvsMaterial> Cristais { get; set; }
        public List<HvsMaterial> Gemas { get; set; }
    }

    public class HvsMaterial
    {
        public string Id { get; set; }
        public string Nome { get; set; }
        public string Grupo { get; set; }
        public object Optico { get; set; } // pode ser hashtable/dynamic JSON
    }

    public class ImageDiagnosticsResult
    {
        public double FocusScore { get; set; }
        public double SaturationClippingFraction { get; set; }
        public double ForegroundFraction { get; set; }
    }

    public class MetalResult
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public double PctSample { get; set; }
        public double? PpmEstimated { get; set; }
        public double Score { get; set; }
    }

    public class CrystalResult
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double PctSample { get; set; }
        public double Score { get; set; }
    }

    public class GemResult
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double PctSample { get; set; }
        public double Score { get; set; }
    }

    public class SampleFullAnalysisResult
    {
        public Guid Id { get; set; }
        public string ImagePath { get; set; }
        public DateTime CaptureDateTimeUtc { get; set; }
        public ImageDiagnosticsResult Diagnostics { get; set; } = new ImageDiagnosticsResult();
        public List<MetalResult> Metals { get; set; } = new List<MetalResult>();
        public List<CrystalResult> Crystals { get; set; } = new List<CrystalResult>();
        public List<GemResult> Gems { get; set; } = new List<GemResult>();
        public string ShortReport { get; set; }
    }

    // Simulação de serviço de câmera (substituir por implementação real)
    public class MicroscopeCameraService : IDisposable
    {
        public int DeviceIndex { get; set; }
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public event Action<Bitmap> FrameReceived;
        private System.Windows.Forms.Timer _timer;
        private bool _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 500;
            _timer.Tick += (s, e) =>
            {
                if (!_running) return;
                var bmp = new Bitmap(Width, Height);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    g.DrawString("Frame Simulado", new Font("Segoe UI", 14), Brushes.Gold, new PointF(10, 10));
                }
                if (FrameReceived != null) FrameReceived(bmp);
            };
            _timer.Start();
        }

        public void Stop()
        {
            _running = false;
            if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
