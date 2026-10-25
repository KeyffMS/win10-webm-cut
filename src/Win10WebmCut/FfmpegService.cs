using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Win10WebmCut;

public sealed record VideoInfo(double Duration, int Width, int Height);
public sealed record CropArea(int X, int Y, int Width, int Height);
public enum ExportProfile { Copy, Ffv1, WebmFull, WebmHalf, WebmQuarter }

public static class FfmpegService
{
    private static string Tool(string name)
    {
        string local = Path.Combine(AppContext.BaseDirectory, "tools", name + ".exe");
        return File.Exists(local) ? local : name;
    }

    private static ProcessStartInfo StartInfo(string executable, IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo(Tool(executable))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in arguments) psi.ArgumentList.Add(argument);
        return psi;
    }

    public static async Task<VideoInfo> ProbeAsync(string input, CancellationToken cancellationToken)
    {
        string[] args = ["-v", "error", "-select_streams", "v:0",
            "-show_entries", "stream=width,height:format=duration",
            "-of", "json", input];
        using var process = new Process { StartInfo = StartInfo("ffprobe", args) };
        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string json = await stdout;
        string error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException("ffprobe: " + error);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var streams = root.GetProperty("streams");
        if (streams.GetArrayLength() == 0) throw new InvalidOperationException("Nie znaleziono strumienia wideo.");
        var video = streams[0];
        int width = video.GetProperty("width").GetInt32();
        int height = video.GetProperty("height").GetInt32();
        double duration = root.GetProperty("format").GetProperty("duration").GetDouble();
        if (duration <= 0 || width <= 0 || height <= 0 || !double.IsFinite(duration))
            throw new InvalidOperationException("Niepoprawne metadane wideo.");
        return new VideoInfo(duration, width, height);
    }

    public static async Task ExportAsync(string input, string output, double start, double end,
        CropArea? crop, ExportProfile profile, IProgress<string>? progress, CancellationToken token)
    {
        if (end <= start || start < 0) throw new ArgumentException("Nieprawidłowy zakres czasu.");
        if (profile == ExportProfile.Copy && crop is not null)
            throw new ArgumentException("Crop wymaga ponownego kodowania.");
        if (Path.GetFullPath(input).Equals(Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Plik wynikowy musi być inny niż wejściowy.");

        string sec(double d) => d.ToString("0.######", CultureInfo.InvariantCulture);
        var args = new List<string> { "-hide_banner", "-y", "-ss", sec(start), "-i", input, "-t", sec(end - start) };
        if (profile == ExportProfile.Copy)
        {
            args.AddRange(["-map", "0", "-c", "copy", "-avoid_negative_ts", "make_zero"]);
        }
        else
        {
            args.AddRange(["-map", "0:v:0", "-map", "0:a?", "-sn", "-dn"]);
            var filters = new List<string>();
            if (crop is not null)
                filters.Add($"crop={crop.Width}:{crop.Height}:{crop.X}:{crop.Y}");
            if (profile is ExportProfile.WebmHalf or ExportProfile.WebmQuarter)
            {
                int divisor = profile == ExportProfile.WebmHalf ? 2 : 4;
                filters.Add($"scale=trunc(iw/{divisor}/2)*2:trunc(ih/{divisor}/2)*2:flags=lanczos");
            }
            if (filters.Count > 0) args.AddRange(["-vf", string.Join(",", filters)]);
            if (profile == ExportProfile.Ffv1)
                args.AddRange(["-c:v", "ffv1", "-level", "3", "-c:a", "flac"]);
            else
                args.AddRange(["-c:v", "libvpx-vp9", "-deadline", "good", "-cpu-used", "4",
                    "-crf", "32", "-b:v", "0", "-row-mt", "1", "-c:a", "libopus", "-b:a", "96k"]);
        }
        args.Add(output);

        using var process = new Process { StartInfo = StartInfo("ffmpeg", args) };
        process.Start();
        try
        {
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(token);
            var lastLines = new Queue<string>();
            Task consumeErrors = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync(token) is { } line)
                {
                    progress?.Report(line);
                    lastLines.Enqueue(line);
                    if (lastLines.Count > 18) lastLines.Dequeue();
                }
            }, token);
            await Task.WhenAll(process.WaitForExitAsync(token), stdout, consumeErrors);
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Błąd FFmpeg:\n" + string.Join("\n", lastLines));
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            try { await process.WaitForExitAsync(CancellationToken.None); } catch { }
            throw;
        }
    }
}