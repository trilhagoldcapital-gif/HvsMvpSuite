using System;
using System.Drawing;
using System.Threading;
using OpenCvSharp;

namespace HvsMvp.App
{
    /// <summary>
    /// Serviço de câmera baseado em OpenCV, com captura em thread separada
    /// e evento FrameReady com Bitmap pronto para a UI.
    /// </summary>
    public class OpenCvCameraService : IDisposable
    {
        private VideoCapture? _capture;
        private Thread? _captureThread;
        private volatile bool _running;
        private readonly object _lock = new object();

        public event EventHandler<Bitmap>? FrameReady;
        public event EventHandler<string>? CameraError;

        public bool IsRunning => _running;

        public void Start(int deviceIndex, int width, int height, int fps = 30)
        {
            lock (_lock)
            {
                if (_running)
                    return;

                try
                {
                    _capture = new VideoCapture(deviceIndex);
                    if (!_capture.IsOpened())
                    {
                        OnCameraError($"Não foi possível abrir a câmera (índice {deviceIndex}).");
                        _capture.Release();
                        _capture.Dispose();
                        _capture = null;
                        return;
                    }

                    // Configura resolução e fps (melhor esforço)
                    if (width > 0 && height > 0)
                    {
                        _capture.Set(VideoCaptureProperties.FrameWidth, width);
                        _capture.Set(VideoCaptureProperties.FrameHeight, height);
                    }
                    if (fps > 0)
                    {
                        _capture.Set(VideoCaptureProperties.Fps, fps);
                    }

                    _running = true;

                    _captureThread = new Thread(CaptureLoop)
                    {
                        IsBackground = true,
                        Name = "OpenCvCameraService.CaptureLoop"
                    };
                    _captureThread.Start();
                }
                catch (Exception ex)
                {
                    OnCameraError($"Erro ao iniciar câmera: {ex.Message}");
                    Stop();
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _running = false;
            }

            try
            {
                if (_captureThread != null && _captureThread.IsAlive)
                {
                    if (!_captureThread.Join(1000))
                    {
                        try
                        {
                            _captureThread.Interrupt();
                        }
                        catch { }
                    }
                }
            }
            catch { }

            lock (_lock)
            {
                if (_capture != null)
                {
                    try
                    {
                        if (_capture.IsOpened())
                            _capture.Release();
                    }
                    catch { }

                    _capture.Dispose();
                    _capture = null;
                }

                _captureThread = null;
            }
        }

        private void CaptureLoop()
        {
            try
            {
                using var frame = new Mat();

                while (_running)
                {
                    VideoCapture? cap;
                    lock (_lock)
                    {
                        cap = _capture;
                    }

                    if (cap == null || !cap.IsOpened())
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    try
                    {
                        if (!cap.Read(frame) || frame.Empty())
                        {
                            Thread.Sleep(5);
                            continue;
                        }

                        // Converte Mat para Bitmap (BGR -> BGRA)
                        using var bgra = new Mat();
                        Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);

                        var bmp = new Bitmap(
                            bgra.Cols,
                            bgra.Rows,
                            bgra.Cols * 4,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb,
                            bgra.Data);

                        var clone = (Bitmap)bmp.Clone();
                        bmp.Dispose();

                        OnFrameReady(clone); // quem receber deve Dispose() depois de usar
                    }
                    catch (Exception ex)
                    {
                        OnCameraError($"Erro na leitura de frame: {ex.Message}");
                        Thread.Sleep(50);
                    }

                    // Controle simples de FPS (~30fps)
                    Thread.Sleep(10);
                }
            }
            catch (ThreadInterruptedException)
            {
                // Encerrando thread
            }
            catch (Exception ex)
            {
                OnCameraError($"Loop de captura encerrou com erro: {ex.Message}");
            }
        }

        protected virtual void OnFrameReady(Bitmap bmp)
        {
            FrameReady?.Invoke(this, bmp);
        }

        protected virtual void OnCameraError(string message)
        {
            CameraError?.Invoke(this, message);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
