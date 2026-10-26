using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Win10WebmCut;

public partial class DarkDialog : Window
{
    public enum Kind { Information, Success, Warning, Error }
    private readonly string _message;
    public DarkDialog(string title, string message, Kind kind = Kind.Information, bool confirm = false)
    {
        InitializeComponent();
        _message = message;
        Title = TitleText.Text = title;
        MessageText.Text = message;
        (string icon, string foreground, string background) = kind switch
        {
            Kind.Success => ("✓", "Success", "SuccessSoft"),
            Kind.Warning => ("!", "Warning", "WarningSoft"),
            Kind.Error => ("×", "Danger", "DangerSoft"),
            _ => ("i", "Accent", "AccentSoft")
        };
        IconText.Text = icon;
        IconText.Foreground = (Brush)FindResource(foreground);
        IconBackground.Background = (Brush)FindResource(background);
        if (confirm) { CancelChoice.Visibility = Visibility.Visible; OkButton.Content = "Kontynuuj"; }
        Loaded += (_, _) =>
        {
            MaxHeight = Math.Max(280, Math.Min(640, SystemParameters.WorkArea.Height - 24));
            MessageText.MaxHeight = Math.Max(80, MaxHeight - 200);
            Width = Math.Max(MinWidth, Math.Min(Width, SystemParameters.WorkArea.Width - 24));
            OkButton.Focus();
        };
    }
    public static void Show(Window owner, string title, string message, Kind kind = Kind.Information) =>
        new DarkDialog(title, message, kind) { Owner = owner }.ShowDialog();
    public static bool Confirm(Window owner, string title, string message) =>
        new DarkDialog(title, message, Kind.Warning, confirm: true) { Owner = owner }.ShowDialog() == true;
    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Dialog_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; DialogResult = false; }
    }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && Mouse.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(AppLog.ForSharing(AppLog.Summary() + "\n\n" + Title + "\n" + _message)); }
        catch (Exception ex)
        {
            AppLog.Write("clipboard", ex.ToString());
            MessageText.Text = _message + "\n\nNie udało się zapisać do schowka. Zaznacz tekst i skopiuj ręcznie.";
        }
    }
}
