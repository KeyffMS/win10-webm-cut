using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace Win10WebmCut;

/// <summary>BGRA frames paired with showinfo PTS. No independent UI playback clock.</summary>
public static class PreviewDecoder
{
    public const int PlaybackFramesPerSecond = 12;
    public static readonly TimeSpan MinimumSeekInterval = TimeSpan.FromMilliseconds(80);
    private const int TimestampQueueCapacity = 64;
    private static readonly Regex StampPattern = new(
        @"\bn:\s*(?<index>\d+)\s+pts:\s*\S+\s+pts_time:(?<time>[-+0-9.eE]+)",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private sealed record Stamp(long Index, double Time);

    public static async Task DecodeAsync(PreviewRequest request,
        Action<PreviewFrameData> onFrame, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        VideoInfo info = request.Source;
        VideoGeometry geometry = VideoGeometry.From(info);
        PreviewSizing.Validate(request.Size);
        double start = TimePolicy.DecodePosition(request.Position, info);
        var stamps = Channel.CreateBounded<Stamp>(new BoundedChannelOptions(TimestampQueueCapacity)
        { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
        var filters = geometry.Filters().ToList();
        if (request.Playback)
            filters.Add($"select='isnan(prev_selected_t)+gte(t-prev_selected_t,1/{PlaybackFramesPerSecond})'");
        filters.Add($"scale={request.Size.Width}:{request.Size.Height}:flags=fast_bilinear");
        filters.Add("setsar=1");
        filters.Add("format=bgra");
        filters.Add("showinfo=checksum=0");
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-loglevel", "info", "-copyts", "-start_at_zero",
            "-ss", TimePolicy.Number(start), "-noautorotate", "-i", info.FullPath,
            "-map", $"0:{info.Video.Index}", "-an", "-sn", "-dn",
            "-vf", string.Join(',', filters), "-threads:v", "1", "-c:v", "rawvideo",
            "-pix_fmt", "bgra", "-fps_mode", "passthrough"
        };
        if (!request.Playback) args.AddRange(["-frames:v", "1"]);
        args.AddRange(["-f", "rawvideo", "pipe:1"]);
        long frames = 0;
        await ToolProcess.RunAsync("ffmpeg", args, async (stream, readToken) =>
        {
            var clock = new Stopwatch();
            double firstTime = 0, previousTime = double.NegativeInfinity;
            while (true)
            {
                byte[] buffer = new byte[request.Size.ByteCount];
                int filled = 0;
                while (filled < buffer.Length)
                {
                    int count = await stream.ReadAsync(buffer.AsMemory(filled), readToken).ConfigureAwait(false);
                    if (count == 0) break;
                    filled += count;
                }
                if (filled == 0) break;
                if (filled != buffer.Length) throw new InvalidDataException("Niepełna klatka podglądu.");
                Stamp stamp;
                try { stamp = await stamps.Reader.ReadAsync(readToken).ConfigureAwait(false); }
                catch (ChannelClosedException ex)
                { throw new InvalidDataException("Brak czasu PTS dla obrazu podglądu.", ex); }
                if (stamp.Index != frames || stamp.Time < previousTime - TimePolicy.EndTolerance)
                    throw new InvalidDataException("Niespójna kolejność klatek/czasów podglądu.");
                if (stamp.Time < -TimePolicy.EndTolerance || stamp.Time > info.Duration + TimePolicy.EndTolerance)
                    throw new InvalidDataException("Czas klatki wykracza poza rozpoznany zakres nagrania.");
                double seconds = Math.Clamp(stamp.Time, 0, info.Duration);
                if (frames == 0) { firstTime = seconds; clock.Start(); }
                if (request.Playback)
                {
                    TimeSpan delay = TimeSpan.FromSeconds(seconds - firstTime) - clock.Elapsed;
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, readToken).ConfigureAwait(false);
                }
                readToken.ThrowIfCancellationRequested();
                // Ownership transfers here: buffer is never modified again.
                onFrame(new PreviewFrameData(request.Size, buffer, seconds));
                previousTime = seconds;
                frames++;
            }
        }, token, request.Playback ? null : ToolProcess.ProbeTimeout,
            errorEnd: () => stamps.Writer.TryComplete(), asyncErrorLine: async (line, readToken) =>
        {
            if (!line.Contains("showinfo", StringComparison.Ordinal)) return;
            Match match = StampPattern.Match(line);
            if (!match.Success) return; // Configuration and color metadata lines.
            if (!long.TryParse(match.Groups["index"].Value, out long index) ||
                !double.TryParse(match.Groups["time"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ||
                !double.IsFinite(seconds)) throw new InvalidDataException("Nieprawidłowy PTS z showinfo.");
            // Apply back-pressure instead of failing for tiny frames that fill stdout
            // slowly while stderr produces timestamps ahead of playback.
            await stamps.Writer.WriteAsync(new Stamp(index, seconds), readToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (frames == 0) throw new InvalidDataException("FFmpeg nie zwrócił żadnej pełnej klatki.");
    }
}
