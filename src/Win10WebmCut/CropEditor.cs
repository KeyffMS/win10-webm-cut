using System.Globalization;

namespace Win10WebmCut;

public sealed record CropDraft(string X, string Y, string Width, string Height);

/// <summary>Only Applied enters an export snapshot; uncommitted text is visibly separate.</summary>
public sealed class CropEditor
{
    public VideoGeometry Geometry { get; }
    public CropArea? Applied { get; private set; }
    public CropDraft Draft { get; private set; }
    public bool IsDirty => Draft != Format(Applied);
    public CropEditor(VideoGeometry geometry) { Geometry = geometry; Draft = Format(null); }

    private CropDraft Format(CropArea? area)
    {
        area ??= new CropArea(0, 0, Geometry.Width, Geometry.Height);
        string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        return new(N(area.X), N(area.Y), N(area.Width), N(area.Height));
    }
    public void Edit(CropDraft value) => Draft = value;
    public void Apply()
    {
        bool Parse(string text, out int value) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        if (!Parse(Draft.X, out int x) || !Parse(Draft.Y, out int y) ||
            !Parse(Draft.Width, out int width) || !Parse(Draft.Height, out int height))
            throw new ArgumentException("X, Y, W i H muszą być liczbami całkowitymi.");
        SetApplied(new CropArea(x, y, width, height));
    }
    public void SetApplied(CropArea? area)
    {
        Applied = ExportValidation.NormalizeCrop(area, Geometry);
        Draft = Format(Applied);
    }
    public void Discard() => Draft = Format(Applied);
    public void FullImage() => SetApplied(null);
    public void CentralArea()
    {
        if (Geometry.Width < 4 || Geometry.Height < 4) { FullImage(); return; }
        int x = (Geometry.Width / 8) & ~1, y = (Geometry.Height / 8) & ~1;
        SetApplied(new CropArea(x, y, (Geometry.Width - 2 * x) & ~1, (Geometry.Height - 2 * y) & ~1));
    }
}
