namespace Win10WebmCut;

public sealed record ExportPlan(ExportRequest Request, string Muxer, int Width, int Height,
    IReadOnlyList<string> Notices);

/// <summary>Domain validation shared by the UI snapshot and service boundary.</summary>
public static class ExportValidation
{
    public const int MaxSourceDimension = 32768;
    public const long MaxSourcePixels = 134_217_728;
    private static readonly HashSet<string> WebmInputFormats = new(StringComparer.Ordinal)
    { "yuv420p", "yuv422p", "yuv444p", "nv12", "nv21" };

    public static void SourceDimensions(int width, int height)
    {
        if (width < 1 || height < 1 || width > MaxSourceDimension || height > MaxSourceDimension ||
            (long)width * height > MaxSourcePixels)
            throw new ArgumentException("Nieprawidłowe lub zbyt duże wymiary źródła (limit 32768 na wymiar i 128 Mi pikseli).");
    }

    public static CropArea? NormalizeCrop(CropArea? crop, VideoGeometry geometry)
    {
        if (crop is null) return null;
        if (crop.X == 0 && crop.Y == 0 && crop.Width == geometry.Width && crop.Height == geometry.Height)
            return null; // Full image, including odd or one-pixel dimensions, is not a crop.
        if (crop.X < 0 || crop.Y < 0 || crop.Width < 2 || crop.Height < 2 ||
            crop.X + (long)crop.Width > geometry.Width || crop.Y + (long)crop.Height > geometry.Height ||
            ((crop.X | crop.Y | crop.Width | crop.Height) & 1) != 0)
            throw new ArgumentException("X i Y muszą być nieujemne i parzyste, W i H parzyste oraz co najmniej 2. Kadr musi mieścić się w obrazie.");
        return crop;
    }

    public static ExportRequest Snapshot(VideoInfo source, string startText, string endText,
        CropArea? crop, ExportProfile profile)
    {
        if (!TimePolicy.TrySeconds(startText, out double start) || !TimePolicy.TrySeconds(endText, out double end))
            throw new ArgumentException("Podaj sekundy jako liczby, np. 12.5 lub 12,5.");
        if (end > source.Duration && end <= source.Duration + TimePolicy.EndTolerance) end = source.Duration;
        var request = new ExportRequest(source, start, end, crop, ExportProfiles.Get(profile.Id));
        _ = Plan(request);
        return request;
    }

    public static ExportPlan Plan(ExportRequest request)
    {
        VideoInfo source = request.Source;
        ExportProfile profile = ExportProfiles.Get(request.Profile.Id);
        SourceDimensions(source.Width, source.Height);
        if (!double.IsFinite(source.Duration) || source.Duration <= 0 ||
            !double.IsFinite(request.Start) || !double.IsFinite(request.End) ||
            request.Start < 0 || request.End <= request.Start || request.End > source.Duration)
            throw new ArgumentException("Nieprawidłowy zakres [początek, koniec). Koniec nie może przekroczyć długości nagrania.");
        string muxer = profile.Muxer ?? Containers.ForExtension(profile.OutputExtension(source.FullPath));
        var notices = new List<string>();
        if (profile.Id == ExportProfileId.Copy)
        {
            if (request.Crop is not null) throw new ArgumentException("Crop wymaga ponownego kodowania.");
            StreamInfo? invalid = source.Streams.FirstOrDefault(s => !Containers.CanCopyStream(muxer, s));
            if (invalid is not null)
                throw new ArgumentException($"Copy: strumień {invalid.Index} ({invalid.Type}/{invalid.Codec}) nie ma potwierdzonej reguły dla {muxer}. Wybierz kodowanie; program nie usuwa go po cichu.");
            notices.Add("Bez rekompresji: cięcie jest zależne od klatek kluczowych. Zakres może różnić się od podglądu. Kopiowane są wszystkie obsługiwane strumienie.");
            return new(request, muxer, source.Width, source.Height, notices.AsReadOnly());
        }
        VideoGeometry geometry = VideoGeometry.From(source);
        CropArea? crop = NormalizeCrop(request.Crop, geometry);
        if (source.Video.Hdr || source.Video.ColorPrimaries == "bt2020")
            throw new ArgumentException("Kodowanie HDR/Dolby Vision/szerokiego gamutu nie jest obsługiwane. Profil Copy nie zmienia próbek.");
        if (profile.IsWebm && !WebmInputFormats.Contains(source.Video.PixelFormat))
            throw new ArgumentException("WebM w tej wersji przyjmuje SDR YUV 8-bit (420/422/444/NV12/NV21), bez alpha. Dla RGB/innej głębi wybierz FFV1; konwersja nie będzie wykonana po cichu.");
        if (profile.Id == ExportProfileId.Ffv1 && (geometry.Rotation is 90 or 270) &&
            (source.Video.PixelFormat.Contains("422", StringComparison.Ordinal) ||
             source.Video.PixelFormat.Contains("440", StringComparison.Ordinal) ||
             source.Video.PixelFormat.Contains("411", StringComparison.Ordinal)))
            throw new ArgumentException("Bezstratny obrót o 90/270° z asymetryczną siatką chromy nie jest obsługiwany przez ten kontrakt. Użyj WebM albo Copy.");
        int width = crop?.Width ?? geometry.Width;
        int height = crop?.Height ?? geometry.Height;
        if (profile.IsWebm)
        {
            if (profile.ScaleDivisor == 1 && ((width | height) & 1) != 0)
                throw new ArgumentException("WebM 1:1 wymaga parzystych wymiarów. Wybierz parzysty kadr lub skalę 1/2 albo 1/4.");
            width = (width / profile.ScaleDivisor / 2) * 2;
            height = (height / profile.ScaleDivisor / 2) * 2;
            if (width < 2 || height < 2)
                throw new ArgumentException("Kadr jest za mały dla tej redukcji wymiarów. Nie wygenerowano zerowego rozmiaru filtra.");
        }
        foreach (StreamInfo audio in source.Streams.Where(s => s.Type == "audio"))
        {
            if (profile.Id == ExportProfileId.Ffv1 && !Containers.CanCopyAudioToMkv(audio.Codec))
                throw new ArgumentException($"FFV1/MKV: kopiowanie audio {audio.Codec} nie jest objęte kontraktem.");
            if (profile.IsWebm && (audio.Channels is < 1 or > 8 || audio.SampleRate <= 0))
                throw new ArgumentException("WebM/Opus obsługuje w tym profilu 1–8 rozpoznanych kanałów audio.");
        }
        notices.Add(profile.IsWebm
            ? "WebM jest stratny: VP9 CRF 32, YUV420P 8-bit; audio Opus 96 kb/s na ścieżkę, 48 kHz. Skala dotyczy wymiarów po cropie, nie wielkości pliku."
            : "FFV1: bezstratne kodowanie obsługiwanych próbek obrazu po obrocie/cropie, bez skalowania. Audio jest kopiowane (nie konwertowane do FLAC). Cały plik nie będzie identyczny z wejściem.");
        notices.Add("Kodowanie zachowuje pierwsze wideo bez okładki oraz wszystkie obsługiwane ścieżki audio; napisy, dodatkowe wideo, załączniki, rozdziały i dowolne tagi są pomijane. Znane metadane kolorów są przekazywane jawnie.");
        if (source.Video.ColorTransfer is "unknown" or "unspecified" or "")
            notices.Add("Źródło nie określa transferu kolorów. Program nie nadaje mu domyślnego profilu HDR/SDR ani nie gwarantuje interpretacji barw przez odtwarzacze.");
        return new(request with { Crop = crop, Profile = profile }, muxer, width, height, notices.AsReadOnly());
    }
}
