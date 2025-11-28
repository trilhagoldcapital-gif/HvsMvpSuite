using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace HvsMvp.App
{
    /// <summary>
    /// Non-blocking continuous analysis controller (thread-safe).
    /// Periodically captures frames and runs analysis.
    /// Updated to store full FullSceneAnalysis for selective analysis support.
    /// </summary>
    public class ContinuousAnalysisController
    {
        private readonly Func<Bitmap?> _frameProvider;
        private readonly Func<Bitmap, FullSceneAnalysis> _sceneAnalyzer;
        private readonly int _intervalMs;

        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private volatile bool _busy;

        /// <summary>
        /// Evento disparado quando uma análise completa é concluída.
        /// Agora retorna FullSceneAnalysis para suporte a análise seletiva.
        /// </summary>
        public event Action<FullSceneAnalysis>? SceneAnalysisCompleted;

        /// <summary>
        /// Evento legado para compatibilidade - retorna apenas o Summary.
        /// </summary>
        public event Action<SampleFullAnalysisResult>? AnalysisCompleted;

        /// <summary>
        /// Construtor atualizado que aceita analisador de cena completa.
        /// </summary>
        public ContinuousAnalysisController(
            Func<Bitmap?> frameProvider,
            Func<Bitmap, FullSceneAnalysis> sceneAnalyzer,
            int intervalMs = 800)
        {
            _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
            _sceneAnalyzer = sceneAnalyzer ?? throw new ArgumentNullException(nameof(sceneAnalyzer));
            _intervalMs = intervalMs > 0 ? intervalMs : 800;
        }

        /// <summary>
        /// Construtor legado para compatibilidade com código existente.
        /// </summary>
        public ContinuousAnalysisController(
            Func<Bitmap?> frameProvider,
            Func<Bitmap, (SampleFullAnalysisResult analysis, SampleMaskClass?[,] mask, Bitmap maskPreview)> analyzer,
            int intervalMs = 800)
        {
            _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
            
            // Wrap the legacy analyzer to return FullSceneAnalysis
            _sceneAnalyzer = bmp =>
            {
                var (analysis, mask, maskPreview) = analyzer(bmp);
                // Note: Legacy mode uses minimal placeholder for Labels (1x1) to avoid wasteful allocation
                // Selective analysis won't work properly in legacy mode, but basic analysis will
                return new FullSceneAnalysis
                {
                    Summary = analysis,
                    Mask = mask,
                    MaskPreview = maskPreview,
                    Width = bmp.Width,
                    Height = bmp.Height,
                    Labels = new PixelLabel[1, 1] // Placeholder - legacy mode doesn't provide full labels
                };
            };
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
                        var scene = _sceneAnalyzer(frame);
                        
                        // Invoke both events for backwards compatibility
                        SceneAnalysisCompleted?.Invoke(scene);
                        AnalysisCompleted?.Invoke(scene.Summary);
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
