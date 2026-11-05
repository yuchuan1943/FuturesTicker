namespace FuturesTicker.Services;

/// <summary>
/// 国内商品期货交易时段判断：仅在工作日有交易（含夜盘）的时段返回 true。
/// 各交易时段前后各留 BufferMinutes 缓冲，既避免开盘/收盘前后漏刷，也避免休市期无效请求。
/// </summary>
public static class TradingHoursHelper
{
    /// <summary>交易时段前后的缓冲分钟数（提前开始、延后结束）。</summary>
    public const int BufferMinutes = 10;

    /// <summary>
    /// 当前时刻是否处于（含缓冲的）交易时段。
    /// 传入 hasGlobal=true 表示自选里有外盘品种——外盘工作日近似全天交易（周末除外），
    /// 因此非国内时段但处于工作日时也视为交易时段。
    /// </summary>
    public static bool IsTradingTime(DateTime now, bool hasGlobal = false)
    {
        // 周末休市。
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        int m = now.Hour * 60 + now.Minute;

        // 日盘（含缓冲）：9:00-10:15 → 8:50-10:25；10:30-11:30 → 10:20-11:40；13:30-15:00 → 13:20-15:10。
        if (In(m, 8 * 60 + 50, 10 * 60 + 25)) return true;
        if (In(m, 10 * 60 + 20, 11 * 60 + 40)) return true;
        if (In(m, 13 * 60 + 20, 15 * 60 + 10)) return true;

        // 夜盘（含缓冲）：21:00-次日02:30 → 20:50-次日02:40。
        // 只有周一~周四的晚上有夜盘（周五晚、周日休市）。
        if (In(m, 20 * 60 + 50, 23 * 60 + 59) &&
            now.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday)
            return true;

        // 凌晨段是前一晚夜盘的延续：周二~周五的 00:00-02:40（周一凌晨、周六/周日凌晨无夜盘）。
        if (In(m, 0, 2 * 60 + 40) &&
            now.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday or DayOfWeek.Friday)
            return true;

        // 外盘：工作日全天（近似），周末已在上面排除。
        if (hasGlobal) return true;

        return false;
    }

    /// <summary>自选里是否包含外盘期货品种。</summary>
    public static bool HasGlobalContract(IEnumerable<string> contracts) =>
        contracts.Any(GlobalProducts.IsGlobalCode);

    private static bool In(int minutes, int start, int end) => minutes >= start && minutes <= end;
}
