using System.Text;
using System.Threading;
using System.Windows;
using FuturesTicker.Services;

namespace FuturesTicker;

/// <summary>
/// Application entry. Registers the GBK encoding provider (for Sina quotes),
/// enforces a single instance, and hosts the system-tray icon.
/// </summary>
public partial class App : Application
{
    private Mutex? _mutex;
    private TrayIconService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "FuturesTicker_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("期货行情悬浮窗已在运行中。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Required for decoding the GBK-encoded Sina quote feed.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        base.OnStartup(e);

        var window = new MainWindow();
        window.Show();

        _tray = new TrayIconService();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
