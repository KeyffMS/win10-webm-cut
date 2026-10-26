using System.Globalization;

namespace Win10WebmCut;

public readonly record struct ViewRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>Crop coordinates are pixels AFTER explicit orthogonal rotation, BEFORE SAR display scaling.</summary>
public sealed record VideoGeometry(int Width, int Height, double SampleAspect, int Rotation)
{
    public static VideoGeometry From(VideoInfo source)
    {
        StreamInfo video = source.Video;
        ExportValidation.SourceDimensions(video.Width, video.Height);
        if (!video.GeometrySupported || video.ClockwiseRotation is not (0 or 90 or 180 or 270) ||
            !double.IsFinite(video.SampleAspect) || video.SampleAspect is < 0.01 or > 100)
            throw new ArgumentException("Nieobsługiwana geometria obrazu (odbicie, dowolny obrót, macierz lub SAR). Możliwy pozostaje eksport Copy bez cropu.");
        bool swap = video.ClockwiseRotation is 90 or 270;
        return new(swap ? video.Height : video.Width, swap ? video.Width : video.Height,
            swap ? 1 / video.SampleAspect : video.SampleAspect, video.ClockwiseRotation);
    }

    public IEnumerable<string> Filters()
    {
        if (Rotation == 90) yield return "transpose=clock";
        else if (Rotation == 180) { yield return "hflip"; yield return "vflip"; }
        else if (Rotation == 270) yield return "transpose=cclock";
        yield return "setsar=" + SampleAspect.ToString("0.############", CultureInfo.InvariantCulture) + ":max=100000";
    }

    public static ViewRect ImageBounds(PixelSize actualBitmap, double viewportWidth, double viewportHeight)
    {
        if (!double.IsFinite(viewportWidth) || !double.IsFinite(viewportHeight) ||
            viewportWidth <= 0 || viewportHeight <= 0 || actualBitmap.Width <= 0 || actualBitmap.Height <= 0)
            return default;
        double scale = Math.Min(viewportWidth / actualBitmap.Width, viewportHeight / actualBitmap.Height);
        double width = actualBitmap.Width * scale, height = actualBitmap.Height * scale;
        return new((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }

    public ViewRect ToView(CropArea? area, ViewRect bounds)
    {
        area ??= new CropArea(0, 0, Width, Height);
        return new(bounds.X + area.X * bounds.Width / Width, bounds.Y + area.Y * bounds.Height / Height,
            area.Width * bounds.Width / Width, area.Height * bounds.Height / Height);
    }

    public CropArea? FromView(ViewRect selection, ViewRect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentException("Brak geometrii podglądu.");
        int Align(double value, int limit) => Math.Clamp((int)Math.Floor(value), 0, limit) & ~1;
        int x = Align((selection.X - bounds.X) * Width / bounds.Width, Width);
        int y = Align((selection.Y - bounds.Y) * Height / bounds.Height, Height);
        int right = Align((selection.Right - bounds.X) * Width / bounds.Width, Width);
        int bottom = Align((selection.Bottom - bounds.Y) * Height / bounds.Height, Height);
        return ExportValidation.NormalizeCrop(new CropArea(x, y, right - x, bottom - y), this);
    }
}
