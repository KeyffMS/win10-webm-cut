using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Win10WebmCut;

public static class MediaProbe
{
    public static async Task<VideoInfo> ReadAsync(string input, CancellationToken token)
    {
        string path = Path.GetFullPath(input);
        FileRevision before = FileRevision.Read(path);
        string json = await ToolProcess.TextAsync("ffprobe",
            ["-v", "error", "-show_format", "-show_streams", "-show_chapters", "-of", "json", "-i", path],
            token, ToolProcess.ProbeTimeout).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (FileRevision.Read(path) != before)
            throw new IOException("Plik zmienił się podczas odczytu. Otwórz go ponownie po zakończeniu zapisu.");
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("streams", out JsonElement streams) || streams.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("FFprobe nie zwrócił listy strumieni.");
        var parsed = new List<StreamInfo>();
        double videoDuration = 0;
        foreach (JsonElement stream in streams.EnumerateArray())
        {
            int rotation = 0;
            bool supported = true, matrixFound = false, hdr = false;
            if (stream.TryGetProperty("side_data_list", out JsonElement sideData) && sideData.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement side in sideData.EnumerateArray())
                {
                    string type = Text(side, "side_data_type");
                    if (type.Contains("DOVI", StringComparison.OrdinalIgnoreCase) ||
                        type.Contains("Mastering display", StringComparison.OrdinalIgnoreCase) ||
                        type.Contains("Content light", StringComparison.OrdinalIgnoreCase) ||
                        type.Contains("HDR", StringComparison.OrdinalIgnoreCase)) hdr = true;
                    if (!side.TryGetProperty("rotation", out _)) continue;
                    matrixFound = true;
                    double degrees = Number(side, "rotation", double.NaN);
                    (rotation, supported) = QuarterRotation(-degrees); // display matrix angle is counter-clockwise.
                    string matrix = Text(side, "displaymatrix");
                    if (matrix.Length > 0 && !IsOrthogonalMatrix(matrix)) supported = false;
                }
            }
            if (!matrixFound && stream.TryGetProperty("tags", out JsonElement tags) && tags.TryGetProperty("rotate", out _))
                (rotation, supported) = QuarterRotation(Number(tags, "rotate", double.NaN));
            string sarText = Text(stream, "sample_aspect_ratio");
            double sar = string.IsNullOrEmpty(sarText) || sarText is "N/A" or "0:1" ? 1 : Ratio(sarText, ':');
            if (!double.IsFinite(sar) || sar <= 0) supported = false;
            double fps = Ratio(Text(stream, "avg_frame_rate"), '/');
            if (!double.IsFinite(fps) || fps <= 0) fps = Ratio(Text(stream, "r_frame_rate"), '/');
            if (!double.IsFinite(fps) || fps <= 0 || fps > 1000) fps = 0;
            bool attached = stream.TryGetProperty("disposition", out JsonElement disposition) && Number(disposition, "attached_pic") == 1;
            string transfer = Text(stream, "color_transfer");
            hdr |= transfer is "smpte2084" or "arib-std-b67";
            string typeName = Text(stream, "codec_type");
            parsed.Add(new StreamInfo((int)Number(stream, "index", -1), typeName, Text(stream, "codec_name"),
                (int)Number(stream, "width"), (int)Number(stream, "height"), Text(stream, "pix_fmt"),
                fps, sar, rotation, supported, Text(stream, "sample_fmt"), (int)Number(stream, "channels"),
                (int)Number(stream, "sample_rate"), transfer, Text(stream, "color_primaries"),
                Text(stream, "color_space"), Text(stream, "color_range"), hdr, attached));
            if (typeName == "video" && !attached && videoDuration <= 0)
            {
                videoDuration = Number(stream, "duration");
                if (videoDuration <= 0 && stream.TryGetProperty("tags", out JsonElement durationTags))
                {
                    string text = Text(durationTags, "DURATION");
                    string[] parts = text.Split(':');
                    if (parts.Length == 3 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double h) &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double m) &&
                        double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                        videoDuration = h * 3600 + m * 60 + s;
                }
            }
        }
        root.TryGetProperty("format", out JsonElement format);
        double duration = Number(format, "duration", videoDuration);
        if (!double.IsFinite(duration) || duration <= 0) duration = videoDuration;
        if (!double.IsFinite(duration) || duration <= 0 || duration > TimeSpan.MaxValue.TotalSeconds / 2)
            throw new InvalidDataException("Brak skończonej długości nagrania. Zakończ zapis pliku przed edycją.");
        int chapters = root.TryGetProperty("chapters", out JsonElement ch) && ch.ValueKind == JsonValueKind.Array ? ch.GetArrayLength() : 0;
        var info = new VideoInfo(path, before, duration, parsed, chapters, Text(format, "format_name"));
        ExportValidation.SourceDimensions(info.Width, info.Height);
        if (info.Streams.Any(s => s.Index < 0)) throw new InvalidDataException("Nieprawidłowy indeks strumienia.");
        return info;
    }

    private static (int Angle, bool Supported) QuarterRotation(double degrees)
    {
        if (!double.IsFinite(degrees)) return (0, false);
        double quarter = Math.Round(degrees / 90);
        if (Math.Abs(degrees - quarter * 90) > 0.01 || Math.Abs(quarter) > 1000) return (0, false);
        return ((((int)quarter % 4 + 4) % 4) * 90, true);
    }

    private static bool IsOrthogonalMatrix(string text)
    {
        var values = new List<long>();
        foreach (string line in text.Split('\n'))
        {
            int colon = line.IndexOf(':');
            if (colon < 0) continue;
            foreach (string number in line[(colon + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)) values.Add(value);
        }
        if (values.Count != 9 || values.Any(v => v < -1073741824L || v > 1073741824L)) return false;
        long a = values[0], b = values[1], c = values[3], d = values[4];
        // Reject reflections, shear, translation and perspective instead of guessing.
        return values[2] == 0 && values[5] == 0 && values[6] == 0 && values[7] == 0 && values[8] == 1073741824 &&
            (Math.Abs(a) + Math.Abs(b) == 65536) && (Math.Abs(c) + Math.Abs(d) == 65536) &&
            a * c + b * d == 0 && a * d - b * c == 4294967296L;
    }

    private static string Text(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out JsonElement value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }
    private static double Number(JsonElement element, string name, double fallback = 0) =>
        double.TryParse(Text(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)
            ? value : fallback;
    private static double Ratio(string value, char separator)
    {
        string[] parts = value.Split(separator);
        return parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double n) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d != 0 ? n / d : 0;
    }
}
