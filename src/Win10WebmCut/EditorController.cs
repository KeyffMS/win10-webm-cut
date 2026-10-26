using System.Windows.Threading;

namespace Win10WebmCut;

/// <summary>UI-thread state owner. Workers can only enqueue generation-checked results.</summary>
public sealed class EditorController : IAsyncDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly LatestOperation<string> _loader;
    private readonly LatestOperation<PreviewRequest> _preview;
    private readonly LatestFramePresenter _presenter;
    private PreviewViewport _viewport;
    private bool _previewPlayback;
    private CancellationTokenSource? _exportCancellation;
    private Task? _exportTask;
    private Task? _closeTask;
    private volatile bool _closing;

    public event Action? Changed;
    public event Action<PreviewFrameData>? FrameReady;
    public event Action<string, string>? Error;
    public EditorPhase Phase { get; private set; } = EditorPhase.Empty;
    public EditingMode Mode { get; private set; }
    public VideoInfo? Source { get; private set; }
    public CropEditor? Crop { get; private set; }
    public Timeline Position { get; } = new();
    public PixelSize? DisplayedSize { get; private set; }
    public string StartText { get; private set; } = "0";
    public string EndText { get; private set; } = "0";
    public ExportProfile Profile { get; private set; } = ExportProfiles.Get(ExportProfileId.Copy);
    public string Status { get; private set; } = "Otwórz lokalne wideo.";
    public string PreviewMessage { get; private set; } = "Otwórz plik, aby rozpocząć";
    public bool IsClosing => _closing;
    public bool CanOpen => !_closing && Phase != EditorPhase.Exporting && !ToolProcess.IsBlocked;
    public bool CanEdit => !_closing && (Phase is EditorPhase.Ready or EditorPhase.Playing);
    public bool CanPreview => CanEdit && Source is not null && Crop is not null && !Source.Video.Hdr && !ToolProcess.IsBlocked;
    public bool CanMark => CanEdit && Position.Presented.HasValue && !Position.Pending;
    public bool CanExport => CanEdit && Source is not null && Crop?.IsDirty != true && !ToolProcess.IsBlocked;

    public EditorController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _presenter = new LatestFramePresenter(dispatcher, generation => _preview.IsCurrent(generation), (generation, frame) =>
        {
            if (_closing || Source is null) return;
            // Image and presentation time are accepted in one dispatcher operation.
            FrameReady?.Invoke(frame);
            DisplayedSize = frame.Size;
            Position.Present(frame.SourceSeconds, _preview.IsLatest(generation));
            PreviewMessage = "";
            Notify();
        });
        _preview = new LatestOperation<PreviewRequest>(
            (request, generation, token) => PreviewDecoder.DecodeAsync(request,
                frame => _presenter.Publish(generation, frame), token),
            (generation, ex) => Post(() =>
            {
                if (!_preview.IsCurrent(generation) || !_preview.IsLatest(generation)) return;
                Phase = Source is null ? EditorPhase.Empty : EditorPhase.Ready;
                Position.Failed();
                PreviewMessage = "Błąd podglądu — szczegóły w diagnostyce";
                Status = ex.Message;
                Notify();
            }),
            generation => Post(() =>
            {
                if (!_preview.IsCurrent(generation)) return;
                if (Phase == EditorPhase.Playing) Phase = EditorPhase.Ready;
                Notify();
            }), PreviewDecoder.MinimumSeekInterval);
        _loader = new LatestOperation<string>(async (path, generation, token) =>
        {
            await _preview.StopAsync().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            VideoInfo info = await FfmpegService.ProbeAsync(path, token).ConfigureAwait(false);
            Post(() =>
            {
                if (!_loader.IsCurrent(generation)) return;
                Source = info;
                StartText = "0"; EndText = TimePolicy.Number(info.Duration);
                Position.Reset();
                Phase = EditorPhase.Ready;
                try { Crop = new CropEditor(VideoGeometry.From(info)); }
                catch (ArgumentException ex) { Crop = null; PreviewMessage = ex.Message; }
                Status = "Wczytano plik. Zakres eksportu jest półotwarty: [początek, koniec).";
                if (info.Video.Hdr) PreviewMessage = "Podgląd HDR nie wykonuje tone mappingu i jest wyłączony. Dostępny jest Copy.";
                if (CanPreview) RequestPreview(0, playback: false);
                Notify();
            });
        }, (generation, ex) => Post(() =>
        {
            if (!_loader.IsCurrent(generation)) return;
            Phase = EditorPhase.Empty;
            PreviewMessage = "Nie udało się otworzyć pliku";
            Status = ex.Message;
            Notify();
            Error?.Invoke("Błąd otwierania", ex.ToString());
        }));
    }

    private void VerifyAccess() => _dispatcher.VerifyAccess();
    private void Notify() => Changed?.Invoke();
    private void Post(Action action)
    {
        if (_closing || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
        try { _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (!_closing) action(); })); }
        catch (InvalidOperationException) { /* Dispatcher is shutting down; no UI access remains. */ }
    }

    public void Open(string path)
    {
        VerifyAccess();
        if (!CanOpen) return;
        Phase = EditorPhase.Loading;
        Source = null; Crop = null; DisplayedSize = null;
        StartText = EndText = "0";
        Position.Reset();
        _presenter.Clear();
        PreviewMessage = "Odczyt metadanych…";
        Status = "Otwieranie — nowszy wybór zastąpi starszy.";
        _loader.RequestLatest(path);
        Notify();
    }

    public void SetViewport(PreviewViewport viewport) { VerifyAccess(); _viewport = viewport; }
    // Resizing only changes the next decode size. Existing images are scaled by WPF.
    private void RequestPreview(double seconds, bool playback)
    {
        if (Source is null || Crop is null || _closing) return;
        _presenter.Clear();
        Position.Request(Math.Clamp(seconds, 0, Source.Duration));
        PixelSize size = PreviewSizing.For(Crop.Geometry, _viewport);
        bool interrupt = playback || _previewPlayback;
        _previewPlayback = playback;
        _preview.RequestLatest(new PreviewRequest(Source, Position.Requested, playback, size), interrupt);
    }

    public void Seek(double seconds)
    {
        VerifyAccess();
        if (!CanPreview || !double.IsFinite(seconds)) return;
        Phase = EditorPhase.Ready; // Navigation pauses playback and preserves last shown image.
        RequestPreview(seconds, playback: false);
        Notify();
    }
    public void Step(int direction, bool shift, bool control)
    {
        VerifyAccess();
        if (!CanPreview || Source is null) return;
        Seek(Position.NavigationOrigin + Math.Sign(direction) * TimePolicy.Step(Source.Fps, shift, control));
    }
    public async Task PauseAsync()
    {
        VerifyAccess();
        if (Phase != EditorPhase.Playing) return;
        Phase = EditorPhase.Ready;
        _presenter.Clear();
        Task stop = _preview.StopAsync();
        Position.Pause();
        Notify();
        await stop;
    }
    public async Task TogglePlaybackAsync()
    {
        VerifyAccess();
        if (!CanPreview || Source is null) return;
        if (Phase == EditorPhase.Playing) { await PauseAsync(); return; }
        double start = Position.Presented ?? Position.Requested;
        if (start >= TimePolicy.DecodePosition(Source.Duration, Source)) start = 0;
        Phase = EditorPhase.Playing;
        RequestPreview(start, playback: true);
        Notify();
    }

    public void Range(string start, string end)
    { VerifyAccess(); if (CanEdit) { StartText = start; EndText = end; Notify(); } }
    public void Mark(bool end)
    {
        VerifyAccess();
        if (!CanMark) return;
        if (end) EndText = TimePolicy.Number(Position.Presented!.Value);
        else StartText = TimePolicy.Number(Position.Presented!.Value);
        Notify();
    }
    public void SelectMode(EditingMode mode)
    {
        VerifyAccess();
        if (!CanEdit && Phase != EditorPhase.Empty) return;
        RequireCleanCrop();
        Mode = mode;
        EnsureCropProfile();
        Notify();
    }
    public void SelectProfile(ExportProfile profile)
    {
        VerifyAccess();
        if (!CanEdit && Phase != EditorPhase.Empty) return;
        if (Mode == EditingMode.Crop && Crop?.Applied is not null && !profile.AllowsCrop)
            throw new ArgumentException("Wybrany kadr wymaga profilu z kodowaniem.");
        Profile = ExportProfiles.Get(profile.Id);
        Notify();
    }
    public void EditCrop(CropDraft draft) { VerifyAccess(); if (CanEdit && Crop is not null) { Crop.Edit(draft); Notify(); } }
    public void ApplyCrop() { VerifyAccess(); if (CanEdit && Crop is not null) { Crop.Apply(); EnsureCropProfile(); Notify(); } }
    public void DiscardCropDraft() { VerifyAccess(); if (CanEdit && Crop is not null) { Crop.Discard(); Notify(); } }
    public void SetCrop(CropArea? area)
    {
        VerifyAccess();
        if (!CanEdit || Crop is null) return;
        RequireCleanCrop();
        Crop.SetApplied(area); EnsureCropProfile(); Notify();
    }
    public void FullImage() { VerifyAccess(); if (CanEdit && Crop is not null) { Crop.FullImage(); Notify(); } }
    public void CentralCrop() { VerifyAccess(); if (CanEdit && Crop is not null) { RequireCleanCrop(); Crop.CentralArea(); EnsureCropProfile(); Notify(); } }
    private void EnsureCropProfile()
    {
        if (Mode == EditingMode.Crop && Crop?.Applied is not null && !Profile.AllowsCrop)
            Profile = ExportProfiles.Get(ExportProfileId.Ffv1);
    }
    private void RequireCleanCrop()
    {
        if (Crop?.IsDirty == true)
            throw new InvalidOperationException("Najpierw zastosuj lub odrzuć niezastosowane zmiany X/Y/W/H.");
    }

    public ExportRequest CreateExportSnapshot()
    {
        VerifyAccess();
        RequireCleanCrop();
        if (!CanExport || Source is null) throw new InvalidOperationException("Brak gotowego pliku do eksportu.");
        return ExportValidation.Snapshot(Source, StartText, EndText,
            Mode == EditingMode.Crop ? Crop?.Applied : null, Profile);
    }

    public async Task<ExportOutcome> ExportAsync(ExportRequest snapshot, string destination, bool allowOverwrite)
    {
        VerifyAccess();
        if (!CanExport) throw new InvalidOperationException("Eksport jest obecnie niedostępny.");
        Phase = EditorPhase.Exporting;
        _presenter.Clear();
        _exportCancellation = new CancellationTokenSource();
        CancellationToken token = _exportCancellation.Token;
        var progress = new Progress<string>(text =>
        {
            if (!_closing && Phase == EditorPhase.Exporting) { Status = text; Notify(); }
        });
        _exportTask = Task.Run(RunExportAsync); // File identity and disk operations stay off the UI thread.
        async Task RunExportAsync()
        {
            await _preview.StopAsync().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await FfmpegService.ExportAsync(snapshot, destination, allowOverwrite, progress, token).ConfigureAwait(false);
        }
        Notify();
        try
        {
            await _exportTask;
            Status = "Zapisano: " + destination;
            return ExportOutcome.Saved;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Status = "Eksport anulowany przed zatwierdzeniem. Poprzedni wynik pozostawiono bez zmian.";
            return ExportOutcome.Cancelled;
        }
        catch (Exception ex) { AppLog.Write("export", ex.ToString()); Status = "Eksport nieudany: " + ex.Message; throw; }
        finally
        {
            _exportCancellation.Dispose();
            _exportCancellation = null;
            _exportTask = null;
            Position.Pause();
            if (!_closing) { Phase = EditorPhase.Ready; Notify(); }
        }
    }
    public void CancelExport() { VerifyAccess(); _exportCancellation?.Cancel(); }

    public ValueTask DisposeAsync()
    {
        VerifyAccess();
        return new ValueTask(_closeTask ??= CloseCoreAsync());
    }
    private async Task CloseCoreAsync()
    {
        _closing = true; Phase = EditorPhase.Closing;
        _presenter.Dispose();
        _exportCancellation?.Cancel();
        Task closeLoad = _loader.DisposeAsync().AsTask();
        Task closePreview = _preview.DisposeAsync().AsTask();
        Task export = _exportTask ?? Task.CompletedTask;
        Notify();
        try { await Task.WhenAll(closeLoad, closePreview, export); }
        catch (Exception ex) { AppLog.Write("window-close", ex.ToString()); }
    }
}
