namespace FuturesTicker.Services;

/// <summary>
/// 新浪外盘（国际）期货品种映射：代码 -> 中文名 + 交易所。
/// 行情接口用 hf_ 前缀（与国内 nf_ 不同），字段布局也完全不同，详见 FuturesDataService。
/// 识别规则：代码精确命中本表（不区分大小写）即视为外盘，优先级高于国内期货判断。
/// </summary>
public static class GlobalProducts
{
    public sealed record Info(string Code, string Name, string Exchange);

    private static readonly Dictionary<string, Info> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CL"] = new("CL", "WTI原油", "NYMEX"),
        ["OIL"] = new("OIL", "布伦特原油", "ICE"),
        ["GC"] = new("GC", "COMEX黄金", "COMEX"),
        ["SI"] = new("SI", "COMEX白银", "COMEX"),
        ["HG"] = new("HG", "COMEX铜", "COMEX"),
        ["NG"] = new("NG", "NYMEX天然气", "NYMEX"),
        ["S"] = new("S", "CBOT大豆", "CBOT"),
        ["C"] = new("C", "CBOT玉米", "CBOT"),
        ["W"] = new("W", "CBOT小麦", "CBOT"),
        ["BO"] = new("BO", "CBOT豆油", "CBOT"),
        ["SM"] = new("SM", "CBOT豆粕", "CBOT"),
        ["CT"] = new("CT", "NYBOT棉花", "NYBOT"),
        ["RS"] = new("RS", "美国原糖", "NYBOT"),
        ["CAD"] = new("CAD", "LME铜3个月", "LME"),
        ["AHD"] = new("AHD", "LME铝3个月", "LME"),
        ["ZSD"] = new("ZSD", "LME锌3个月", "LME"),
        ["PBD"] = new("PBD", "LME铅3个月", "LME"),
        ["NID"] = new("NID", "LME镍3个月", "LME"),
        ["SND"] = new("SND", "LME锡3个月", "LME"),
        ["XAU"] = new("XAU", "伦敦金", "LME"),
        ["XAG"] = new("XAG", "伦敦银", "LME"),
        ["XPT"] = new("XPT", "伦敦铂金", "LME"),
        ["XPD"] = new("XPD", "伦敦钯金", "LME"),
        ["FEF"] = new("FEF", "新加坡铁矿石", "SGX"),
        ["FCPO"] = new("FCPO", "马棕油", "BMD"),
        ["RSS3"] = new("RSS3", "日橡胶", "TOCOM"),
        ["TRB"] = new("TRB", "日本橡胶", "TOCOM"),
        ["LHC"] = new("LHC", "CME瘦肉猪", "CME"),
        ["BTC"] = new("BTC", "CME比特币期货", "CME"),
        ["EUA"] = new("EUA", "欧洲碳排放", "ICE"),
        ["HSI"] = new("HSI", "恒生指数期货", "HKEX"),
    };

    /// <summary>判断代码是否为外盘期货（精确匹配映射表，不区分大小写）。</summary>
    public static bool IsGlobalCode(string code) =>
        !string.IsNullOrWhiteSpace(code) && Map.ContainsKey(code.Trim());

    /// <summary>取外盘品种信息；非外盘返回 null。</summary>
    public static Info? Get(string code) =>
        Map.TryGetValue(code.Trim(), out var info) ? info : null;
}
