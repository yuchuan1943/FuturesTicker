using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FuturesTicker.Models;
using FuturesTicker.Native;
using FuturesTicker.Services;

namespace FuturesTicker;

/// <summary>
/// 外盘期货详情窗口：双击外盘期货行弹出，展示单品种完整实时行情
/// （最新价、涨跌、昨结算/开盘/高低、买价卖价、持仓量、交易所、行情时间等）。
/// 数据来自新浪 hf_ 接口（外盘没有分时数据，故不做分时图）。
/// </summary>
public partial class GlobalDetailWindow : Window
{
    private static readonly SolidColorBrush Red = Make(0xE5, 0x39, 0x35);
    private static readonly SolidColorBrush Green = Make(0x2E, 0x7D, 0x32);
    private static readonly SolidColorBrush Gray = Make(0x9E, 0x9E, 0x9E);

    private readonly FuturesDataService _service = new();
    private readonly string _code;
    private readonly DispatcherTimer _timer;

    public static void Show(FuturesQuote q)
    {
        new GlobalDetailWindow(q).Show();
    }

    private GlobalDetailWindow(FuturesQuote q)
    {
        InitializeComponent();

        _code = q.Code;

        NameText.Text = q.Name;
        CodeText.Text = q.Code;
        ExchangeText.Text = q.Exchange;

        // 先用已有报价占位，避免首屏空白。
        UpdateDisplay(q.Last, q.Change);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(2, AppConfig.Current.RefreshIntervalSeconds)),
        };
        _timer.Tick += async (_, _) =>
        {
            // 外盘工作日近似全天交易，故 hasGlobal 传 true。
            if (AppConfig.Current.OnlyTradeTime && !TradingHoursHelper.IsTradingTime(DateTime.Now, true))
                return;
            await RefreshAsync();
        };
        _timer.Start();

        Loaded += async (_, _) => await RefreshAsync();

        // 按 ESC 关闭窗口。
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        Closed += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        var s = await _service.GetGlobalSpotAsync(_code);
        if (s == null) return;

        UpdateDisplay(s.Last, s.Last - s.PrevSettle);
        PrevSettleText.Text = s.PrevSettle.ToString("0.###");
        OpenText.Text = s.Open.ToString("0.###");
        HighText.Text = s.High.ToString("0.###");
        LowText.Text = s.Low.ToString("0.###");
        BidText.Text = s.Bid.ToString("0.###");
        AskText.Text = s.Ask.ToString("0.###");
        OiText.Text = s.OpenInterest.ToString("N0");
        ExchangeText.Text = s.Exchange;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.Time)) parts.Add($"行情时间 {s.Time}");
        if (!string.IsNullOrWhiteSpace(s.Date)) parts.Add(s.Date);
        TimeText.Text = parts.Count > 0 ? string.Join("　", parts) : "加载中…";
    }

    private void UpdateDisplay(double last, double change)
    {
        var c = change > 0 ? Red : change < 0 ? Green : Gray;
        LastText.Text = last.ToString("0.###");
        LastText.Foreground = c;
        ChangeText.Text = $"{change:+0.###;-0.###;0}";
        ChangeText.Foreground = c;
    }

    /// <summary>无边框窗口拖拽：系统级 DragMove，松手后 clamp 回工作区。</summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 快速点击时鼠标按钮可能已释放，忽略。
        }

        var pos = TaskbarHelper.ClampToWorkArea(
            new Point(Left, Top),
            new Size(ActualWidth, ActualHeight));
        Left = pos.X;
        Top = pos.Y;
    }

    private static SolidColorBrush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
