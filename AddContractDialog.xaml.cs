using System.Windows;
using System.Windows.Controls;
using FuturesTicker.Services;

namespace FuturesTicker;

public partial class AddContractDialog : Window
{
    public string? ResultCode { get; private set; }

    public AddContractDialog()
    {
        InitializeComponent();

        foreach (var (code, name) in CommonContracts.Items)
        {
            var btn = new Button
            {
                Content = $"{name} {code}",
                Margin = new Thickness(2),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                ToolTip = $"添加 {name}（{code}）",
            };
            btn.Click += (_, _) =>
            {
                ResultCode = code;
                DialogResult = true;
            };
            ButtonsPanel.Children.Add(btn);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultCode = CodeBox.Text.Trim();
        DialogResult = !string.IsNullOrWhiteSpace(ResultCode);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
