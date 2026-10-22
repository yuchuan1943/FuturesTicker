using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FuturesTicker.Models;
using FuturesTicker.Native;
using FuturesTicker.Services;

namespace FuturesTicker;

/// <summary>
/// 分时图窗口：展示某个合约的当日分时价格线 + 均价线 + 昨结基准线。
/// </summary>
public partial class MinuteChartWindow : Window
{
    private static readonly SolidColorBrush PriceBrush = Make(0x1E, 0x88, 0xE5);
    private static readonly SolidColorBrush AvgBrush = Make(0xFF, 0x98, 0x00);
    private static readonly SolidColorBrush SettleBrush = Make(0xBD, 0xBD, 0xBD);
    private static readonly SolidColorBrush AxisTextBrush = Make(0x99, 0x99, 0x99);
    private static readonly SolidColorBrush Red = Make(0xE5, 0x39, 0x35);
    private static readonly SolidColorBrush Green = Make(0x2E, 0x7D, 0x32);
    private static readonly SolidColorBrush Gray = Make(0x9E, 0x9E, 0x9E);
    private static readonly SolidColorBrush OiBrush = Make(0x8E, 0x24, 0xAA);

    private readonly FuturesDataService _service = new();
    private readonly string _code;
    private double _prevSettle;

    public static void Show(FuturesQuote q)
    {
        new MinuteChartWindow(q.Code, q.Name, q.Last, q.Change, q.PrevSettle).Show();
    }

    private MinuteChartWindow(string code, string name, double last, double change, double prevSettle)
    {
        InitializeComponent();

        _code = code;
        _prevSettle = prevSettle;

        Title = $"{name} 分时图";
        NameText.Text = name;
        CodeText.Text = code;
        LastText.Text = last.ToString("0.###");
        ChangeLabel.Text = change.ToString("+0.##;-0.##;0");

        var c = change > 0 ? Red : change < 0 ? Green : Gray;
        LastText.Foreground = c;
        ChangeLabel.Foreground = c;

        Loaded += async (_, _) => await LoadAndDrawAsync();

        // 按 ESC 关闭窗口。
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    /// <summary>无边框窗口拖拽：系统级 DragMove，松手后 clamp 回工作区（不越出屏幕）。</summary>
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

    private async Task LoadAndDrawAsync()
    {
        try
        {
            var data = await _service.GetMinuteLineAsync(_code);
            if (data.Points.Count == 0)
            {
                LoadingText.Text = "暂无分时数据";
                return;
            }

            if (data.PrevSettle != 0) _prevSettle = data.PrevSettle;
            Draw(data);
            LoadingText.Visibility = Visibility.Collapsed;
        }
        catch
        {
            LoadingText.Text = "获取分时数据失败";
        }
    }

    private void Draw(MinuteLineData data)
    {
        Chart.Children.Clear();
        var pts = data.Points;

        double w = Chart.ActualWidth;
        double h = Chart.ActualHeight;
        if (w < 20 || h < 20) return;

        const double padLeft = 8, padRight = 8, padTop = 10, padBottom = 18;
        double plotW = w - padLeft - padRight;
        double plotH = h - padTop - padBottom;

        // 价格范围：价格线、均价线、昨结均纳入
        double min = _prevSettle, max = _prevSettle;
        foreach (var p in pts)
        {
            min = Math.Min(min, Math.Min(p.Price, p.AvgPrice));
            max = Math.Max(max, Math.Max(p.Price, p.AvgPrice));
        }
        if (max - min < 1e-9) { max += 1; min -= 1; }
        double range = max - min;

        double X(int i) => padLeft + (pts.Count == 1 ? plotW / 2 : plotW * i / (pts.Count - 1));
        double Y(double price) => padTop + (max - price) / range * plotH;

        // 昨结虚线
        Chart.Children.Add(new Line
        {
            X1 = padLeft, Y1 = Y(_prevSettle), X2 = w - padRight, Y2 = Y(_prevSettle),
            Stroke = SettleBrush, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 },
        });

        // 均价线
        var avgLine = new Polyline { Stroke = AvgBrush, StrokeThickness = 1.5 };
        for (int i = 0; i < pts.Count; i++) avgLine.Points.Add(new Point(X(i), Y(pts[i].AvgPrice)));
        Chart.Children.Add(avgLine);

        // 价格线
        var priceLine = new Polyline { Stroke = PriceBrush, StrokeThickness = 1.5 };
        for (int i = 0; i < pts.Count; i++) priceLine.Points.Add(new Point(X(i), Y(pts[i].Price)));
        Chart.Children.Add(priceLine);

        // 左侧价格刻度：最高 / 昨结 / 最低
        AddYLabel(max, Y(max));
        AddYLabel(_prevSettle, Y(_prevSettle));
        AddYLabel(min, Y(min));

        // 底部时间刻度：起止
        AddXLabel(pts[0].Time, padLeft, h - padBottom + 4, left: true);
        AddXLabel(pts[^1].Time, w - padRight, h - padBottom + 4, left: false);

        LegendText.Text = $"　　—— 价格 {pts[^1].Price:0.###}　　　—— 均价 {pts[^1].AvgPrice:0.###}　　　- - 昨结 {_prevSettle:0.###}";

        // 成交量副图（红绿柱）
        DrawVolumeChart(pts);

        // 头部：累计成交量、当前持仓量、持仓量相对开盘的变化
        double totalVol = 0;
        foreach (var p in pts) totalVol += p.Volume;
        double lastOI = pts[^1].OpenInterest;
        double oiDelta = lastOI - pts[0].OpenInterest;
        VolumeInfoText.Text = $"成交量 {totalVol:0} 手　　持仓量 {lastOI:0} 手（较开盘 {(oiDelta >= 0 ? "+" : "")}{oiDelta:0} 手）";
    }

    private void AddYLabel(double value, double y)
    {
        var tb = new TextBlock
        {
            Text = value.ToString("0.###"),
            FontSize = 9,
            Foreground = AxisTextBrush,
        };
        Canvas.SetLeft(tb, 2);
        Canvas.SetTop(tb, y - 7);
        Chart.Children.Add(tb);
    }

    private void AddXLabel(string text, double x, double y, bool left)
    {
        var tb = new TextBlock { Text = text, FontSize = 9, Foreground = AxisTextBrush };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, left ? x : x - tb.DesiredSize.Width);
        Canvas.SetTop(tb, y);
        Chart.Children.Add(tb);
    }

    /// <summary>在底部副图绘制每分钟成交量柱状图（涨红跌绿）。</summary>
    private void DrawVolumeChart(List<MinutePoint> pts)
    {
        double vw = VolumeChart.ActualWidth;
        double vh = VolumeChart.ActualHeight;
        if (vw < 20 || vh < 20) return;

        const double padLeft = 8, padRight = 8, padTop = 14, padBottom = 2;
        double plotW = vw - padLeft - padRight;
        double plotH = vh - padTop - padBottom;
        if (plotH <= 0) return;

        double X(int i) => padLeft + (pts.Count == 1 ? plotW / 2 : plotW * i / (pts.Count - 1));

        double maxVol = 0;
        foreach (var p in pts) maxVol = Math.Max(maxVol, p.Volume);
        if (maxVol <= 0) maxVol = 1;

        double bw = pts.Count > 1 ? plotW / (pts.Count - 1) : plotW;
        double barW = Math.Max(1, bw * 0.6);

        for (int i = 0; i < pts.Count; i++)
        {
            double hh = Math.Max(1, pts[i].Volume / maxVol * plotH);
            double x = X(i);
            double y = padTop + (plotH - hh);

            // 涨红跌绿：首根对照昨结，其余对照前一根价格。
            var color = i == 0
                ? (pts[i].Price >= _prevSettle ? Red : Green)
                : (pts[i].Price >= pts[i - 1].Price ? Red : Green);

            var bar = new Rectangle
            {
                Width = barW,
                Height = hh,
                Fill = color,
            };
            Canvas.SetLeft(bar, x - barW / 2);
            Canvas.SetTop(bar, y);
            VolumeChart.Children.Add(bar);
        }

        // ---- 持仓量变化趋势线（相对开盘的增减，紫色） ----
        double baseOI = pts[0].OpenInterest;
        double minD = 0, maxD = 0;
        foreach (var p in pts)
        {
            double d = p.OpenInterest - baseOI;
            minD = Math.Min(minD, d);
            maxD = Math.Max(maxD, d);
        }
        double dRange = maxD - minD;
        double Y(double d) => dRange < 1e-9
            ? padTop + plotH / 2
            : padTop + (maxD - d) / dRange * plotH;

        var oiLine = new Polyline { Stroke = OiBrush, StrokeThickness = 1.5 };
        for (int i = 0; i < pts.Count; i++)
            oiLine.Points.Add(new Point(X(i), Y(pts[i].OpenInterest - baseOI)));
        VolumeChart.Children.Add(oiLine);

        // 0 基准虚线（持仓量与开盘持平）
        if (dRange >= 1e-9 && minD <= 0 && maxD >= 0)
        {
            double y0 = Y(0);
            VolumeChart.Children.Add(new Line
            {
                X1 = padLeft, Y1 = y0, X2 = vw - padRight, Y2 = y0,
                Stroke = OiBrush, StrokeThickness = 0.7,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Opacity = 0.55,
            });
        }

        // 左上角标注最大单分钟成交量；右上角标注持仓增减范围。
        var label = new TextBlock { Text = $"量 {maxVol:0}", FontSize = 9, Foreground = AxisTextBrush };
        Canvas.SetLeft(label, 2);
        Canvas.SetTop(label, 0);
        VolumeChart.Children.Add(label);

        var oiLabel = new TextBlock
        {
            Text = $"持仓增减 {(maxD >= 0 ? "+" : "")}{maxD:0} / {(minD >= 0 ? "+" : "")}{minD:0}",
            FontSize = 9,
            Foreground = OiBrush,
        };
        oiLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(oiLabel, vw - oiLabel.DesiredSize.Width - 2);
        Canvas.SetTop(oiLabel, 0);
        VolumeChart.Children.Add(oiLabel);
    }
}
