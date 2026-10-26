using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using FuturesTicker.Models;
using FuturesTicker.Native;
using FuturesTicker.Services;

namespace FuturesTicker;

/// <summary>
/// The floating ticker widget: a borderless, always-on-top card docked to the
/// taskbar, showing real-time commodity futures quotes (up=red, down=green).
/// Dims to near-transparent when the mouse is away, and highlights on hover.
/// Double-click opens the main control window.
/// </summary>
public partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }

    private readonly FuturesDataService _service = new();
    private readonly DispatcherTimer _timer;
    private readonly AppConfig _config = AppConfig.Current;
    private bool _isRefreshing;
    private IntPtr _hwnd;
    private bool _pinnedToCorner = true;

    public ObservableCollection<FuturesQuote> Quotes { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        QuoteList.ItemsSource = Quotes;
        Instance = this;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(1, _config.RefreshIntervalSeconds)),
        };
        _timer.Tick += async (_, _) => await RefreshAsync();

        ApplyHoverSettings();
        ApplyDisplaySettings();
        ApplyFontSize();

        // 尺寸变化（如行情数据加载后列表展开）时，若仍停靠在右下角，则保持右下角对齐，防止越界。
        SizeChanged += (_, _) =>
        {
            if (_pinnedToCorner) DockToTaskbar();
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DockToTaskbar();
        Topmost = _config.Topmost;
        _timer.Start();
        _ = RefreshAsync();

        // 若配置为隐藏悬浮窗，初始化完成后隐藏（托盘仍持续刷新价格）。
        if (!_config.ShowWidget) Hide();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Hide from Alt+Tab so it behaves like a tool widget.
        _hwnd = new WindowInteropHelper(this).Handle;
        int ex = Win32.GetWindowLong(_hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(_hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_TOOLWINDOW);

        ApplyMouseThrough();
    }

    /// <summary>切换鼠标穿透：穿透时点击/悬停直达下层（不挡任务栏），穿透后无法拖拽/悬停。</summary>
    public void ApplyMouseThrough()
    {
        if (_hwnd == IntPtr.Zero) return;

        long ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        if (_config.MouseThrough)
            ex |= Win32.WS_EX_TRANSPARENT | Win32.WS_EX_LAYERED;
        else
            ex &= ~(long)Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex));

        // 穿透时鼠标无法悬停，应用淡显透明度（与悬停淡出的目标一致）。
        ApplyHoverSettings();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_config.MouseThrough) return; // 穿透模式下鼠标不会进来，这里仅作兜底
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2) return; // 双击交给行级处理（弹分时图），不拖拽

        double oldX = Left, oldY = Top;

        // 系统级拖拽：由 Windows 消息循环接管，最流畅、最跟手，
        // 避免手动设置 Left/Top 导致透明窗口反复重定位合成的卡顿。
        // DragMove 阻塞直到鼠标释放。
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 快速点击时鼠标按钮可能已释放，忽略即可。
        }

        // 拖拽结束后：若窗口越出屏幕可见区域，自动移回工作区（排除任务栏）。
        var pos = TaskbarHelper.ClampToWorkArea(
            new Point(Left, Top),
            new Size(ActualWidth, ActualHeight));
        Left = pos.X;
        Top = pos.Y;

        // 确实发生了移动才解除右下角停靠（快速点击不误解除）。
        if (Math.Abs(Left - oldX) > 0.5 || Math.Abs(Top - oldY) > 0.5)
            _pinnedToCorner = false;
    }

    private void QuoteRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_config.MouseThrough) return;
        if (e.ClickCount != 2) return; // 单击留给窗口级做拖拽
        if (sender is not FrameworkElement fe || fe.DataContext is not FuturesQuote q) return;

        e.Handled = true; // 阻止冒泡，避免触发拖拽

        // 期权走期权详情窗口，期货走分时图。
        if (OptionProducts.IsOptionCode(q.Code))
        {
            OptionDetailWindow.Show(q);
            return;
        }

        MinuteChartWindow.Show(q);
    }

    // ---- Opacity ----

    /// <summary>应用固定透明度：悬浮窗始终以配置的淡显透明度显示（已取消悬停高亮）。</summary>
    private void ApplyHoverSettings()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = _config.DimOpacity;
    }

    /// <summary>按配置显示/隐藏悬浮窗。</summary>
    public void ApplyWidgetVisibility()
    {
        if (_config.ShowWidget)
        {
            if (!IsVisible) Show();
        }
        else
        {
            if (IsVisible) Hide();
        }
    }

    /// <summary>应用显示设置：只显示价格（切换模板+隐藏状态行）与颜色开关（重建列表重新求值）。</summary>
    public void ApplyDisplaySettings()
    {
        bool priceOnly = _config.PriceOnly;
        QuoteList.ItemTemplate = (DataTemplate)FindResource(priceOnly ? "PriceOnlyTemplate" : "FullTemplate");
        StatusText.Visibility = priceOnly ? Visibility.Collapsed : Visibility.Visible;

        // 颜色开关经 ChangeBrushConverter 生效，重建列表以重新应用转换结果。
        var src = QuoteList.ItemsSource;
        QuoteList.ItemsSource = null;
        QuoteList.ItemsSource = src;
    }

    /// <summary>仅应用透明度设置（供主界面拖动透明度滑块时实时预览，避免频繁刷新网络）。</summary>
    public void RefreshHover() => ApplyHoverSettings();

    /// <summary>按配置更新浮窗字号（供主界面拖动字体滑块时实时预览，DynamicResource 即时刷新）。</summary>
    public void ApplyFontSize()
    {
        double baseSize = Math.Clamp(_config.FontSize, 8, 20);
        Resources["PriceFontSize"] = baseSize;
        Resources["NameFontSize"] = Math.Max(7, baseSize - 1.5);
        Resources["BadgeFontSize"] = Math.Max(6.5, baseSize - 2.5);
    }

    /// <summary>应用全部配置变更并刷新列表（供主界面调用）。</summary>
    public void ApplyConfigChanges()
    {
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, _config.RefreshIntervalSeconds));
        Topmost = _config.Topmost;
        ApplyMouseThrough();
        ApplyDisplaySettings();
        ApplyFontSize();
        ApplyWidgetVisibility();
        _ = RefreshAsync();
    }

    // ---- Docking ----

    public void DockToTaskbar()
    {
        UpdateLayout();
        var size = new Size(ActualWidth, ActualHeight);
        var pos = TaskbarHelper.GetDockPosition(size, margin: 6);
        Left = pos.X;
        Top = pos.Y;
        _pinnedToCorner = true;
    }

    // ---- Refresh ----

    private async Task RefreshAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            var fresh = await _service.GetQuotesAsync(_config.Contracts);
            QuoteMerger.Merge(Quotes, fresh, _config.Contracts);
            StatusText.Text = $"· {DateTime.Now:HH:mm:ss}";
        }
        catch
        {
            StatusText.Text = "连接失败";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

}
