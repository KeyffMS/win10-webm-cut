using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Win10WebmCut;

/// <summary>
/// Lekki dekoder podglądu oparty na istniejącym ffmpeg.exe.
/// Dekoduje klatki do BGRA, bez pośrednich plików na dysku.
/// Podgląd celowo nie odtwarza dźwięku; ścieżka audio jest zachowywana podczas eksportu.
/// </summary>
public static class PreviewDecoder
{
    public static async Task DecodeAsync(
        string input, VideoInfo info, double startSeconds, bool playback,
        Action<BitmapSource> onFrame, CancellationToken cancellationToken)
    {
        const int maxWidth = 960;
        const int fps = 12;
        int width = Math.Max(2, (int)Math.Round(Math.Min(info.Width, maxWidth) / 2.0) * 2);
        int height = Math.Max(2, (int)Math.Round(width * (double)info.Height / info.Width / 2.0) * 2);
        int stride = checked(width * 4);
        int frameBytes = checked(stride * height);

        string local = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");
        var psi = new ProcessStartInfo(File.Exists(local) ? local : "ffmpeg")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
        };

        foreach (string arg in new[] { "-hide_banner", "-loglevel", "error",
                     "-ss", startSeconds.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
                     "-i", input, "-an", "-sn", "-dn",
                     "-vf", (playback ? $"fps={fps}," : "") +
                            $"scale={width}:{height}:flags=fast_bilinear",
                     "-pix_fmt", "bgra", "-f", "rawvideo" })
            psi.ArgumentList.Add(arg);
        if (!playback)
        {
            psi.ArgumentList.Add("-frames:v");
            psi.ArgumentList.Add("1");
        }
        psi.ArgumentList.Add("pipe:1");

        using var process = new Process { StartInfo = psi };
        process.Start();
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });

        var stream = process.StandardOutput.BaseStream;
        var stopwatch = Stopwatch.StartNew();
        long frameIndex = 0;
        var buffer = new byte[frameBytes];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = 0;
                while (read < frameBytes)
                {
                    int count = await stream.ReadAsync(buffer.AsMemory(read, frameBytes - read), cancellationToken);
                    if (count == 0) break;
                    read += count;
                }
                if (read != frameBytes) break;

                var bitmap = BitmapSource.Create(width, height, 96, 96,
                    PixelFormats.Bgra32, null, buffer, stride);
                bitmap.Freeze();
                onFrame(bitmap);

                if (!playback) break;
                frameIndex++;
                var due = TimeSpan.FromSeconds(frameIndex / (double)fps);
                var delay = due - stopwatch.Elapsed;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (IOException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            try { await process.WaitForExitAsync(CancellationToken.None); }
            catch (InvalidOperationException) { }
        }
    }
}