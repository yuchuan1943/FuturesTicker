using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FuturesTicker.Models;
using FuturesTicker.Services;

namespace FuturesTicker;

/// <summary>
/// The main control window: manages the watchlist and all display settings.
/// Opened by double-clicking the floating widget (or from its context menu).
/// </summary>
public partial class MainControlWindow : Window
{
    private static MainControlWindow? _instance;

    private readonly FuturesDataService _service = new();
    private readonly DispatcherTimer _timer;
    private readonly AppConfig _config = AppConfig.Current;
    private bool _isRefreshing;
    private bool _loading;

    private static readonly int[] Intervals = { 1, 2, 3, 5, 10 };

    public ObservableCollection<FuturesQuote> Quotes { get; } = new();

    public static void ShowWindow()
    {
        if (_instance == null)
        {
            _instance = new MainControlWindow();
        }
        _instance.Show();
        _instance.Activate();
        _instance._timer.Start();
    }

    public MainControlWindow()
    {
        // Suppress control-event handlers while XAML is being parsed; some
        // controls (e.g. Slider) fire ValueChanged during initialization,
        // before their siblings (e.g. DimLabel) are constructed.
        _loading = true;

        InitializeComponent();
        ContractList.ItemsSource = Quotes;

        // Common contract quick-add buttons.
        foreach (var (code, name) in CommonContracts.Items)
        {
            var btn = new Button
            {
                Content = name,
                Margin = new Thickness(2),
                Padding = new Thickness(9, 3, 9, 3),
                FontSize = 11,
                ToolTip = $"{name}（{code}）",
            };
            var captured = code;
            btn.Click += (_, _) => AddContract(captured);
            CommonPanel.Children.Add(btn);
        }

        LoadSettings();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(1, _config.RefreshIntervalSeconds)),
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        // 窗口被真正关闭（如点 X）后，释放单例引用，下次 ShowWindow 重新创建。
        // 否则复用已 Closed 的窗口会导致 Show() 抛 InvalidOperationException。
        Closed += (_, _) => _instance = null;

        // 按 ESC 关闭窗口。
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        _ = RefreshAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Pause polling while the window is hidden; it resumes on ShowWindow.
        _timer.Stop();
    }

    private void LoadSettings()
    {
        _loading = true;
        int idx = Array.IndexOf(Intervals, _config.RefreshIntervalSeconds);
        IntervalCombo.SelectedIndex = idx >= 0 ? idx : 2;
        DimSlider.Value = _config.DimOpacity;
        DimLabel.Text = $"{_config.DimOpacity:P0}";
        FontSlider.Value = _config.FontSize;
        FontLabel.Text = $"{_config.FontSize:0.#}";
        ShowWidgetCheck.IsChecked = _config.ShowWidget;
        MouseThroughCheck.IsChecked = _config.MouseThrough;
        ShowColorCheck.IsChecked = _config.ShowColor;
        PriceOnlyCheck.IsChecked = _config.PriceOnly;
        TopmostCheck.IsChecked = _config.Topmost;
        AutoStartCheck.IsChecked = _config.AutoStart;
        _loading = false;
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            var fresh = await _service.GetQuotesAsync(_config.Contracts);
            QuoteMerger.Merge(Quotes, fresh, _config.Contracts);
        }
        catch
        {
            // Ignore transient network errors.
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void SaveAndApply()
    {
        ConfigStore.Save(_config);
        MainWindow.Instance?.ApplyConfigChanges();
    }

    // ---- Contract management ----

    private void AddContract(string code)
    {
        code = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return;
        if (_config.Contracts.Contains(code)) return;

        _config.Contracts.Add(code);
        SaveAndApply();
        _ = RefreshAsync();
        CodeBox.Clear();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddContract(CodeBox.Text);

    private void CodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddContract(CodeBox.Text);
    }

    private void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        // 行内删除按钮：直接取该行的 DataContext（合约），无需先选中。
        if (sender is not Button btn || btn.DataContext is not FuturesQuote q) return;
        _config.Contracts.Remove(q.Code);
        SaveAndApply();
        _ = RefreshAsync();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(+1);

    private void MoveSelected(int delta)
    {
        if (ContractList.SelectedItem is not FuturesQuote q) return;

        var list = _config.Contracts;
        int ci = list.IndexOf(q.Code);
        int nj = ci + delta;
        if (ci < 0 || nj < 0 || nj >= list.Count) return;

        (list[ci], list[nj]) = (list[nj], list[ci]);

        int ui = Quotes.IndexOf(q);
        Quotes.Move(ui, ui + delta);
        ContractList.SelectedItem = q;

        SaveAndApply();
    }

    // ---- Settings ----

    private void Interval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || IntervalCombo.SelectedIndex < 0) return;
        _config.RefreshIntervalSeconds = Intervals[IntervalCombo.SelectedIndex];
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, _config.RefreshIntervalSeconds));
        SaveAndApply();
    }

    private void Dim_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var v = Math.Round(DimSlider.Value, 2);
        DimLabel.Text = $"{v:P0}";
        _config.DimOpacity = v;
        ConfigStore.Save(_config);
        MainWindow.Instance?.RefreshHover();
    }

    private void Font_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var v = Math.Round(FontSlider.Value, 1);
        FontLabel.Text = $"{v:0.#}";
        _config.FontSize = v;
        ConfigStore.Save(_config);
        MainWindow.Instance?.ApplyFontSize();
    }

    private void ShowWidget_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.ShowWidget = ShowWidgetCheck.IsChecked == true;
        SaveAndApply();
        TrayIconService.Instance?.SyncFromConfig();
    }

    private void MouseThrough_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.MouseThrough = MouseThroughCheck.IsChecked == true;
        SaveAndApply();
        TrayIconService.Instance?.SyncFromConfig();
    }

    private void ShowColor_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.ShowColor = ShowColorCheck.IsChecked == true;
        SaveAndApply();
        TrayIconService.Instance?.SyncFromConfig();
    }

    private void PriceOnly_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.PriceOnly = PriceOnlyCheck.IsChecked == true;
        SaveAndApply();
        TrayIconService.Instance?.SyncFromConfig();
    }

    private void Topmost_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.Topmost = TopmostCheck.IsChecked == true;
        SaveAndApply();
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.AutoStart = AutoStartCheck.IsChecked == true;
        AutoStartHelper.SetAutoStart(_config.AutoStart);
        SaveAndApply();
    }

    private void ResetPos_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.DockToTaskbar();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
