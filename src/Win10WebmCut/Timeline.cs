using System.Globalization;

namespace Win10WebmCut;

public static class TimePolicy
{
    public const double EndTolerance = 0.000001;
    public static string Number(double seconds) => seconds.ToString("0.######", CultureInfo.InvariantCulture);
    public static bool TrySeconds(string text, out double seconds) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out seconds) && double.IsFinite(seconds);

    public static string Timecode(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return "—";
        var time = TimeSpan.FromSeconds(seconds);
        return $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    public static double Step(double fps, bool shift, bool control)
    {
        if (control) return 1;
        if (!double.IsFinite(fps) || fps <= 0)
            throw new InvalidOperationException("Brak FPS: użyj Ctrl + strzałka (1 sekunda).");
        return (shift ? 10d : 1d) / fps;
    }

    public static string FrameLabel(double seconds, double duration, double fps, bool endBoundary = false)
    {
        if (!double.IsFinite(fps) || fps <= 0 || duration * fps > long.MaxValue / 2d)
            return "klatka: — (brak wiarygodnego FPS)";
        long total = Math.Max(1, (long)Math.Ceiling(duration * fps));
        if (endBoundary)
        {
            long boundary = Math.Clamp((long)Math.Ceiling(seconds * fps - 1e-7), 0, total);
            return $"granica końca ≈{boundary} (wyłączna)";
        }
        long index = Math.Clamp((long)Math.Floor(Math.Max(0, seconds) * fps + 1e-7), 0, total - 1);
        return $"klatka ≈{index} / ≈{total} · {fps:0.###} FPS · indeks od 0";
    }

    public static double DecodePosition(double requested, VideoInfo info)
    {
        // The EOF boundary is not a frame. Request a nearby frame instead;
        // the UI will show its returned PTS, never the requested EOF as an image time.
        double margin = info.Fps > 0 ? 1 / info.Fps : 0.1;
        return Math.Clamp(requested, 0, Math.Max(0, info.Duration - margin));
    }
}

/// <summary>Requested time is not presented time. Only an accepted image sets Presented.</summary>
public sealed class Timeline
{
    public double Requested { get; private set; }
    public double? Presented { get; private set; }
    public bool Pending { get; private set; }
    public void Reset() { Requested = 0; Presented = null; Pending = false; }
    public void Request(double seconds) { Requested = seconds; Pending = true; }
    public void Present(double seconds, bool completesLatestRequest)
    {
        Presented = seconds;
        if (completesLatestRequest) { Requested = seconds; Pending = false; }
        // An accepted in-flight still frame must not erase a newer pending seek target.
    }
    public void Pause() { Requested = Presented ?? Requested; Pending = false; }
    public void Failed() => Pending = false;
    public double NavigationOrigin => Pending ? Requested : Presented ?? Requested;
}
