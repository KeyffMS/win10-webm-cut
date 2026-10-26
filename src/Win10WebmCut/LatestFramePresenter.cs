using System.Windows.Threading;

namespace Win10WebmCut;

/// <summary>At most one queued dispatcher callback and one replaceable image slot.</summary>
public sealed class LatestFramePresenter : IDisposable
{
    private readonly object _gate = new();
    private readonly Dispatcher _dispatcher;
    private readonly Func<long, bool> _isCurrent;
    private readonly Action<long, PreviewFrameData> _present;
    private (long Generation, PreviewFrameData Frame)? _latest;
    private bool _queued;
    private bool _disposed;

    public LatestFramePresenter(Dispatcher dispatcher, Func<long, bool> isCurrent,
        Action<long, PreviewFrameData> present)
    { _dispatcher = dispatcher; _isCurrent = isCurrent; _present = present; }

    public void Publish(long generation, PreviewFrameData frame)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _latest = (generation, frame);
            if (_queued) return;
            _queued = true;
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            { _queued = false; _latest = null; return; }
            try { _dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(Drain)); }
            catch (InvalidOperationException)
            { _queued = false; _latest = null; }
        }
    }

    private void Drain()
    {
        (long Generation, PreviewFrameData Frame)? item;
        lock (_gate)
        {
            item = _disposed ? null : _latest;
            _latest = null;
            _queued = false;
        }
        if (item is { } accepted && _isCurrent(accepted.Generation)) _present(accepted.Generation, accepted.Frame);
    }

    public void Clear() { lock (_gate) _latest = null; }
    public void Dispose() { lock (_gate) { _disposed = true; _latest = null; } }
}
