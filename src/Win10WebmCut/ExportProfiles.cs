using System.IO;

namespace Win10WebmCut;

public enum ExportProfileId { Copy, Ffv1, WebmFull, WebmHalf, WebmQuarter }

public sealed class ExportProfile
{
    public ExportProfileId Id { get; }
    public string Name { get; }
    public string? Extension { get; }
    public string? Muxer { get; }
    public int ScaleDivisor { get; }
    public bool AllowsCrop => Id != ExportProfileId.Copy;
    public bool IsWebm => Id is ExportProfileId.WebmFull or ExportProfileId.WebmHalf or ExportProfileId.WebmQuarter;
    public IReadOnlyList<string> CodecArguments { get; }
    public string VideoEncoder { get; }
    public string AudioEncoder { get; }

    internal ExportProfile(ExportProfileId id, string name, string? extension, string? muxer,
        int scaleDivisor, string videoEncoder, string audioEncoder, IEnumerable<string> codecArguments)
    {
        Id = id; Name = name; Extension = extension; Muxer = muxer;
        ScaleDivisor = scaleDivisor; VideoEncoder = videoEncoder; AudioEncoder = audioEncoder;
        CodecArguments = Array.AsReadOnly(codecArguments.ToArray());
    }
    public string OutputExtension(string input) => Extension ?? Path.GetExtension(input).ToLowerInvariant();
}

public static class ExportProfiles
{
    // Codec quality defaults have one owner, independent of ComboBox ordering.
    private static readonly string[] WebmArguments = ["-c:v", "libvpx-vp9", "-deadline", "good",
        "-cpu-used", "4", "-crf", "32", "-b:v", "0", "-row-mt", "1", "-pix_fmt", "yuv420p",
        "-c:a", "libopus", "-b:a", "96k", "-ar", "48000"];
    public static IReadOnlyList<ExportProfile> All { get; } = Array.AsReadOnly(new[]
    {
        new ExportProfile(ExportProfileId.Copy, "Bez rekompresji — oryginalny format", null, null,
            1, "copy", "copy", ["-c", "copy", "-avoid_negative_ts", "make_zero"]),
        new ExportProfile(ExportProfileId.Ffv1, "FFV1 / MKV — obraz bezstratny, audio copy", ".mkv", "matroska",
            1, "ffv1", "copy", ["-c:v", "ffv1", "-level", "3", "-c:a", "copy"]),
        Webm(ExportProfileId.WebmFull, "WebM VP9 — wymiary 1:1", 1),
        Webm(ExportProfileId.WebmHalf, "WebM VP9 — 1/2 szerokości i wysokości", 2),
        Webm(ExportProfileId.WebmQuarter, "WebM VP9 — 1/4 szerokości i wysokości", 4)
    });
    private static ExportProfile Webm(ExportProfileId id, string name, int scale) =>
        new(id, name, ".webm", "webm", scale, "libvpx-vp9", "libopus", WebmArguments);
    public static ExportProfile Get(ExportProfileId id) => All.FirstOrDefault(p => p.Id == id)
        ?? throw new ArgumentOutOfRangeException(nameof(id), "Nieznany profil eksportu.");
}

public static class Containers
{
    private static readonly Dictionary<string, string> Muxers = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mkv"] = "matroska", [".webm"] = "webm", [".mp4"] = "mp4", [".m4v"] = "mp4",
        [".mov"] = "mov", [".avi"] = "avi", [".ts"] = "mpegts", [".mts"] = "mpegts",
        [".m2ts"] = "mpegts", [".wmv"] = "asf", [".mpg"] = "mpeg", [".mpeg"] = "mpeg"
    };
    private static readonly HashSet<string> MatroskaAudio = new(StringComparer.Ordinal)
    { "aac", "opus", "vorbis", "mp3", "mp2", "flac", "alac", "ac3", "eac3", "dts", "truehd",
      "pcm_s16le", "pcm_s24le", "pcm_s32le", "pcm_f32le", "pcm_f64le", "pcm_s16be", "pcm_s24be" };
    public static string ForExtension(string extension) => Muxers.TryGetValue(extension, out string? muxer)
        ? muxer : throw new ArgumentException("Nieobsługiwane rozszerzenie wyjściowe. Użyj MKV lub WebM z kodowaniem.");
    public static bool CanCopyAudioToMkv(string codec) => MatroskaAudio.Contains(codec);

    public static bool CanCopyStream(string muxer, StreamInfo stream)
    {
        string codec = stream.Codec;
        if (muxer == "matroska") return stream.Type switch
        {
            "video" => codec is "h264" or "hevc" or "av1" or "vp8" or "vp9" or "mpeg4" or "mpeg2video" or "ffv1" or "mjpeg" or "png",
            "audio" => CanCopyAudioToMkv(codec),
            "subtitle" => codec is "subrip" or "ass" or "ssa" or "webvtt" or "hdmv_pgs_subtitle" or "dvd_subtitle",
            "attachment" => codec is "ttf" or "otf",
            _ => false
        };
        if (muxer == "webm") return stream.Type switch
        {
            "video" => codec is "vp8" or "vp9" or "av1",
            "audio" => codec is "opus" or "vorbis",
            "subtitle" => codec == "webvtt",
            _ => false
        };
        if (muxer is "mp4" or "mov") return stream.Type switch
        {
            "video" => codec is "h264" or "hevc" or "av1" or "mpeg4" or "vp9" or "mjpeg" or "png",
            "audio" => codec is "aac" or "mp3" or "ac3" or "eac3" or "alac" ||
                (muxer == "mov" && (codec is "pcm_s16le" or "pcm_s24le" or "pcm_s32le")),
            "subtitle" => codec == "mov_text",
            _ => false
        };
        if (muxer is "mpegts" or "mpeg") return stream.Type switch
        {
            "video" => codec is "mpeg1video" or "mpeg2video" || (muxer == "mpegts" && (codec is "h264" or "hevc")),
            "audio" => codec is "mp2" or "mp3" or "ac3" || (muxer == "mpegts" && (codec is "aac" or "eac3")),
            _ => false
        };
        if (muxer == "avi") return stream.Type switch
        { "video" => codec is "mpeg4" or "mjpeg" or "h264", "audio" => codec is "mp3" or "pcm_s16le", _ => false };
        if (muxer == "asf") return stream.Type switch
        { "video" => codec is "wmv1" or "wmv2" or "wmv3" or "vc1", "audio" => codec is "wmav1" or "wmav2" or "wmapro" or "wmalossless", _ => false };
        return false;
    }
}
