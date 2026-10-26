using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FuturesTicker.Models;
using FuturesTicker.Native;
using FuturesTicker.Services;

namespace FuturesTicker;

/// <summary>
/// 期权详情窗口：双击期权行弹出，展示单合约完整实时行情
/// （最新价、涨跌、昨结算/开盘/高低/涨跌停、成交量/持仓量、买一卖一盘口、行权价等）。
/// 数据来自新浪 P_OP 接口（比期权链 getOptionData 更全）。
/// </summary>
public partial class OptionDetailWindow : Window
{
    private static readonly SolidColorBrush Red = Make(0xE5, 0x39, 0x35);
    private static readonly SolidColorBrush Green = Make(0x2E, 0x7D, 0x32);
    private static readonly SolidColorBrush Gray = Make(0x9E, 0x9E, 0x9E);

    private readonly FuturesDataService _service = new();
    private readonly string _code;
    private readonly DispatcherTimer _timer;

    public static void Show(FuturesQuote q)
    {
        new OptionDetailWindow(q).Show();
    }

    private OptionDetailWindow(FuturesQuote q)
    {
        InitializeComponent();

        _code = q.Code;

        // 静态信息（合约代码解析出品种、月份、方向、行权价、交易所）。
        var parsed = OptionProducts.TryParse(q.Code);
        NameText.Text = parsed != null
            ? $"{parsed.Info.Name}{parsed.Month} {(parsed.IsCall ? "购" : "沽")}{parsed.Strike}"
            : q.Name;
        CodeText.Text = q.Code;
        DirectionText.Text = parsed?.IsCall == true ? "看涨（购）" : parsed != null ? "看跌（沽）" : "--";
        StrikeText.Text = parsed?.Strike ?? "--";
        MonthText.Text = parsed != null ? $"{parsed.Month[..2]}年{parsed.Month[2..]}月" : "--";
        ExchangeText.Text = parsed?.Info.ExchangeName ?? q.Exchange;

        // 先用已有报价占位，避免首屏空白。
        UpdateDisplay(q.Last, q.Change);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(2, AppConfig.Current.RefreshIntervalSeconds)),
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
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
        var s = await _service.GetOptionSpotAsync(_code);
        if (s == null) return;

        UpdateDisplay(s.Last, s.Last - s.PrevSettle);
        PrevSettleText.Text = s.PrevSettle.ToString("0.###");
        OpenText.Text = s.Open.ToString("0.###");
        HighText.Text = s.High.ToString("0.###");
        LowText.Text = s.Low.ToString("0.###");
        UpperText.Text = s.UpperLimit.ToString("0.###");
        LowerText.Text = s.LowerLimit.ToString("0.###");
        VolumeText.Text = $"{s.Volume:N0} 手";
        OiText.Text = $"{s.OpenInterest:N0} 手";
        BidText.Text = s.Bid.ToString("0.###");
        AskText.Text = s.Ask.ToString("0.###");
        BidVolText.Text = $"{s.BidVolume:N0}";
        AskVolText.Text = $"{s.AskVolume:N0}";
        TimeText.Text = string.IsNullOrWhiteSpace(s.Time) ? "" : $"行情时间 {s.Time}";
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
