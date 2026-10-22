using System.Text.RegularExpressions;

namespace FuturesTicker.Services;

/// <summary>
/// 商品期权品种映射与期权合约代码解析。
/// 新浪期权行情走 OptionService.getOptionData 期权链接口，需要 (product, exchange, pinzhong) 三个参数；
/// 本类负责从期权合约代码（如 au2612C648）反推出这三个参数及品种中文名。
/// </summary>
public static class OptionProducts
{
    /// <summary>期权品种信息。</summary>
    public sealed record Info(string Product, string Exchange, string ExchangeName, string Name);

    /// <summary>解析后的期权合约。</summary>
    public sealed record Parsed(string Sym, string Month, char Cp, string Strike, Info Info)
    {
        /// <summary>标的合约（小写品种 + 4 位月份），如 au2612。</summary>
        public string PinZhong => Sym.ToLowerInvariant() + Month;

        /// <summary>是否看涨期权。</summary>
        public bool IsCall => Cp == 'C';
    }

    // 品种代码 -> (product, exchange, 交易所中文名, 品种中文名)。来源：新浪 optionsDP.php 页面品种链接。
    private static readonly Dictionary<string, Info> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["m"]  = new("m_o",  "dce",  "大商所", "豆粕"),
        ["c"]  = new("c_o",  "dce",  "大商所", "玉米"),
        ["i"]  = new("i_o",  "dce",  "大商所", "铁矿石"),
        ["cf"] = new("cf",   "czce", "郑商所", "棉花"),
        ["sr"] = new("sr",   "czce", "郑商所", "白糖"),
        ["ta"] = new("ta",   "czce", "郑商所", "PTA"),
        ["oi"] = new("oi",   "czce", "郑商所", "菜油"),
        ["pk"] = new("pk",   "czce", "郑商所", "花生"),
        ["ma"] = new("ma",   "czce", "郑商所", "甲醇"),
        ["ru"] = new("ru_o", "shfe", "上期所", "橡胶"),
        ["cu"] = new("cu_o", "shfe", "上期所", "沪铜"),
        ["au"] = new("au_o", "shfe", "上期所", "黄金"),
        ["rm"] = new("rm",   "czce", "郑商所", "菜籽粕"),
        ["pg"] = new("pg_o", "dce",  "大商所", "液化石油气"),
        ["zc"] = new("zc",   "czce", "郑商所", "动力煤"),
        ["a"]  = new("a_o",  "dce",  "大商所", "豆一"),
        ["b"]  = new("b_o",  "dce",  "大商所", "豆二"),
        ["y"]  = new("y_o",  "dce",  "大商所", "豆油"),
        ["ag"] = new("ag_o", "shfe", "上期所", "白银"),
        ["rb"] = new("rb_o", "shfe", "上期所", "螺纹钢"),
        ["si"] = new("si_o", "gfex", "广期所", "工业硅"),
        ["eg"] = new("eg_o", "dce",  "大商所", "乙二醇"),
        ["eb"] = new("eb_o", "dce",  "大商所", "苯乙烯"),
        ["lc"] = new("lc_o", "gfex", "广期所", "碳酸锂"),
        ["br"] = new("br_o", "shfe", "上期所", "丁二烯橡胶"),
        ["al"] = new("al_o", "shfe", "上期所", "沪铝"),
        ["px"] = new("px",   "czce", "郑商所", "对二甲苯"),
        ["sh"] = new("sh",   "czce", "郑商所", "烧碱"),
    };

    // 期权合约代码：品种(1-2字母) + 月份(4位) + C/P + 行权价(数字)。
    // 贪婪 {1,2} 对 cu2601C50000 先匹配 2 字母 "cu"；对 c2509C2600 因第二位非字母回退到 1 字母 "c"。
    private static readonly Regex OptionPattern = new(
        @"^(?<sym>[A-Za-z]{1,2})(?<month>\d{4})(?<cp>[CP])(?<strike>\d+)$",
        RegexOptions.Compiled);

    /// <summary>尝试解析期权合约代码；不是已知品种的合法期权代码时返回 null。</summary>
    public static Parsed? TryParse(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var m = OptionPattern.Match(code.Trim().ToUpperInvariant());
        if (!m.Success) return null;

        var sym = m.Groups["sym"].Value;
        if (!Map.TryGetValue(sym, out var info)) return null;

        return new Parsed(sym, m.Groups["month"].Value, m.Groups["cp"].Value[0], m.Groups["strike"].Value, info);
    }

    /// <summary>是否为合法期权合约代码。</summary>
    public static bool IsOptionCode(string code) => TryParse(code) != null;
}
