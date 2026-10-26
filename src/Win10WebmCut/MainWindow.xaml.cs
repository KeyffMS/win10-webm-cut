using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Win10WebmCut;

/// <summary>View adapter: dialogs, controls and pointer gestures; no process or file cleanup ownership.</summary>
public partial class MainWindow : Window
{
    private readonly EditorController _editor;
    private bool _updating;
    private bool _scrubbing;
    private bool _cropDragging;
    private bool _allowClose;
    private bool _closeInProgress;
    private double _scrubOrigin;
    private Point _cropAnchor;
    private ViewRect? _rubberBand;
    private string? _gestureSource;
    private WriteableBitmap? _bitmap;

    public MainWindow()
    {
        InitializeComponent();
        _editor = new EditorController(Dispatcher);
        _editor.Changed += Refresh;
        _editor.Error += (title, detail) =>
        {
            if (!_editor.IsClosing) DarkDialog.Show(this, title, detail, DarkDialog.Kind.Error);
        };
        _editor.FrameReady += frame =>
        {
            if (_bitmap is null || _bitmap.PixelWidth != frame.Size.Width || _bitmap.PixelHeight != frame.Size.Height)
                _bitmap = new WriteableBitmap(frame.Size.Width, frame.Size.Height, 96, 96, PixelFormats.Bgra32, null);
            _bitmap.WritePixels(new Int32Rect(0, 0, frame.Size.Width, frame.Size.Height), frame.Pixels, frame.Size.Stride, 0);
            PreviewFrame.Source = _bitmap;
        };
        ProfileBox.ItemsSource = ExportProfiles.All;
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        Loaded += (_, _) =>
        {
            Width = Math.Max(MinWidth, Math.Min(Width, SystemParameters.WorkArea.Width - 24));
            Height = Math.Max(MinHeight, Math.Min(Height, SystemParameters.WorkArea.Height - 24));
            UpdateViewport(); Refresh(); OpenButton.Focus();
        };
        Refresh();
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    private void EnableDarkTitleBar()
    {
        int enabled = 1;
        nint handle = new WindowInteropHelper(this).Handle;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }

    private void Refresh()
    {
        if (_editor is null || _updating) return;
        _updating = true;
        try
        {
            VideoInfo? source = _editor.Source;
            if ((_scrubbing || _cropDragging) && (!_editor.CanEdit || source?.FullPath != _gestureSource)) CancelGestures();
            OpenButton.IsEnabled = _editor.CanOpen;
            PlayButton.IsEnabled = _editor.CanPreview;
            PlayButton.Content = _editor.Phase == EditorPhase.Playing ? "⏸ Pauza" : "▶ Odtwarzaj";
            MarkStartButton.IsEnabled = MarkEndButton.IsEnabled = _editor.CanMark;
            PositionSlider.IsEnabled = _editor.CanPreview;
            PositionSlider.Maximum = source?.Duration ?? 1;
            if (!_scrubbing) PositionSlider.Value = _editor.Position.Pending ? _editor.Position.Requested : _editor.Position.Presented ?? 0;
            StartBox.IsEnabled = EndBox.IsEnabled = _editor.CanEdit;
            if (StartBox.Text != _editor.StartText) StartBox.Text = _editor.StartText;
            if (EndBox.Text != _editor.EndText) EndBox.Text = _editor.EndText;
            FileLabel.Text = source is null ? "Nie wybrano pliku" : $"{Path.GetFileName(source.FullPath)}  ({source.Width}×{source.Height})";
            FileLabel.ToolTip = source?.FullPath;
            string imageTime = _editor.Position.Presented is { } shown ? TimePolicy.Timecode(shown) : "—";
            TimeLabel.Text = source is null ? "— / —" : $"obraz: {imageTime} / {TimePolicy.Timecode(source.Duration)}";
            FrameLabel.Text = source is not null && _editor.Position.Presented is { } current
                ? TimePolicy.FrameLabel(current, source.Duration, source.Fps) : "klatka: —";
            WantedLabel.Text = _editor.Position.Pending ? "Żądano: " + TimePolicy.Timecode(_editor.Position.Requested) : "";
            StartFrameLabel.Text = source is not null && TimePolicy.TrySeconds(_editor.StartText, out double start) && start >= 0 && start <= source.Duration
                ? TimePolicy.FrameLabel(start, source.Duration, source.Fps) : "granica początku: —";
            EndFrameLabel.Text = source is not null && TimePolicy.TrySeconds(_editor.EndText, out double end) && end >= 0 && end <= source.Duration + TimePolicy.EndTolerance
                ? TimePolicy.FrameLabel(Math.Min(end, source.Duration), source.Duration, source.Fps, endBoundary: true) : "granica końca: —";
            bool cropMode = _editor.Mode == EditingMode.Crop;
            TrimMode.IsChecked = !cropMode; CropMode.IsChecked = cropMode;
            TrimMode.IsEnabled = CropMode.IsEnabled = _editor.CanEdit || _editor.Phase == EditorPhase.Empty;
            CropSettings.Visibility = cropMode ? Visibility.Visible : Visibility.Collapsed;
            CropHint.Visibility = cropMode && _editor.CanPreview ? Visibility.Visible : Visibility.Collapsed;
            PreviewHost.Cursor = cropMode ? Cursors.Cross : Cursors.Arrow;
            CropEditor? crop = _editor.Crop;
            CropLabel.Text = crop?.Applied is { } area
                ? $"{area.Width}×{area.Height} @ {area.X},{area.Y}" + (cropMode ? "" : " — pomijany w trybie czasu") : "Cały obraz";
            CropX.IsEnabled = CropY.IsEnabled = CropW.IsEnabled = CropH.IsEnabled = _editor.CanEdit && crop is not null;
            if (crop is not null)
            {
                if (CropX.Text != crop.Draft.X) CropX.Text = crop.Draft.X;
                if (CropY.Text != crop.Draft.Y) CropY.Text = crop.Draft.Y;
                if (CropW.Text != crop.Draft.Width) CropW.Text = crop.Draft.Width;
                if (CropH.Text != crop.Draft.Height) CropH.Text = crop.Draft.Height;
            }
            else { CropX.Text = CropY.Text = CropW.Text = CropH.Text = ""; }
            ApplyCropButton.IsEnabled = DiscardCropButton.IsEnabled = _editor.CanEdit && crop?.IsDirty == true;
            ResetCropButton.IsEnabled = CentralCropButton.IsEnabled = _editor.CanEdit && crop is not null;
            CropStateLabel.Text = crop?.IsDirty == true
                ? "Niezastosowane zmiany. Ramka pokazuje zatwierdzony kadr; eksport jest zablokowany do Zastosuj/Odrzuć."
                : "X/Y/W/H: piksele po obrocie, przed korekcją SAR. X/Y mogą wynosić 0. Cały obraz usuwa crop.";
            CropStateLabel.Foreground = (Brush)FindResource(crop?.IsDirty == true ? "Warning" : "TextSecondary");
            ProfileBox.IsEnabled = _editor.CanEdit || _editor.Phase == EditorPhase.Empty;
            ProfileBox.SelectedItem = _editor.Profile;
            ProfileBox.ToolTip = _editor.Profile.Name;
            ExportButton.IsEnabled = _editor.CanExport;
            CancelButton.IsEnabled = _editor.Phase == EditorPhase.Exporting;
            BusyProgress.IsIndeterminate = _editor.Phase == EditorPhase.Exporting;
            BusyProgress.Visibility = BusyProgress.IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;
            PreviewNote.Text = _editor.PreviewMessage;
            PreviewNote.Visibility = string.IsNullOrEmpty(_editor.PreviewMessage) ? Visibility.Collapsed : Visibility.Visible;
            StatusLabel.Text = _editor.Status; StatusLabel.ToolTip = _editor.Status;
            DrawCrop();
        }
        finally { _updating = false; }
    }

    private void ShowError(string title, Exception ex)
    {
        AppLog.Write("ui-operation", ex.ToString());
        if (!_editor.IsClosing)
            DarkDialog.Show(this, title, ex.Message, ex is ArgumentException or InvalidOperationException ? DarkDialog.Kind.Warning : DarkDialog.Kind.Error);
        Refresh();
    }
    private void Safely(Action action, string title)
    {
        try { action(); }
        catch (Exception ex) { ShowError(title, ex); }
    }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!_editor.CanOpen) return;
        try
        {
            var dialog = new OpenFileDialog { Filter = "Wideo|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.ts;*.mts;*.m2ts;*.wmv;*.mpg|Wszystkie pliki|*.*", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            if (_editor.Crop?.IsDirty == true && !DarkDialog.Confirm(this, "Niezastosowany crop", "Otworzenie nowego pliku odrzuci niezastosowane pola X/Y/W/H. Kontynuować?")) return;
            CancelGestures(); _bitmap = null; PreviewFrame.Source = null;
            _editor.Open(dialog.FileName);
        }
        catch (Exception ex) { ShowError("Otwieranie", ex); }
    }
    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        try { await _editor.TogglePlaybackAsync(); }
        catch (Exception ex) { ShowError("Odtwarzanie", ex); }
    }
    private void MarkStart_Click(object sender, RoutedEventArgs e) => Safely(() => _editor.Mark(end: false), "Początek");
    private void MarkEnd_Click(object sender, RoutedEventArgs e) => Safely(() => _editor.Mark(end: true), "Koniec");
    private void Range_TextChanged(object sender, TextChangedEventArgs e)
    { if (!_updating && _editor is not null) _editor.Range(StartBox.Text, EndBox.Text); }
    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (_updating || _editor is null) return;
        CancelGestures();
        Safely(() => _editor.SelectMode(CropMode.IsChecked == true ? EditingMode.Crop : EditingMode.Trim), "Tryb edycji");
    }
    private void Profile_Changed(object sender, SelectionChangedEventArgs e)
    { if (!_updating && _editor is not null && ProfileBox.SelectedItem is ExportProfile profile) Safely(() => _editor.SelectProfile(profile), "Profil eksportu"); }
    private void CropField_Changed(object sender, TextChangedEventArgs e)
    { if (!_updating && _editor is not null) _editor.EditCrop(new(CropX.Text, CropY.Text, CropW.Text, CropH.Text)); }
    private void ApplyCrop_Click(object sender, RoutedEventArgs e) => Safely(_editor.ApplyCrop, "Crop");
    private void DiscardCrop_Click(object sender, RoutedEventArgs e) => Safely(_editor.DiscardCropDraft, "Crop");
    private void ResetCrop_Click(object sender, RoutedEventArgs e) => Safely(_editor.FullImage, "Crop");
    private void CentralCrop_Click(object sender, RoutedEventArgs e) => Safely(_editor.CentralCrop, "Crop");

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_editor is null || _editor.IsClosing) return;
        if (e.Key == Key.Escape && (_cropDragging || _scrubbing))
        {
            bool seekBack = _scrubbing;
            CancelGestures();
            if (seekBack) _editor.Seek(_scrubOrigin);
            e.Handled = true; return;
        }
        if (e.Key is not (Key.Left or Key.Right) || !_editor.CanPreview || _cropDragging || _scrubbing) return;
        if (Keyboard.FocusedElement is TextBoxBase or ComboBox or ComboBoxItem) return;
        ModifierKeys modifiers = Keyboard.Modifiers;
        if ((modifiers & (ModifierKeys.Alt | ModifierKeys.Windows)) != 0) return;
        e.Handled = true;
        Safely(() => _editor.Step(e.Key == Key.Right ? 1 : -1,
            (modifiers & ModifierKeys.Shift) != 0, (modifiers & ModifierKeys.Control) != 0), "Nawigacja");
    }
    private async void Slider_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_updating || !_editor.CanPreview) return;
        _scrubbing = true; _scrubOrigin = _editor.Position.NavigationOrigin; _gestureSource = _editor.Source?.FullPath;
        try { await _editor.PauseAsync(); }
        catch (Exception ex) { CancelGestures(); ShowError("Przewijanie", ex); }
    }
    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (_scrubbing && !_updating && _editor is not null) Safely(() => _editor.Seek(e.NewValue), "Przewijanie"); }
    private void FinishScrub()
    {
        if (!_scrubbing) return;
        _scrubbing = false;
        if (_gestureSource == _editor.Source?.FullPath) Safely(() => _editor.Seek(PositionSlider.Value), "Przewijanie");
        Refresh();
    }
    private void Slider_MouseUp(object sender, MouseButtonEventArgs e) => FinishScrub();
    private void Slider_LostMouseCapture(object sender, MouseEventArgs e) => FinishScrub();

    private void UpdateViewport()
    {
        if (_editor is null || PreviewHost is null) return;
        DpiScale dpi = VisualTreeHelper.GetDpi(PreviewHost);
        _editor.SetViewport(new(PreviewHost.ActualWidth, PreviewHost.ActualHeight, dpi.DpiScaleX, dpi.DpiScaleY));
    }
    private void PreviewHost_SizeChanged(object sender, SizeChangedEventArgs e)
    { UpdateViewport(); if (_editor is not null) { CancelCropGesture(); DrawCrop(); } }
    private bool Bounds(out ViewRect bounds)
    {
        bounds = default;
        if (_editor.Crop is null || _editor.DisplayedSize is not { } size) return false;
        bounds = VideoGeometry.ImageBounds(size, PreviewHost.ActualWidth, PreviewHost.ActualHeight);
        return bounds.Width > 0 && bounds.Height > 0;
    }
    private static Point ClampPoint(Point point, ViewRect bounds) =>
        new(Math.Clamp(point.X, bounds.X, bounds.Right), Math.Clamp(point.Y, bounds.Y, bounds.Bottom));
    private static ViewRect Between(Point a, Point b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private async void Crop_MouseDown(object sender, MouseButtonEventArgs e)
    {
        PreviewHost.Focus();
        if (_editor.Mode != EditingMode.Crop || !_editor.CanPreview || !Bounds(out ViewRect bounds)) return;
        try
        {
            if (_editor.Crop!.IsDirty) throw new InvalidOperationException("Zastosuj lub odrzuć zmiany pól przed rysowaniem nowego kadru.");
            Point point = e.GetPosition(PreviewHost);
            if (point.X < bounds.X || point.X > bounds.Right || point.Y < bounds.Y || point.Y > bounds.Bottom) return;
            Task pause = _editor.PauseAsync();
            ViewRect old = _editor.Crop.Geometry.ToView(_editor.Crop.Applied, bounds);
            _cropAnchor = point;
            const double hitRadius = 13;
            bool Near(double x, double y) => Math.Abs(point.X - x) <= hitRadius && Math.Abs(point.Y - y) <= hitRadius;
            if (Near(old.X, old.Y)) _cropAnchor = new Point(old.Right, old.Bottom);
            else if (Near(old.Right, old.Y)) _cropAnchor = new Point(old.X, old.Bottom);
            else if (Near(old.X, old.Bottom)) _cropAnchor = new Point(old.Right, old.Y);
            else if (Near(old.Right, old.Bottom)) _cropAnchor = new Point(old.X, old.Y);
            _cropDragging = true; _gestureSource = _editor.Source?.FullPath;
            _rubberBand = Between(_cropAnchor, point);
            PreviewHost.CaptureMouse(); DrawCrop(); e.Handled = true;
            await pause;
        }
        catch (Exception ex) { CancelCropGesture(); ShowError("Crop", ex); }
    }
    private void Crop_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_cropDragging) return;
        if (e.LeftButton != MouseButtonState.Pressed || !Bounds(out ViewRect bounds)) { CancelCropGesture(); return; }
        _rubberBand = Between(_cropAnchor, ClampPoint(e.GetPosition(PreviewHost), bounds));
        DrawCrop();
    }
    private void Crop_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_cropDragging || !Bounds(out ViewRect bounds)) { CancelCropGesture(); return; }
        ViewRect selection = Between(_cropAnchor, ClampPoint(e.GetPosition(PreviewHost), bounds));
        _cropDragging = false; _rubberBand = null; PreviewHost.ReleaseMouseCapture();
        if (selection.Width >= 4 && selection.Height >= 4 && _gestureSource == _editor.Source?.FullPath)
            Safely(() => _editor.SetCrop(_editor.Crop!.Geometry.FromView(selection, bounds)), "Crop");
        DrawCrop(); e.Handled = true;
    }
    private void Crop_LostMouseCapture(object sender, MouseEventArgs e) => CancelCropGesture();
    private void CancelCropGesture()
    {
        bool captured = _cropDragging;
        _cropDragging = false; _rubberBand = null;
        if (captured && PreviewHost.IsMouseCaptureWithin) PreviewHost.ReleaseMouseCapture();
        if (_editor is not null) DrawCrop();
    }
    private void CancelGestures()
    {
        _scrubbing = false;
        if (PositionSlider.IsMouseCaptureWithin) Mouse.Capture(null);
        CancelCropGesture();
    }
    private void DrawCrop()
    {
        if (_editor is null || CropRect is null) return;
        bool visible = _editor.Mode == EditingMode.Crop && _editor.Crop is not null && Bounds(out _);
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        CropRect.Visibility = HandleTL.Visibility = HandleTR.Visibility = HandleBL.Visibility = HandleBR.Visibility = visibility;
        if (!visible || !Bounds(out ViewRect bounds)) return;
        ViewRect rect = _rubberBand ?? _editor.Crop!.Geometry.ToView(_editor.Crop.Applied, bounds);
        Canvas.SetLeft(CropRect, rect.X); Canvas.SetTop(CropRect, rect.Y); CropRect.Width = rect.Width; CropRect.Height = rect.Height;
        void Handle(FrameworkElement handle, double x, double y) { Canvas.SetLeft(handle, x - 5); Canvas.SetTop(handle, y - 5); }
        Handle(HandleTL, rect.X, rect.Y); Handle(HandleTR, rect.Right, rect.Y);
        Handle(HandleBL, rect.X, rect.Bottom); Handle(HandleBR, rect.Right, rect.Bottom);
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ExportRequest snapshot = _editor.CreateExportSnapshot(); // Before either modal dialog.
            ExportPlan plan = ExportValidation.Plan(snapshot);
            if (!DarkDialog.Confirm(this, "Kontrakt eksportu", string.Join("\n\n", plan.Notices))) return;
            string extension = snapshot.Profile.OutputExtension(snapshot.Source.FullPath);
            var dialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(snapshot.Source.FullPath) + "_cut" + extension,
                InitialDirectory = Path.GetDirectoryName(snapshot.Source.FullPath), DefaultExt = extension,
                Filter = $"Wynik {extension}|*{extension}", AddExtension = true, CheckPathExists = true, OverwritePrompt = false
            };
            if (dialog.ShowDialog(this) != true) return;
            bool allowOverwrite = File.Exists(dialog.FileName);
            if (allowOverwrite && !DarkDialog.Confirm(this, "Zastąpić istniejący wynik?",
                "Poprzedni plik pozostanie do czasu ukończenia eksportu i zatwierdzenia nowego wyniku.\n\n" + dialog.FileName)) return;
            CancelGestures();
            ExportOutcome outcome = await _editor.ExportAsync(snapshot, dialog.FileName, allowOverwrite);
            if (outcome == ExportOutcome.Saved && !_editor.IsClosing)
                DarkDialog.Show(this, "Eksport zakończony", "Zapisano plik:\n" + dialog.FileName, DarkDialog.Kind.Success);
        }
        catch (Exception ex) { ShowError("Eksport", ex); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => _editor.CancelExport();
    private void Diagnostics_Click(object sender, RoutedEventArgs e) =>
        DarkDialog.Show(this, "Diagnostyka lokalna", AppLog.Summary() + "\n\nOstatni status:\n" + _editor.Status);
    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_closeInProgress) return;
        _closeInProgress = true;
        CancelGestures();
        try
        {
            await _editor.DisposeAsync();
            if (ToolProcess.IsBlocked)
                DarkDialog.Show(this, "Zamykanie procesu", "Nie potwierdzono zakończenia jednego z procesów narzędzi. Sprawdź PID w lokalnym logu. Nie uruchomiono kolejnych operacji.", DarkDialog.Kind.Warning);
        }
        catch (Exception ex) { AppLog.Write("close", ex.ToString()); }
        finally
        {
            PreviewFrame.Source = null; _bitmap = null;
            _allowClose = true; Close();
        }
    }
}
