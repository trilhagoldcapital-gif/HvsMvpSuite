using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;

namespace HvsMvp.App
{
    /// <summary>
    /// Serviço simples de captura de vídeo para o microscópio usando OpenCvSharp.
    /// Adaptado do MicroLab antigo.
    /// </summary>
    public class MicroscopeCameraService : IDisposable
    {
        private VideoCapture? _capture;
        private Thread? _worker;
        private bool _running;

        public event Action<Bitmap>? FrameReceived;

        public int DeviceIndex { get; set; } = 1; // ajuste se precisar
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;

        public void Start()
        {
            if (_running)
                return;

            _running = true;

            _capture?.Release();
            _capture?.Dispose();
            _capture = null;

            _capture = new VideoCapture(DeviceIndex);
            if (!_capture.IsOpened())
            {
                _running = false;
                throw new InvalidOperationException("Não foi possível abrir a câmera do microscópio (índice " + DeviceIndex + ").");
            }

            if (Width > 0 && Height > 0)
            {
                _capture.Set(VideoCaptureProperties.FrameWidth, Width);
                _capture.Set(VideoCaptureProperties.FrameHeight, Height);
            }

            try
            {
                // MJPG costuma ser mais estável/rápido para câmeras USB
                _capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
            }
            catch
            {
            }

            _worker = new Thread(CaptureLoop)
            {
                IsBackground = true
            };
            _worker.Start();
        }

        public void Stop()
        {
            _running = false;
            try
            {
                if (_worker != null && _worker.IsAlive)
                {
                    if (!_worker.Join(1000))
                    {
                        try { _worker.Interrupt(); } catch { }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                _worker = null;
            }
        }

        private void CaptureLoop()
        {
            using var mat = new Mat();
            while (_running && _capture != null && _capture.IsOpened())
            {
                try
                {
                    if (!_capture.Read(mat) || mat.Empty())
                        continue;

                    using var bgr = mat.Clone();
                    using var bmp = MatToBitmap(bgr);
                    var clone = (Bitmap)bmp.Clone();
                    FrameReceived?.Invoke(clone);
                }
                catch
                {
                    // ignora erros de captura
                }
            }
        }

        private static Bitmap MatToBitmap(Mat mat)
        {
            // Garante formato BGR de 8 bits, 3 canais
            using var m = mat.Clone();
            if (m.Type() != MatType.CV_8UC3)
            {
                using var converted = new Mat();
                Cv2.CvtColor(m, converted, ColorConversionCodes.BGRA2BGR);
                return MatToBitmapInternal(converted);
            }
            return MatToBitmapInternal(m);
        }

        private static Bitmap MatToBitmapInternal(Mat mat)
        {
            int width = mat.Width;
            int height = mat.Height;

            // Obtém todos os bytes da imagem (BGR) em um array
            int channels = mat.Channels();
            int bytesPerPixel = channels; // deve ser 3
            int srcStride = width * bytesPerPixel;
            byte[] srcData = new byte[srcStride * height];
            Marshal.Copy(mat.Data, srcData, 0, srcData.Length);

            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var data = bmp.LockBits(new Rectangle(0, 0, width, height),
                                    ImageLockMode.WriteOnly,
                                    PixelFormat.Format24bppRgb);
            try
            {
                int dstStride = data.Stride;
                int copyWidth = Math.Min(srcStride, dstStride);

                for (int y = 0; y < height; y++)
                {
                    IntPtr dstPtr = IntPtr.Add(data.Scan0, y * dstStride);
                    int srcOffset = y * srcStride;
                    Marshal.Copy(srcData, srcOffset, dstPtr, copyWidth);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        public void Dispose()
        {
            Stop();

            try
            {
                _capture?.Release();
            }
            catch
            {
            }

            _capture?.Dispose();
            _capture = null;
        }
    }
}
