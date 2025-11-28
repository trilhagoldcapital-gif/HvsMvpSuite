using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace HvsMvp.App
{
    // Controlador de análise contínua não bloqueante (thread-safe)
    public class ContinuousAnalysisController
    {
        private readonly Func<Bitmap> _frameProvider;
        private readonly Func<Bitmap, (SampleFullAnalysisResult analysis, SampleMaskClass[,] mask, Bitmap maskPreview)> _analyzer;
        private readonly int _intervalMs;

        private CancellationTokenSource _cts;
        private Task _loopTask;
        private volatile bool _busy;

        public event Action<SampleFullAnalysisResult> AnalysisCompleted;

        public ContinuousAnalysisController(
            Func<Bitmap> frameProvider,
            Func<Bitmap, (SampleFullAnalysisResult, SampleMaskClass[,], Bitmap)> analyzer,
            int intervalMs = 800)
        {
            _frameProvider = frameProvider;
            _analyzer = analyzer;
            _intervalMs = intervalMs;
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                }
            }
            catch { }
            _cts = null;
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_busy)
                    {
                        await Task.Delay(_intervalMs, ct);
                        continue;
                    }

                    Bitmap frame = null;
                    try
                    {
                        frame = _frameProvider?.Invoke();
                        if (frame == null)
                        {
                            await Task.Delay(_intervalMs, ct);
                            continue;
                        }

                        _busy = true; // evita concorrência
                        var result = _analyzer(frame);
                        AnalysisCompleted?.Invoke(result.analysis);
                    }
                    catch
                    {
                        // silencioso
                    }
                    finally
                    {
                        frame?.Dispose();
                        _busy = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }

                await Task.Delay(_intervalMs, ct);
            }
        }
    }
}
