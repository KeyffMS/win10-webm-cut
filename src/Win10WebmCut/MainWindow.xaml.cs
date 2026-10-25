using Microsoft.Win32;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
    private bool _draggingSlider;
    private bool _selectingCrop;
    private Point _dragStart;
    private CropArea? _crop;
    private CancellationTokenSource? _exportCancellation;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) =>
        {
            if (_info is null || _draggingSlider) return;
            double seconds = Math.Clamp(Player.Position.TotalSeconds, 0, _info.Duration);
            PositionSlider.Value = seconds;
            TimeLabel.Text = $"{FormatTime(seconds)} / {FormatTime(_info.Duration)}";
        };
        _timer.Start();
        Loaded += (_, _) => UpdateCropDisplay();
    }

    private static string FormatTime(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss");

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
            Player.Stop();
            _playing = false;
            PlayButton.Content = "▶ Odtwarzaj";
            _input = null;
            _info = null;
            _crop = null;
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
            Player.Source = new Uri(_input);
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

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        PreviewNote.Visibility = Visibility.Collapsed;
        UpdateCropDisplay();
    }

    private void Player_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        PreviewNote.Text = "Brak obsługi podglądu przez Windows MediaElement";
        PreviewNote.Visibility = Visibility.Visible;
        StatusLabel.Text = "FFmpeg nadal może wyeksportować plik.";
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_info is null) return;
        if (_playing) { Player.Pause(); _playing = false; }
        else { Player.Play(); _playing = true; }
        PlayButton.Content = _playing ? "⏸ Pauza" : "▶ Odtwarzaj";
    }

    private void Slider_MouseDown(object sender, MouseButtonEventArgs e) => _draggingSlider = true;

    private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingSlider = false;
        if (_info is not null) Player.Position = TimeSpan.FromSeconds(PositionSlider.Value);
    }

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_info is not null && _draggingSlider)
            TimeLabel.Text = $"{FormatTime(PositionSlider.Value)} / {FormatTime(_info.Duration)}";
    }

    private void MarkStart_Click(object sender, RoutedEventArgs e)
    {
        if (_info is not null) StartBox.Text = Number(Player.Position.TotalSeconds);
    }

    private void MarkEnd_Click(object sender, RoutedEventArgs e)
    {
        if (_info is not null) EndBox.Text = Number(Player.Position.TotalSeconds);
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
        _dragStart = ClampPoint(e.GetPosition(PreviewHost), bounds);
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
        _crop = new CropArea(x, y, w, h);
        CropLabel.Text = $"Crop: {w}×{h} @ {x},{y}";
        if (ProfileBox.SelectedIndex == 0) ProfileBox.SelectedIndex = 1;
        UpdateCropDisplay();
    }

    private void DrawRectangle(Rect rect)
    {
        Canvas.SetLeft(CropRect, rect.Left);
        Canvas.SetTop(CropRect, rect.Top);
        CropRect.Width = rect.Width;
        CropRect.Height = rect.Height;
    }

    private void UpdateCropDisplay()
    {
        CropCanvas.Width = PreviewHost.ActualWidth;
        CropCanvas.Height = PreviewHost.ActualHeight;
        if (_crop is null || _info is null || CropMode.IsChecked != true ||
            !TryGetVideoBounds(out Rect bounds))
        {
            CropRect.Visibility = Visibility.Collapsed;
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

    private void ResetCrop()
    {
        _crop = null;
        CropRect.Visibility = Visibility.Collapsed;
        CropLabel.Text = "Cały obraz";
    }

    private void PreviewHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCropDisplay();

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (CropRect is not null) UpdateCropDisplay();
        if (ProfileBox is not null && CropMode?.IsChecked == true && ProfileBox.SelectedIndex == 0)
            ProfileBox.SelectedIndex = 1;
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
        Player.Pause();
        _playing = false;
        PlayButton.Content = "▶ Odtwarzaj";
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
        Player.Close();
    }
}