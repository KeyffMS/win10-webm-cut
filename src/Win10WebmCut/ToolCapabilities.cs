using System.IO;
using System.Text.RegularExpressions;

namespace Win10WebmCut;

public sealed class ToolCapabilities
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static (string Key, ToolCapabilities Value)? _cached;
    private readonly HashSet<string> _encoders;
    private readonly HashSet<string> _muxers;
    private readonly HashSet<string> _ffv1Formats;
    public string Description { get; }

    private ToolCapabilities(string encoders, string muxers, string ffv1Help, string description)
    {
        _encoders = Regex.Matches(encoders, @"(?m)^\s*[A-Z.]{6}\s+(\S+)\s", RegexOptions.CultureInvariant)
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        _muxers = Regex.Matches(muxers, @"(?m)^\s*E\s+(\S+)\s", RegexOptions.CultureInvariant)
            .SelectMany(m => m.Groups[1].Value.Split(',')).ToHashSet(StringComparer.Ordinal);
        Match formats = Regex.Match(ffv1Help, @"Supported pixel formats:\s*([^\r\n]+)", RegexOptions.CultureInvariant);
        _ffv1Formats = formats.Success ? formats.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal) : [];
        Description = description;
    }

    public static async Task<ToolCapabilities> ReadAsync(CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            string Fingerprint(string name)
            {
                string path = ToolResolver.Resolve(name);
                var info = new FileInfo(path);
                return $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            }
            string key = Fingerprint("ffmpeg") + ";" + Fingerprint("ffprobe");
            if (_cached is { } cached && cached.Key == key) return cached.Value;
            string version = await ToolProcess.TextAsync("ffmpeg", ["-version"], token, ToolProcess.CapabilityTimeout).ConfigureAwait(false);
            string probeVersion = await ToolProcess.TextAsync("ffprobe", ["-version"], token, ToolProcess.CapabilityTimeout).ConfigureAwait(false);
            string encoders = await ToolProcess.TextAsync("ffmpeg", ["-hide_banner", "-encoders"], token, ToolProcess.CapabilityTimeout).ConfigureAwait(false);
            string muxers = await ToolProcess.TextAsync("ffmpeg", ["-hide_banner", "-muxers"], token, ToolProcess.CapabilityTimeout).ConfigureAwait(false);
            string help = encoders.Contains(" ffv1 ", StringComparison.Ordinal)
                ? await ToolProcess.TextAsync("ffmpeg", ["-hide_banner", "-h", "encoder=ffv1"], token, ToolProcess.CapabilityTimeout).ConfigureAwait(false) : "";
            string description = version.Split('\n')[0].Trim() + Environment.NewLine + probeVersion.Split('\n')[0].Trim();
            var result = new ToolCapabilities(encoders, muxers, help, description);
            _cached = (key, result);
            AppLog.SetToolsDescription(description);
            return result;
        }
        finally { Gate.Release(); }
    }

    public void Require(ExportPlan plan)
    {
        ExportProfile profile = plan.Request.Profile;
        if (!_muxers.Contains(plan.Muxer)) throw new InvalidOperationException($"FFmpeg nie zawiera muksera {plan.Muxer}.");
        foreach (string encoder in new[] { profile.VideoEncoder,
            plan.Request.Source.Streams.Any(s => s.Type == "audio") ? profile.AudioEncoder : "copy" })
            if (encoder != "copy" && !_encoders.Contains(encoder))
                throw new InvalidOperationException($"FFmpeg nie zawiera enkodera {encoder}.");
        if (profile.Id == ExportProfileId.Ffv1 && !_ffv1Formats.Contains(plan.Request.Source.Video.PixelFormat))
            throw new InvalidOperationException($"FFV1 w tym buildzie nie deklaruje formatu {plan.Request.Source.Video.PixelFormat}. Nie wykonano niejawnej konwersji.");
    }
}
