using System.IO;

namespace Win10WebmCut;

public static class FfmpegService
{
    public static Task<VideoInfo> ProbeAsync(string input, CancellationToken token) => MediaProbe.ReadAsync(input, token);

    public static async Task ExportAsync(ExportRequest snapshot, string output, bool allowOverwrite,
        IProgress<string>? progress, CancellationToken token)
    {
        using var activity = AppLog.BeginOperation("export");
        ExportPlan plan = ExportValidation.Plan(snapshot);
        ExportRequest request = plan.Request;
        string expectedExtension = request.Profile.OutputExtension(request.Source.FullPath);
        if (!Path.GetExtension(output).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Ten profil zapisuje plik {expectedExtension}.");
        if (FileRevision.Read(request.Source.FullPath) != request.Source.Stamp)
            throw new IOException("Źródło zmieniło się od odczytu metadanych. Otwórz je ponownie.");
        await ExportTransaction.WriteAsync(request.Source.FullPath, output, allowOverwrite, async (temporary, ct) =>
        {
            progress?.Report("Sprawdzanie narzędzi i możliwości profilu…");
            ToolCapabilities capabilities = await ToolCapabilities.ReadAsync(ct).ConfigureAwait(false);
            capabilities.Require(plan);
            progress?.Report("Eksport do pliku roboczego…");
            string[] args = Arguments(plan, temporary);
            DateTime nextUpdate = DateTime.MinValue;
            await ToolProcess.RunAsync("ffmpeg", args, (stream, readToken) => stream.CopyToAsync(Stream.Null, readToken),
                ct, errorLine: line =>
                {
                    if ((line.Contains("time=", StringComparison.Ordinal) || line.Contains("Error", StringComparison.OrdinalIgnoreCase)) &&
                        DateTime.UtcNow >= nextUpdate)
                    { nextUpdate = DateTime.UtcNow.AddMilliseconds(200); progress?.Report(line); }
                }).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            progress?.Report("Sprawdzanie metadanych wyniku przed zatwierdzeniem…");
            VideoInfo result = await MediaProbe.ReadAsync(temporary, ct).ConfigureAwait(false);
            if (result.Width != plan.Width || result.Height != plan.Height)
                throw new InvalidDataException("Wymiary wyniku różnią się od planu eksportu. Poprzedni plik pozostawiono bez zmian.");
            if (FileRevision.Read(request.Source.FullPath) != request.Source.Stamp)
                throw new IOException("Źródło zmieniło się podczas eksportu. Wynik nie został zatwierdzony.");
        }, token).ConfigureAwait(false);
    }

    private static string[] Arguments(ExportPlan plan, string output)
    {
        ExportRequest request = plan.Request;
        ExportProfile profile = request.Profile;
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-ss", TimePolicy.Number(request.Start), "-noautorotate" };
        // Override the input matrix only when explicitly normalizing pixels. Copy retains metadata.
        if (profile.Id != ExportProfileId.Copy)
            args.AddRange([$"-display_rotation:{request.Source.Video.Index}", "0"]);
        args.AddRange(["-i", request.Source.FullPath, "-t", TimePolicy.Number(request.End - request.Start)]);
        if (profile.Id == ExportProfileId.Copy) args.AddRange(["-map", "0", "-map_metadata", "0", "-map_chapters", "0"]);
        else
        {
            args.AddRange(["-map", $"0:{request.Source.Video.Index}", "-map", "0:a?", "-sn", "-dn",
                "-map_metadata", "-1", "-map_metadata:s", "-1", "-map_chapters", "-1", "-metadata:s:v:0", "rotate=0"]);
            var filters = VideoGeometry.From(request.Source).Filters().ToList();
            if (request.Crop is { } crop) filters.Add($"crop={crop.Width}:{crop.Height}:{crop.X}:{crop.Y}");
            if (profile.IsWebm && profile.ScaleDivisor != 1) filters.Add($"scale={plan.Width}:{plan.Height}:flags=lanczos");
            args.AddRange(["-vf", string.Join(',', filters)]);
            void Color(string option, string value)
            {
                if (!string.IsNullOrWhiteSpace(value) && value is not ("unknown" or "unspecified" or "reserved"))
                    args.AddRange([option, value]);
            }
            StreamInfo video = request.Source.Video;
            Color("-color_primaries", video.ColorPrimaries); Color("-color_trc", video.ColorTransfer);
            Color("-colorspace", video.ColorSpace); Color("-color_range", video.ColorRange);
        }
        args.AddRange(profile.CodecArguments);
        if (profile.Id == ExportProfileId.Ffv1)
            args.AddRange(["-pix_fmt", "+" + request.Source.Video.PixelFormat]); // Reject implicit format conversion.
        args.AddRange(["-f", plan.Muxer, output]);
        return args.ToArray();
    }
}
