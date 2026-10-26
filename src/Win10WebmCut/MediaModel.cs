using System.IO;

namespace Win10WebmCut;

public sealed record StreamInfo(
    int Index, string Type, string Codec, int Width, int Height, string PixelFormat,
    double Fps, double SampleAspect, int ClockwiseRotation, bool GeometrySupported,
    string SampleFormat, int Channels, int SampleRate, string ColorTransfer,
    string ColorPrimaries, string ColorSpace, string ColorRange, bool Hdr,
    bool AttachedPicture);

/// <summary>A copied, read-only probe result tied to one local file revision.</summary>
public sealed class VideoInfo
{
    public string FullPath { get; }
    public FileRevision Stamp { get; }
    public double Duration { get; }
    public IReadOnlyList<StreamInfo> Streams { get; }
    public StreamInfo Video { get; }
    public int ChapterCount { get; }
    public string Container { get; }
    public int Width => Video.Width;
    public int Height => Video.Height;
    public double Fps => Video.Fps;

    public VideoInfo(string fullPath, FileRevision stamp, double duration,
        IEnumerable<StreamInfo> streams, int chapterCount, string container)
    {
        FullPath = Path.GetFullPath(fullPath);
        Stamp = stamp;
        Duration = duration;
        Streams = Array.AsReadOnly(streams.ToArray());
        Video = Streams.FirstOrDefault(s => s.Type == "video" && !s.AttachedPicture)
            ?? throw new InvalidDataException("Nie znaleziono strumienia wideo (poza okładką).");
        ChapterCount = chapterCount;
        Container = container;
    }
}

public sealed record CropArea(int X, int Y, int Width, int Height);
public sealed record ExportRequest(VideoInfo Source, double Start, double End,
    CropArea? Crop, ExportProfile Profile);
public enum EditorPhase { Empty, Loading, Ready, Playing, Exporting, Closing }
public enum EditingMode { Trim, Crop }
public enum ExportOutcome { Saved, Cancelled }
public readonly record struct PixelSize(int Width, int Height)
{
    public int Stride => checked(Width * 4);
    public int ByteCount => checked(Stride * Height);
}
public readonly record struct PreviewViewport(double WidthDip, double HeightDip, double DpiX, double DpiY);

/// <summary>The byte array is transferred to the consumer and never reused by the producer.</summary>
public sealed record PreviewFrameData(PixelSize Size, byte[] Pixels, double SourceSeconds);
public sealed record PreviewRequest(VideoInfo Source, double Position, bool Playback, PixelSize Size);
