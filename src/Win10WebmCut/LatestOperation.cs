using System.Diagnostics;
using System.Threading.Channels;

namespace Win10WebmCut;

/// <summary>
/// One worker, one active operation and one replaceable pending request.
/// Cancel is serialized with Dispose; Dispose follows the awaited operation.
/// </summary>
public sealed class LatestOperation<T> : IAsyncDisposable
{
    private sealed record Request(long Generation, T Value);
    private readonly object _gate = new();
    private readonly Channel<Request> _requests = Channel.CreateBounded<Request>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = false });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Func<T, long, CancellationToken, Task> _run;
    private readonly Action<long, Exception> _failed;
    private readonly Action<long>? _completed;
    private readonly TimeSpan _minimumStartInterval;
    private readonly Task _worker;
    private CancellationTokenSource? _active;
    private TaskCompletionSource _idle = CompletedSignal();
    private long _generation;
    private long _visibleGeneration;
    private bool _closing;
    private Task? _closeTask;

    public LatestOperation(Func<T, long, CancellationToken, Task> run,
        Action<long, Exception> failed, Action<long>? completed = null,
        TimeSpan minimumStartInterval = default)
    {
        _run = run;
        _failed = failed;
        _completed = completed;
        _minimumStartInterval = minimumStartInterval;
        _worker = Task.Run(PumpAsync);
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }

    public bool IsCurrent(long generation)
    {
        lock (_gate) return !_closing && generation == _visibleGeneration;
    }

    public bool IsLatest(long generation)
    {
        lock (_gate) return !_closing && generation == _generation;
    }

    public long RequestLatest(T value, bool interruptActive = true)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            long generation = ++_generation;
            // A still-frame seek may finish while a newer target waits in the single
            // pending slot. Playback and file replacement interrupt immediately.
            if (interruptActive || _active is null)
            {
                _visibleGeneration = generation;
                _active?.Cancel(); // Only signals; never Kill on the UI thread.
            }
            _requests.Writer.TryWrite(new Request(generation, value));
            return generation;
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            _visibleGeneration = ++_generation; // Invalidate callbacks before canceling.
            while (_requests.Reader.TryRead(out _)) { }
            _active?.Cancel();
            return _idle.Task;
        }
    }

    private async Task PumpAsync()
    {
        long lastStart = 0;
        try
        {
            while (await _requests.Reader.WaitToReadAsync(_shutdown.Token).ConfigureAwait(false))
            {
                if (!_requests.Reader.TryRead(out Request? item)) continue;
                if (lastStart != 0)
                {
                    TimeSpan remaining = _minimumStartInterval - Stopwatch.GetElapsedTime(lastStart);
                    if (remaining > TimeSpan.Zero)
                        await Task.Delay(remaining, _shutdown.Token).ConfigureAwait(false);
                }
                // Fixed rate limit, not an endlessly postponed debounce.
                while (_requests.Reader.TryRead(out Request? newer)) item = newer;
                CancellationTokenSource active;
                lock (_gate)
                {
                    if (_closing || item.Generation != _generation) continue;
                    active = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    _active = active;
                    _visibleGeneration = item.Generation;
                    _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                lastStart = Stopwatch.GetTimestamp();
                try
                {
                    using var activity = AppLog.BeginOperation($"request-{item.Generation}");
                    active.Token.ThrowIfCancellationRequested();
                    await _run(item.Value, item.Generation, active.Token).ConfigureAwait(false);
                    if (!active.IsCancellationRequested && IsCurrent(item.Generation))
                        _completed?.Invoke(item.Generation);
                }
                catch (OperationCanceledException) when (active.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    // Observe failures even for a superseded generation.
                    AppLog.Write("operation", ex.ToString());
                    if (IsCurrent(item.Generation)) _failed(item.Generation, ex);
                }
                finally
                {
                    lock (_gate)
                    {
                        _active = null;
                        active.Dispose();
                        _idle.TrySetResult();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppLog.Write("operation-pump", ex.ToString());
            // The failure callback must only enqueue a guarded UI notification.
            _failed(Volatile.Read(ref _generation), ex);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_closeTask is not null) return new ValueTask(_closeTask);
            _closing = true;
            _visibleGeneration = ++_generation;
            _requests.Writer.TryComplete();
            _active?.Cancel();
            _shutdown.Cancel();
            _closeTask = FinishCloseAsync();
            return new ValueTask(_closeTask);
        }
    }

    private async Task FinishCloseAsync()
    {
        await _worker.ConfigureAwait(false);
        _shutdown.Dispose();
    }
}
