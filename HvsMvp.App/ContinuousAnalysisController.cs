using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace HvsMvp.App
{
    /// <summary>
    /// Non-blocking continuous analysis controller (thread-safe).
    /// Periodically captures frames and runs analysis.
    /// </summary>
    public class ContinuousAnalysisController
    {
        private readonly Func<Bitmap?> _frameProvider;
        private readonly Func<Bitmap, (SampleFullAnalysisResult analysis, SampleMaskClass?[,] mask, Bitmap maskPreview)> _analyzer;
        private readonly int _intervalMs;

        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private volatile bool _busy;

        public event Action<SampleFullAnalysisResult>? AnalysisCompleted;

        public ContinuousAnalysisController(
            Func<Bitmap?> frameProvider,
            Func<Bitmap, (SampleFullAnalysisResult, SampleMaskClass?[,], Bitmap)> analyzer,
            int intervalMs = 800)
        {
            _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _intervalMs = intervalMs > 0 ? intervalMs : 800;
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

                    Bitmap? frame = null;
                    try
                    {
                        frame = _frameProvider?.Invoke();
                        if (frame == null)
                        {
                            await Task.Delay(_intervalMs, ct);
                            continue;
                        }

                        _busy = true;
                        var result = _analyzer(frame);
                        AnalysisCompleted?.Invoke(result.analysis);
                    }
                    catch
                    {
                        // Silently ignore analysis errors
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
                    // Ignore other exceptions
                }

                try
                {
                    await Task.Delay(_intervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
