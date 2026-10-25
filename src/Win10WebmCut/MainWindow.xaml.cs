using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Win10WebmCut;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private string? _input;
    private VideoInfo? _info;
    private bool _playing;
    private double _position;
    private double _playStart;
    private readonly Stopwatch _playWatch = new();
    private CancellationTokenSource? _previewCancellation;
    private bool _draggingSlider;
    private bool _selectingCrop;
    private Point _dragStart;
    private CropArea? _crop;
    private CancellationTokenSource? _exportCancellation;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        _timer.Tick += (_, _) =>
        {
            if (_info is null || _draggingSlider) return;
            if (_playing)
            {
                _position = Math.Clamp(_playStart + _playWatch.Elapsed.TotalSeconds, 0, _info.Duration);
                if (_position >= _info.Duration)
                {
                    PausePreview();
                    StartPreview();
                }
            }
            PositionSlider.Value = _position;
            UpdateTimeDisplay(_position);
        };
        _timer.Start();
        Loaded += (_, _) => UpdateCropDisplay();
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    private void EnableDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int enabled = 1;
        // Windows 10 19045 (20H1+) oraz starszy identyfikator atrybutu DWM.
        if (DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, 19, ref enabled, sizeof(int));
    }

    private static string FormatTime(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss");

    private static string Timecode(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss\.fff");

    private void UpdateTimeDisplay(double seconds)
    {
        if (_info is null) return;
        TimeLabel.Text = $"{Timecode(seconds)} / {Timecode(_info.Duration)}";
        if (_info.Fps > 0)
        {
            long index = Math.Max(0, (long)Math.Round(seconds * _info.Fps));
            long total = Math.Max(1, (long)Math.Ceiling(_info.Duration * _info.Fps));
            FrameLabel.Text = $"klatka: {Math.Min(index, total)} / {total}  ({_info.Fps:0.###} FPS)";
        }
        else FrameLabel.Text = "klatka: FPS nieznane";
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_info is null || e.Key is not (Key.Left or Key.Right)) return;
        // Nie przechwytuj strzałek podczas edycji pól liczbowych i list wyboru.
        if (Keyboard.FocusedElement is TextBox or ComboBox) return;
        if (_draggingSlider) return;
        if (_info.Fps <= 0) { StatusLabel.Text = "Nie można przewijać po klatkach: brak FPS w metadanych."; return; }
        int direction = e.Key == Key.Right ? 1 : -1;
        var modifiers = Keyboard.Modifiers;
        double step = (modifiers & ModifierKeys.Control) != 0 ? 1.0 :
                      (modifiers & ModifierKeys.Shift) != 0 ? 10.0 / _info.Fps : 1.0 / _info.Fps;
        PausePreview();
        _position = Math.Clamp(_position + direction * step, 0, _info.Duration);
        PositionSlider.Value = _position;
        UpdateTimeDisplay(_position);
        StartPreview();
        e.Handled = true;
    }

    private static string Number(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_exportCancellation is not null) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Wideo|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.ts;*.mts;*.wmv|Wszystkie pliki|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            PausePreview();
            CancelPreview();
            PreviewFrame.Source = null;
            _position = 0;
            _playStart = 0;
            PlayButton.Content = "▶ Odtwarzaj";
            _input = null;
            _info = null;
            _crop = null;
            CropHint.Visibility = Visibility.Collapsed;
            CropRect.Visibility = Visibility.Collapsed;
            PreviewNote.Text = "Wczytywanie…";
            PreviewNote.Visibility = Visibility.Visible;
            StatusLabel.Text = "Odczyt metadanych FFprobe…";
            var info = await FfmpegService.ProbeAsync(dlg.FileName, CancellationToken.None);
            _input = dlg.FileName;
            _info = info;
            FileLabel.Text = Path.GetFileName(_input) + $"  ({info.Width}×{info.Height})";
            FileLabel.ToolTip = _input;
            StartBox.Text = "0";
            EndBox.Text = Number(info.Duration);
            PositionSlider.Maximum = info.Duration;
            PositionSlider.Value = 0;
            UpdateTimeDisplay(0);
            if (CropMode.IsChecked == true) InitCrop();
            StartPreview();
            CropLabel.Text = "Cały obraz";
            UpdateCropDisplay();
            StatusLabel.Text = $"Wczytano: {FormatTime(info.Duration)}";
        }
        catch (Exception ex)
        {
            PreviewNote.Text = "Nie udało się otworzyć pliku";
            MessageBox.Show(this, ex.Message, "Błąd otwierania", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusLabel.Text = "Błąd";
        }
    }

    private void CancelPreview()
    {
        // Tylko wątek UI zarządza życiem CTS. Zadanie dekodujące nigdy go nie zwalnia.
        var previous = _previewCancellation;
        _previewCancellation = null;
        if (previous is not null)
        {
            previous.Cancel();
            previous.Dispose();
        }
    }

    private void PausePreview()
    {
        if (_playing && _info is not null)
            _position = Math.Clamp(_playStart + _playWatch.Elapsed.TotalSeconds, 0, _info.Duration);
        _playing = false;
        _playWatch.Stop();
        PlayButton.Content = "▶ Odtwarzaj";
    }

    private void StartPreview()
    {
        CancelPreview();
        if (_info is null || _input is null) return;
        var request = new CancellationTokenSource();
        _previewCancellation = request;
        CancellationToken token = request.Token; // Pobierz token PRZED uruchomieniem zadania.
        string path = _input;
        VideoInfo info = _info;
        double offset = Math.Clamp(_position, 0, Math.Max(0, info.Duration - 0.01));
        bool playing = _playing;
        _ = Task.Run(async () =>
        {
            try
            {
                await PreviewDecoder.DecodeAsync(path, info, offset, playing, bitmap =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (!ReferenceEquals(_previewCancellation, request) || token.IsCancellationRequested) return;
                        PreviewFrame.Source = bitmap;
                        PreviewNote.Visibility = Visibility.Collapsed;
                    });
                }, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Anulowanie podglądu podczas przewijania jest normalne.
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (!ReferenceEquals(_previewCancellation, request)) return;
                    PreviewNote.Text = "Błąd dekodowania podglądu przez FFmpeg";
                    PreviewNote.Visibility = Visibility.Visible;
                    StatusLabel.Text = ex.Message;
                });
            }
        });
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_info is null) return;
        if (_playing)
        {
            PausePreview();
            StartPreview();
        }
        else
        {
            if (_position >= _info.Duration - 0.01) _position = 0;
            _playing = true;
            _playStart = _position;
            _playWatch.Restart();
            PlayButton.Content = "⏸ Pauza";
            StartPreview();
        }
    }

    private void Slider_MouseDown(object sender, MouseButtonEventArgs e) => _draggingSlider = true;

    private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingSlider = false;
        if (_info is null) return;
        _position = PositionSlider.Value;
        if (_playing)
        {
            _playStart = _position;
            _playWatch.Restart();
        }
        StartPreview();
    }

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_info is not null && _draggingSlider)
            UpdateTimeDisplay(PositionSlider.Value);
    }

    private void MarkStart_Click(object sender, RoutedEventArgs e)
    {
        if (_info is not null) StartBox.Text = Number(_position);
    }

    private void MarkEnd_Click(object sender, RoutedEventArgs e)
    {
        if (_info is not null) EndBox.Text = Number(_position);
    }

    private bool TryGetVideoBounds(out Rect rect)
    {
        rect = Rect.Empty;
        if (_info is null || PreviewHost.ActualWidth <= 0 || PreviewHost.ActualHeight <= 0) return false;
        double scale = Math.Min(PreviewHost.ActualWidth / _info.Width, PreviewHost.ActualHeight / _info.Height);
        double width = _info.Width * scale;
        double height = _info.Height * scale;
        rect = new Rect((PreviewHost.ActualWidth - width) / 2,
                        (PreviewHost.ActualHeight - height) / 2, width, height);
        return true;
    }

    private static Point ClampPoint(Point p, Rect rect) =>
        new(Math.Clamp(p.X, rect.Left, rect.Right), Math.Clamp(p.Y, rect.Top, rect.Bottom));

    private void Crop_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (CropMode.IsChecked != true || !TryGetVideoBounds(out Rect bounds)) return;
        Point cursor = ClampPoint(e.GetPosition(PreviewHost), bounds);
        _dragStart = cursor;
        // Blisko narożnika istniejącej ramki: przeciągaj ten narożnik.
        if (_crop is not null && CropRect.Visibility == Visibility.Visible)
        {
            double left = Canvas.GetLeft(CropRect), top = Canvas.GetTop(CropRect);
            double right = left + CropRect.Width, bottom = top + CropRect.Height;
            const double hit = 13;
            if (Math.Abs(cursor.X - left) < hit && Math.Abs(cursor.Y - top) < hit)
                _dragStart = new Point(right, bottom);
            else if (Math.Abs(cursor.X - right) < hit && Math.Abs(cursor.Y - top) < hit)
                _dragStart = new Point(left, bottom);
            else if (Math.Abs(cursor.X - left) < hit && Math.Abs(cursor.Y - bottom) < hit)
                _dragStart = new Point(right, top);
            else if (Math.Abs(cursor.X - right) < hit && Math.Abs(cursor.Y - bottom) < hit)
                _dragStart = new Point(left, top);
        }
        _selectingCrop = true;
        PreviewHost.CaptureMouse();
        CropRect.Visibility = Visibility.Visible;
    }

    private void Crop_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_selectingCrop || !TryGetVideoBounds(out Rect bounds)) return;
        Point p = ClampPoint(e.GetPosition(PreviewHost), bounds);
        DrawRectangle(new Rect(_dragStart, p));
    }

    private void Crop_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selectingCrop || _info is null || !TryGetVideoBounds(out Rect bounds)) return;
        _selectingCrop = false;
        PreviewHost.ReleaseMouseCapture();
        Point p = ClampPoint(e.GetPosition(PreviewHost), bounds);
        Rect selection = new(_dragStart, p);
        if (selection.Width < 8 || selection.Height < 8) { ResetCrop(); return; }

        double sx = _info.Width / bounds.Width;
        double sy = _info.Height / bounds.Height;
        int x = Math.Clamp((int)Math.Floor((selection.Left - bounds.Left) * sx), 0, _info.Width - 2);
        int y = Math.Clamp((int)Math.Floor((selection.Top - bounds.Top) * sy), 0, _info.Height - 2);
        x -= x % 2;
        y -= y % 2;
        int w = Math.Min(_info.Width - x, (int)Math.Floor(selection.Width * sx));
        int h = Math.Min(_info.Height - y, (int)Math.Floor(selection.Height * sy));
        w -= w % 2;
        h -= h % 2;
        if (w < 2 || h < 2) { ResetCrop(); return; }
        SetCrop(new CropArea(x, y, w, h));
    }

    private void DrawRectangle(Rect rect)
    {
        Canvas.SetLeft(CropRect, rect.Left);
        Canvas.SetTop(CropRect, rect.Top);
        CropRect.Width = rect.Width;
        CropRect.Height = rect.Height;
        void handle(System.Windows.Shapes.Rectangle shape, double x, double y)
        {
            Canvas.SetLeft(shape, x - 5);
            Canvas.SetTop(shape, y - 5);
            shape.Visibility = Visibility.Visible;
        }
        handle(HandleTL, rect.Left, rect.Top);
        handle(HandleTR, rect.Right, rect.Top);
        handle(HandleBL, rect.Left, rect.Bottom);
        handle(HandleBR, rect.Right, rect.Bottom);
    }

    private void UpdateCropDisplay()
    {
        if (CropCanvas is null || CropRect is null) return;
        CropCanvas.Width = PreviewHost.ActualWidth;
        CropCanvas.Height = PreviewHost.ActualHeight;
        if (_crop is null || _info is null || CropMode.IsChecked != true ||
            !TryGetVideoBounds(out Rect bounds))
        {
            CropRect.Visibility = Visibility.Collapsed;
            HandleTL.Visibility = HandleTR.Visibility = HandleBL.Visibility = HandleBR.Visibility = Visibility.Collapsed;
            return;
        }
        var r = new Rect(bounds.Left + _crop.X * bounds.Width / _info.Width,
                         bounds.Top + _crop.Y * bounds.Height / _info.Height,
                         _crop.Width * bounds.Width / _info.Width,
                         _crop.Height * bounds.Height / _info.Height);
        DrawRectangle(r);
        CropRect.Visibility = Visibility.Visible;
    }

    private void ResetCrop_Click(object sender, RoutedEventArgs e) => ResetCrop();

    private void InitCrop()
    {
        if (_info is null || _crop is not null) return;
        int x = (_info.Width / 8) & ~1;
        int y = (_info.Height / 8) & ~1;
        int w = ((_info.Width - 2 * x) & ~1);
        int h = ((_info.Height - 2 * y) & ~1);
        SetCrop(new CropArea(x, y, w, h));
    }

    private void SetCrop(CropArea area)
    {
        _crop = area;
        CropLabel.Text = $"Crop: {area.Width}×{area.Height} @ {area.X},{area.Y}";
        CropX.Text = area.X.ToString(CultureInfo.InvariantCulture);
        CropY.Text = area.Y.ToString(CultureInfo.InvariantCulture);
        CropW.Text = area.Width.ToString(CultureInfo.InvariantCulture);
        CropH.Text = area.Height.ToString(CultureInfo.InvariantCulture);
        if (ProfileBox.SelectedIndex == 0) ProfileBox.SelectedIndex = 1;
        UpdateCropDisplay();
    }

    private void ApplyCrop_Click(object sender, RoutedEventArgs e)
    {
        if (_info is null) return;
        if (!int.TryParse(CropX.Text, out int x) || !int.TryParse(CropY.Text, out int y) ||
            !int.TryParse(CropW.Text, out int w) || !int.TryParse(CropH.Text, out int h) ||
            x < 0 || y < 0 || w < 2 || h < 2 || x + (long)w > _info.Width || y + (long)h > _info.Height ||
            (x | y | w | h) % 2 != 0)
        {
            MessageBox.Show(this, "X, Y, W, H muszą być parzyste, dodatnie i mieścić się w obrazie.",
                "Nieprawidłowy crop", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SetCrop(new CropArea(x, y, w, h));
    }

    private void ResetCrop()
    {
        _crop = null;
        CropRect.Visibility = Visibility.Collapsed;
        HandleTL.Visibility = HandleTR.Visibility = HandleBL.Visibility = HandleBR.Visibility = Visibility.Collapsed;
        CropLabel.Text = "Cały obraz";
        if (CropMode?.IsChecked == true) InitCrop();
    }

    private void PreviewHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCropDisplay();

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (CropSettings is null || CropHint is null || CropRect is null) return;
        bool cropMode = CropMode?.IsChecked == true;
        CropSettings.Visibility = cropMode ? Visibility.Visible : Visibility.Collapsed;
        CropHint.Visibility = cropMode ? Visibility.Visible : Visibility.Collapsed;
        if (cropMode) InitCrop();
        UpdateCropDisplay();
    }

    private void Profile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileBox is not null && CropMode?.IsChecked == true && ProfileBox.SelectedIndex == 0)
            ProfileBox.SelectedIndex = 1;
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_input is null || _info is null || _exportCancellation is not null) return;
        if (!double.TryParse(StartBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double start) ||
            !double.TryParse(EndBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double end) ||
            !double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start ||
            end > _info.Duration + 0.001)
        {
            MessageBox.Show(this, "Podaj poprawny zakres sekund (format: 12.5).", "Zakres", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var profile = (ExportProfile)ProfileBox.SelectedIndex;
        CropArea? crop = CropMode.IsChecked == true ? _crop : null;
        if (CropMode.IsChecked == true && crop is null)
        {
            MessageBox.Show(this, "Zaznacz crop myszą na podglądzie.", "Crop", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string extension = profile switch
        {
            ExportProfile.Ffv1 => ".mkv",
            ExportProfile.Copy => Path.GetExtension(_input),
            _ => ".webm"
        };
        if (string.IsNullOrWhiteSpace(extension)) extension = ".mkv";
        var dlg = new SaveFileDialog
        {
            FileName = Path.GetFileNameWithoutExtension(_input) + "_cut" + extension,
            DefaultExt = extension,
            Filter = profile == ExportProfile.Copy ? "Plik wideo|*" + extension :
                     profile == ExportProfile.Ffv1 ? "Matroska|*.mkv" : "WebM|*.webm",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dlg.ShowDialog(this) != true) return;

        _exportCancellation = new CancellationTokenSource();
        ExportButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        BusyProgress.Visibility = Visibility.Visible;
        PausePreview();
        StartPreview();
        var progress = new Progress<string>(line =>
        {
            if (line.Contains("time=") || line.Contains("Error", StringComparison.OrdinalIgnoreCase))
                StatusLabel.Text = line.Trim();
        });
        try
        {
            StatusLabel.Text = "Eksport trwa…";
            await FfmpegService.ExportAsync(_input, dlg.FileName, start, end, crop, profile, progress,
                _exportCancellation.Token);
            StatusLabel.Text = "Zapisano: " + dlg.FileName;
            MessageBox.Show(this, "Eksport zakończony:\n" + dlg.FileName, "Gotowe",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Eksport anulowany";
            try { File.Delete(dlg.FileName); } catch { }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Eksport nieudany";
            MessageBox.Show(this, ex.Message, "Błąd eksportu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _exportCancellation.Dispose();
            _exportCancellation = null;
            ExportButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            BusyProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _exportCancellation?.Cancel();

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _exportCancellation?.Cancel();
        _timer.Stop();
        CancelPreview();
    }
}