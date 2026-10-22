using System.ComponentModel;

namespace FuturesTicker.Models;

/// <summary>
/// A single futures quote. Implements change notification so the UI
/// updates in place without flicker.
/// </summary>
public class FuturesQuote : INotifyPropertyChanged
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Exchange { get; set; } = "";

    /// <summary>紧凑显示名（去掉“连续”“上海”等词缀，用于隐蔽模式）。</summary>
    public string ShortName => Name.Replace("连续", "").Replace("上海", "");

    private double _last;
    public double Last
    {
        get => _last;
        set
        {
            if (_last == value) return;
            _last = value;
            OnChanged(nameof(Last));
            OnChanged(nameof(Change));
            OnChanged(nameof(ChangePercent));
            OnChanged(nameof(ChangeText));
        }
    }

    private double _prevSettle;
    public double PrevSettle
    {
        get => _prevSettle;
        set
        {
            if (_prevSettle == value) return;
            _prevSettle = value;
            OnChanged(nameof(PrevSettle));
            OnChanged(nameof(Change));
            OnChanged(nameof(ChangePercent));
            OnChanged(nameof(ChangeText));
        }
    }

    private double _open;
    public double Open
    {
        get => _open;
        set { if (_open == value) return; _open = value; OnChanged(nameof(Open)); OnChanged(nameof(DetailText)); }
    }

    private double _high;
    public double High
    {
        get => _high;
        set { if (_high == value) return; _high = value; OnChanged(nameof(High)); OnChanged(nameof(DetailText)); }
    }

    private double _low;
    public double Low
    {
        get => _low;
        set { if (_low == value) return; _low = value; OnChanged(nameof(Low)); OnChanged(nameof(DetailText)); }
    }

    private double _volume;
    public double Volume
    {
        get => _volume;
        set { if (_volume == value) return; _volume = value; OnChanged(nameof(Volume)); OnChanged(nameof(DetailText)); }
    }

    private double _openInterest;
    public double OpenInterest
    {
        get => _openInterest;
        set { if (_openInterest == value) return; _openInterest = value; OnChanged(nameof(OpenInterest)); OnChanged(nameof(DetailText)); }
    }

    /// <summary>涨跌额 = 最新价 - 昨结算。</summary>
    public double Change => Last - PrevSettle;

    /// <summary>涨跌幅（相对昨结算价，期货惯例）。</summary>
    public double ChangePercent => PrevSettle != 0 ? (Last - PrevSettle) / PrevSettle * 100 : 0;

    /// <summary>显示文本：涨跌额 + 涨跌幅。</summary>
    public string ChangeText => $"{Change:+0.##;-0.##;0}  {ChangePercent:+0.00;-0.00;0.00}%";

    /// <summary>悬浮提示的详细行情。</summary>
    public string DetailText => $"最高 {High:0.###}  最低 {Low:0.###}\n持仓 {OpenInterest:N0}  成交 {Volume:N0}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
