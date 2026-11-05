using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private bool _recordingHotkey;

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

        // 主界面激活会把浮窗挤到后面，稍后强制浮窗重新置顶。
        MainWindow.Instance?.Dispatcher.BeginInvoke(
            MainWindow.Instance.EnsureTopmost, DispatcherPriority.ApplicationIdle);
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
        _timer.Tick += async (_, _) =>
        {
            if (!_config.OnlyTradeTime ||
                TradingHoursHelper.IsTradingTime(DateTime.Now, TradingHoursHelper.HasGlobalContract(_config.Contracts)))
                await RefreshAsync();
        };
        _timer.Start();

        // 窗口被真正关闭（如点 X）后，释放单例引用，下次 ShowWindow 重新创建。
        // 否则复用已 Closed 的窗口会导致 Show() 抛 InvalidOperationException。
        Closed += (_, _) => _instance = null;

        // 按 ESC 关闭窗口；录制老板键时 ESC 取消录制。
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (_recordingHotkey)
                {
                    StopRecording();
                }
                else
                {
                    Close();
                }
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
        OnlyTradeTimeCheck.IsChecked = _config.OnlyTradeTime;
        DimSlider.Value = _config.DimOpacity;
        DimLabel.Text = $"{_config.DimOpacity:P0}";
        FontSlider.Value = _config.FontSize;
        FontLabel.Text = $"{_config.FontSize:0.#}";
        MouseThroughCheck.IsChecked = _config.MouseThrough;
        ShowColorCheck.IsChecked = _config.ShowColor;
        PriceOnlyCheck.IsChecked = _config.PriceOnly;
        TopmostCheck.IsChecked = _config.Topmost;
        AutoStartCheck.IsChecked = _config.AutoStart;
        BossKeyCheck.IsChecked = _config.BossKeyEnabled;
        HotkeyBtn.Content = BossKeyService.Format(_config.BossKeyModifiers, _config.BossKeyKey);
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

    private void OnlyTradeTime_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.OnlyTradeTime = OnlyTradeTimeCheck.IsChecked == true;
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

    private void BossKey_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.BossKeyEnabled = BossKeyCheck.IsChecked == true;
        ConfigStore.Save(_config);
        BossKeyService.Instance?.Apply();
    }

    private void Hotkey_Record_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingHotkey) return;
        _recordingHotkey = true;
        HotkeyBtn.Content = "按下新组合键…";
        PreviewKeyDown += Hotkey_PreviewKeyDown;
        HotkeyBtn.Focus();
    }

    private void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recordingHotkey) return;
        e.Handled = true;

        // Alt 组合键时 WPF 会把主键放进 SystemKey，这里取真实主键。
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key)) return; // 忽略纯修饰键，等待真正的主键

        int modifiers = (int)Keyboard.Modifiers;
        int vk = KeyInterop.VirtualKeyFromKey(key);

        _config.BossKeyModifiers = modifiers;
        _config.BossKeyKey = vk;
        ConfigStore.Save(_config);
        BossKeyService.Instance?.Apply();

        StopRecording();
    }

    private static bool IsModifierKey(Key key) =>
        key == Key.LeftCtrl || key == Key.RightCtrl ||
        key == Key.LeftAlt || key == Key.RightAlt ||
        key == Key.LeftShift || key == Key.RightShift ||
        key == Key.LWin || key == Key.RWin;

    private void StopRecording()
    {
        _recordingHotkey = false;
        PreviewKeyDown -= Hotkey_PreviewKeyDown;
        HotkeyBtn.Content = BossKeyService.Format(_config.BossKeyModifiers, _config.BossKeyKey);
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
