using System.Windows;
using System.Windows.Controls;

namespace MockTax;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var client = ValueAfter(args, "--client") ?? "Margaret Buttle";
        var year = ValueAfter(args, "--year") ?? "2025";
        Title = $"MockTax - {client} {year}";
        RecipientName.Text = client;

        var readonlyField = ValueAfter(args, "--readonly");
        if (string.Equals(readonlyField, "Box22", StringComparison.Ordinal))
        {
            Box22.TextChanged += (_, _) =>
            {
                if (Box22.Text.Length > 0) Box22.Clear();
            };
        }
    }

    private static string? ValueAfter(string[] args, string option)
    {
        var index = Array.FindIndex(args, value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Status.Text = "Saved";
    }
}
