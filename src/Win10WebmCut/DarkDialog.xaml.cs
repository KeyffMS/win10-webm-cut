using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Win10WebmCut;

public partial class DarkDialog : Window
{
    public enum Kind { Information, Success, Warning, Error }

    public DarkDialog(string title, string message, Kind kind = Kind.Information)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        var color = kind switch
        {
            Kind.Success => Color.FromRgb(77, 211, 169),
            Kind.Warning => Color.FromRgb(255, 190, 92),
            Kind.Error => Color.FromRgb(255, 111, 128),
            _ => Color.FromRgb(112, 139, 255)
        };
        IconText.Text = kind switch
        {
            Kind.Success => "✓",
            Kind.Warning => "!",
            Kind.Error => "×",
            _ => "i"
        };
        IconText.Foreground = new SolidColorBrush(color);
        IconBackground.Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B));
    }

    public static void Show(Window owner, string title, string message, Kind kind = Kind.Information)
    {
        var dialog = new DarkDialog(title, message, kind) { Owner = owner };
        dialog.ShowDialog();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}