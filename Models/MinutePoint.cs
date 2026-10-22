namespace FuturesTicker.Models;

/// <summary>分时线上的一个采样点。</summary>
public class MinutePoint
{
    public string Time { get; set; } = "";
    public double Price { get; set; }
    public double AvgPrice { get; set; }
    public double Volume { get; set; }
    public double OpenInterest { get; set; }
}

/// <summary>某个合约的分时数据（含昨结算价，作为分时基准线）。</summary>
public class MinuteLineData
{
    public double PrevSettle { get; set; }
    public List<MinutePoint> Points { get; } = new();
}
