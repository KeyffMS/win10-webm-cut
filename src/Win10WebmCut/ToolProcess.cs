using System.Diagnostics;
using System.IO;
using System.Text;

namespace Win10WebmCut;

public enum ToolFailure { Start, Exit, Timeout, Cleanup }
public sealed class ToolProcessException : Exception
{
    public ToolFailure Failure { get; }
    public ToolProcessException(ToolFailure failure, string message, Exception? inner = null)
        : base(message, inner) => Failure = failure;
}
public sealed record ProcessResult(int ExitCode, string ErrorTail);

/// <summary>
/// Owns a process, both pipe readers and their cancellation source. Never uses a shell.
/// Stop signals are handled on a worker, not inside the caller's Cancel callback.
/// </summary>
public static class ToolProcess
{
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan CapabilityTimeout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
    public const int TextCharacterLimit = 2 * 1024 * 1024;
    private const int ErrorLines = 48;
    private const int LineLimit = 4096;
    private static int _cleanupFailed;
    public static bool IsBlocked => Volatile.Read(ref _cleanupFailed) != 0;

    public static Task<ProcessResult> RunAsync(string tool, IEnumerable<string> arguments,
        Func<Stream, CancellationToken, Task> readOutput, CancellationToken cancellationToken,
        TimeSpan? timeout = null, Action<string>? errorLine = null, Action? errorEnd = null,
        Func<string, CancellationToken, ValueTask>? asyncErrorLine = null)
    {
        string[] args = arguments.ToArray();
        return Task.Run(() => RunCoreAsync(tool, args, readOutput, cancellationToken,
            timeout, errorLine, errorEnd, asyncErrorLine));
    }

    public static async Task<string> TextAsync(string tool, IEnumerable<string> args,
        CancellationToken token, TimeSpan? timeout = null)
    {
        string text = "";
        await RunAsync(tool, args, async (stream, readToken) =>
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
            var builder = new StringBuilder();
            char[] block = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(block.AsMemory(), readToken).ConfigureAwait(false)) > 0)
            {
                if (builder.Length + count > TextCharacterLimit)
                    throw new InvalidDataException("Odpowiedź narzędzia przekroczyła limit 2 Mi znaków.");
                builder.Append(block, 0, count);
            }
            text = builder.ToString();
        }, token, timeout ?? ProbeTimeout).ConfigureAwait(false);
        return text;
    }

    private static async Task<ProcessResult> RunCoreAsync(string tool, string[] args,
        Func<Stream, CancellationToken, Task> readOutput, CancellationToken token,
        TimeSpan? timeout, Action<string>? errorLine, Action? errorEnd,
        Func<string, CancellationToken, ValueTask>? asyncErrorLine)
    {
        token.ThrowIfCancellationRequested();
        if (IsBlocked)
            throw new ToolProcessException(ToolFailure.Cleanup,
                "Poprzedni proces nie zakończył się poprawnie. Zamknij aplikację i sprawdź log przed ponownym uruchomieniem.");
        string executable;
        try { executable = ToolResolver.Resolve(tool); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { throw new ToolProcessException(ToolFailure.Start, $"Nie można odnaleźć/otworzyć {tool}: {ex.Message}", ex); }
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string arg in args) start.ArgumentList.Add(arg);
        var process = new Process { StartInfo = start };
        var readerStop = new CancellationTokenSource();
        var tail = new Queue<string>();
        bool deferredCleanup = false;
        try
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!process.Start()) throw new IOException("System nie uruchomił procesu.");
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
            {
                throw new ToolProcessException(ToolFailure.Start, $"Nie można uruchomić {tool}: {ex.Message}", ex);
            }
            AppLog.Write("process-start", $"{tool}; PID={process.Id}; path={executable}");
            var failure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var externalRegistration = token.Register(() => stop.TrySetResult());
            using var deadline = new CancellationTokenSource();
            if (timeout is { } duration) deadline.CancelAfter(duration);
            using var deadlineRegistration = deadline.Token.Register(() => stop.TrySetResult());

            async Task ObserveAsync(Task task)
            {
                try { await task.ConfigureAwait(false); }
                catch (Exception ex) { failure.TrySetResult(ex); throw; }
            }
            Task output = ObserveAsync(ReadOutputAsync());
            async Task ReadOutputAsync() =>
                await readOutput(process.StandardOutput.BaseStream, readerStop.Token).ConfigureAwait(false);
            Task errors = ObserveAsync(ReadErrorsAsync());
            async Task ReadErrorsAsync()
            {
                try
                {
                    await ReadBoundedLinesAsync(process.StandardError, async line =>
                    {
                        tail.Enqueue(line);
                        if (tail.Count > ErrorLines) tail.Dequeue();
                        errorLine?.Invoke(line);
                        if (asyncErrorLine is not null)
                            await asyncErrorLine(line, readerStop.Token).ConfigureAwait(false);
                    }, readerStop.Token).ConfigureAwait(false);
                }
                finally { errorEnd?.Invoke(); }
            }
            Task exited = ObserveAsync(process.WaitForExitAsync(CancellationToken.None));
            Task all = Task.WhenAll(output, errors, exited);
            Task first = await Task.WhenAny(all, failure.Task, stop.Task).ConfigureAwait(false);
            if (first != all || !all.IsCompletedSuccessfully || token.IsCancellationRequested || deadline.IsCancellationRequested)
            {
                Exception? originalFailure = failure.Task.IsCompletedSuccessfully ? failure.Task.Result : null;
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                { AppLog.Write("process-kill", ex.ToString()); }
                readerStop.Cancel();
                try { await all.WaitAsync(CleanupTimeout).ConfigureAwait(false); }
                catch (TimeoutException) when (!all.IsCompleted)
                {
                    // Do not dispose resources still used by readers. A single retained
                    // owner observes their eventual end. Block further process starts.
                    Interlocked.Exchange(ref _cleanupFailed, 1);
                    deferredCleanup = true;
                    _ = DisposeAfterExitAsync(all, process, readerStop);
                    throw new ToolProcessException(ToolFailure.Cleanup,
                        $"Nie udało się zakończyć {tool} w limicie cleanup. Zasoby pozostają pod obserwacją; kolejne operacje są zablokowane.");
                }
                catch (Exception ex) { originalFailure ??= ex; }
                if (token.IsCancellationRequested)
                    throw new OperationCanceledException("Operacja została anulowana.", originalFailure, token);
                if (deadline.IsCancellationRequested)
                    throw new ToolProcessException(ToolFailure.Timeout, $"Przekroczono limit czasu narzędzia {tool}.", originalFailure);
                if (originalFailure is not null)
                    throw new IOException($"Błąd odczytu procesu {tool}: {originalFailure.Message}", originalFailure);
            }
            await all.ConfigureAwait(false);
            string errorTail = string.Join(Environment.NewLine, tail);
            if (process.ExitCode != 0)
                throw new ToolProcessException(ToolFailure.Exit,
                    $"{tool} zakończył się kodem {process.ExitCode}.\n{errorTail}");
            AppLog.Write("process-end", $"{tool}; PID={process.Id}; exit=0");
            return new ProcessResult(process.ExitCode, errorTail);
        }
        finally
        {
            if (!deferredCleanup) { readerStop.Dispose(); process.Dispose(); }
        }
    }

    private static async Task DisposeAfterExitAsync(Task all, Process process, CancellationTokenSource readerStop)
    {
        try { await all.ConfigureAwait(false); }
        catch (Exception ex) { AppLog.Write("deferred-cleanup", ex.ToString()); }
        finally { readerStop.Dispose(); process.Dispose(); }
    }

    private static async Task ReadBoundedLinesAsync(StreamReader reader, Func<string, ValueTask> consume, CancellationToken token)
    {
        char[] block = new char[2048];
        var line = new StringBuilder();
        bool truncated = false;
        int count;
        while ((count = await reader.ReadAsync(block.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            for (int i = 0; i < count; i++)
            {
                char c = block[i];
                if (c is '\n' or '\r')
                {
                    if (line.Length > 0) await consume(line.ToString() + (truncated ? " …[limit]" : "")).ConfigureAwait(false);
                    line.Clear(); truncated = false;
                }
                else if (line.Length < LineLimit) line.Append(c);
                else truncated = true;
            }
        }
        if (line.Length > 0) await consume(line.ToString() + (truncated ? " …[limit]" : "")).ConfigureAwait(false);
    }
}
