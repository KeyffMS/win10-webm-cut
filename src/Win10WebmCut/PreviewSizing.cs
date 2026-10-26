namespace Win10WebmCut;

public static class PreviewSizing
{
    public const int MaxWidth = 960;
    public const int MaxHeight = 540;
    public const int MaxPixels = MaxWidth * MaxHeight;
    public const int MaxBytes = MaxPixels * 4;

    public static PixelSize For(VideoGeometry geometry, PreviewViewport viewport)
    {
        double availableWidth = FinitePositive(viewport.WidthDip * viewport.DpiX, MaxWidth);
        double availableHeight = FinitePositive(viewport.HeightDip * viewport.DpiY, MaxHeight);
        double displayWidth = geometry.Width * geometry.SampleAspect;
        double displayHeight = geometry.Height;
        double widthLimit = Math.Max(1, Math.Min(MaxWidth, availableWidth));
        double heightLimit = Math.Max(1, Math.Min(MaxHeight, availableHeight));
        double scale = Math.Min(1, Math.Min(widthLimit / displayWidth, heightLimit / displayHeight));
        scale = Math.Min(scale, Math.Sqrt(MaxPixels / (displayWidth * displayHeight)));
        var result = new PixelSize(Math.Max(1, (int)Math.Floor(displayWidth * scale)),
            Math.Max(1, (int)Math.Floor(displayHeight * scale)));
        Validate(result);
        return result;
    }

    private static double FinitePositive(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;

    public static void Validate(PixelSize size)
    {
        if (size.Width < 1 || size.Height < 1 || size.Width > MaxWidth || size.Height > MaxHeight ||
            (long)size.Width * size.Height > MaxPixels || (long)size.Width * size.Height * 4 > MaxBytes)
            throw new ArgumentOutOfRangeException(nameof(size), "Rozmiar podglądu przekracza budżet.");
    }
}
