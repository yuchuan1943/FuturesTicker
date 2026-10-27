using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FuturesTicker.Models;

namespace FuturesTicker.Services;

/// <summary>
/// Fetches real-time domestic commodity futures quotes from the Sina finance
/// feed (https://hq.sinajs.cn). The response is GBK encoded.
/// </summary>
public class FuturesDataService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Referrer = new Uri("https://finance.sina.com.cn");
        client.Timeout = TimeSpan.FromSeconds(5);
        return client;
    }

    /// <summary>
    /// Fetches quotes for the given contract codes (e.g. RB0, AU0, SC0 for futures,
    /// au2612C648 for commodity options). Futures go through the nf_ feed, options
    /// through the OptionService.getOptionData chain feed.
    /// </summary>
    public async Task<List<FuturesQuote>> GetQuotesAsync(IEnumerable<string> codes)
    {
        var list = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (list.Count == 0) return new List<FuturesQuote>();

        var futures = list.Where(c => !OptionProducts.IsOptionCode(c)).ToList();
        var options = list.Where(OptionProducts.IsOptionCode).ToList();

        var result = new List<FuturesQuote>();

        // 期货：新浪 nf_ 接口（GBK 编码，一次批量）。
        if (futures.Count > 0)
        {
            var url = "https://hq.sinajs.cn/list=" + string.Join(",", futures.Select(c => "nf_" + c));
            var bytes = await Http.GetByteArrayAsync(url);
            var text = Encoding.GetEncoding("GBK").GetString(bytes);

            foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var quote = ParseLine(raw);
                if (quote != null) result.Add(quote);
            }
        }

        // 期权：P_OP 实时行情接口（批量一次请求）。
        // 注意不能用 getOptionData：它返回的是延迟/缓存的价格，与 P_OP 实时价不一致（列表价格会"错误"）。
        if (options.Count > 0)
        {
            result.AddRange(await GetOptionQuotesAsync(options));
        }

        return result;
    }

    /// <summary>
    /// 批量获取期权实时行情（新浪 P_OP 接口，逗号分隔一次请求多个合约）。
    /// P_OP 字段：0买量,1买价,2最新价,3卖价,4卖量,5持仓量,6涨幅(空),7行权价,
    /// 8昨结算价,9开盘价,10涨停价,11跌停价,12-31五档,32时间,39最高价,40最低价,41成交量。
    /// </summary>
    private async Task<List<FuturesQuote>> GetOptionQuotesAsync(List<string> codes)
    {
        var result = new List<FuturesQuote>();

        // 代码(大写) -> 标准小写 sym（P_OP 接口要求「品种小写+月份+C/P大写+行权价」）。
        var pairs = new List<(string Code, string Sym, OptionProducts.Parsed Parsed)>();
        foreach (var code in codes)
        {
            var p = OptionProducts.TryParse(code);
            if (p == null) continue;
            pairs.Add((code, p.Sym.ToLowerInvariant() + p.Month + p.Cp + p.Strike, p));
        }
        if (pairs.Count == 0) return result;

        try
        {
            var url = "https://hq.sinajs.cn/list=" + string.Join(",", pairs.Select(x => "P_OP_" + x.Sym));
            var bytes = await Http.GetByteArrayAsync(url);
            var text = Encoding.GetEncoding("GBK").GetString(bytes);

            var bySym = new Dictionary<string, FuturesQuote>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                // "var hq_str_P_OP_sa2612C1000=..." -> "sa2612C1000"
                var sym = line[..eq].Split('_').Last();

                int q1 = line.IndexOf('"', eq);
                int q2 = line.LastIndexOf('"');
                if (q1 < 0 || q2 <= q1) continue;
                var body = line.Substring(q1 + 1, q2 - q1 - 1);
                if (string.IsNullOrEmpty(body)) continue;

                var f = body.Split(',');
                if (f.Length < 42) continue;

                bySym[sym] = new FuturesQuote
                {
                    Last = Num(f[2]),
                    PrevSettle = Num(f[8]),
                    Open = Num(f[9]),
                    High = Num(f[39]),
                    Low = Num(f[40]),
                    Volume = Num(f[41]),
                    OpenInterest = Num(f[5]),
                };
            }

            foreach (var (code, sym, parsed) in pairs)
            {
                if (!bySym.TryGetValue(sym, out var q)) continue;
                q.Code = code;
                q.Name = $"{parsed.Info.Name}{(parsed.IsCall ? "购" : "沽")}{parsed.Strike}";
                q.Exchange = parsed.Info.ExchangeName;
                result.Add(q);
            }
        }
        catch
        {
            // 网络异常返回空，由上层静默处理。
        }

        return result;
    }

    /// <summary>
    /// 单个期权合约的完整实时行情（来自新浪 P_OP 接口），用于期权详情窗口。
    /// 字段已用多组合约实测验证，详见 GetOptionSpotAsync 注释。
    /// </summary>
    public sealed record OptionSpot(
        double Last, double PrevSettle, double Open, double High, double Low,
        double Bid, double Ask, double BidVolume, double AskVolume,
        double Volume, double OpenInterest,
        double Strike, double UpperLimit, double LowerLimit,
        string Time);

    /// <summary>
    /// 获取单个期权合约的完整实时行情（新浪 P_OP 接口）。
    /// 字段位（逗号分隔，实测确认）：
    ///   0 买量, 1 买价, 2 最新价, 3 卖价, 4 卖量, 5 持仓量, 6 涨幅(空),
    ///   7 行权价, 8 昨结算价, 9 开盘价, 10 涨停价, 11 跌停价,
    ///   12-31 五档买卖盘, 32 行情时间, 39 最高价, 40 最低价, 41 成交量。
    /// </summary>
    public async Task<OptionSpot?> GetOptionSpotAsync(string code)
    {
        code = code.Trim();
        if (string.IsNullOrEmpty(code)) return null;

        // 新浪 P_OP 接口大小写敏感：必须是「品种小写 + 月份 + C/P 大写 + 行权价」
        // （如 sa2612C1000）；全大写或全小写都返回空。这里用解析器重组出标准格式。
        var parsed = OptionProducts.TryParse(code);
        var sym = parsed != null
            ? parsed.Sym.ToLowerInvariant() + parsed.Month + parsed.Cp + parsed.Strike
            : code;

        try
        {
            var url = "https://hq.sinajs.cn/list=P_OP_" + sym;
            var bytes = await Http.GetByteArrayAsync(url);
            var text = Encoding.GetEncoding("GBK").GetString(bytes);

            int q1 = text.IndexOf('"');
            int q2 = text.LastIndexOf('"');
            if (q1 < 0 || q2 <= q1) return null;

            var body = text.Substring(q1 + 1, q2 - q1 - 1);
            if (string.IsNullOrEmpty(body)) return null;

            var f = body.Split(',');
            if (f.Length < 42) return null;

            return new OptionSpot(
                Last: Num(f[2]),
                PrevSettle: Num(f[8]),
                Open: Num(f[9]),
                High: Num(f[39]),
                Low: Num(f[40]),
                Bid: Num(f[1]),
                Ask: Num(f[3]),
                BidVolume: Num(f[0]),
                AskVolume: Num(f[4]),
                Volume: Num(f[41]),
                OpenInterest: Num(f[5]),
                Strike: Num(f[7]),
                UpperLimit: Num(f[10]),
                LowerLimit: Num(f[11]),
                Time: f[32]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取指定合约的分时数据（含均价线），用于分时图。
    /// 新浪接口 InnerFuturesNewService.getMinLine 返回 [时间, 最新价, 均价, 成交量, 持仓]，
    /// 首行额外带 [昨结算价, 日期]。
    /// </summary>
    public async Task<MinuteLineData> GetMinuteLineAsync(string code)
    {
        var data = new MinuteLineData();
        code = code.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return data;

        var url = "https://stock2.finance.sina.com.cn/futures/api/jsonp.php/var%20t=/InnerFuturesNewService.getMinLine?symbol=" + code;
        var text = await Http.GetStringAsync(url);

        int start = text.IndexOf('[');
        int end = text.LastIndexOf(']');
        if (start < 0 || end <= start) return data;

        var json = text[start..(end + 1)];
        using var doc = JsonDocument.Parse(json);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() < 5) continue;

            var p = new MinutePoint
            {
                Time = el[0].GetString() ?? "",
                Price = Num(el[1]),
                AvgPrice = Num(el[2]),
                Volume = Num(el[3]),
                OpenInterest = Num(el[4]),
            };
            data.Points.Add(p);

            // 首行额外带昨结算价（index 5 为昨结算，index 6 为日期字符串）。
            if (data.PrevSettle == 0 && el.GetArrayLength() >= 6)
            {
                data.PrevSettle = Num(el[5]);
            }
        }
        return data;
    }

    private static FuturesQuote? ParseLine(string line)
    {
        int eq = line.IndexOf('=');
        if (eq < 0) return null;

        // "var hq_str_nf_RB0=..." -> "RB0"
        var code = line[..eq].Split('_').Last().Trim();

        int q1 = line.IndexOf('"', eq);
        int q2 = line.LastIndexOf('"');
        if (q1 < 0 || q2 <= q1) return null;

        var body = line.Substring(q1 + 1, q2 - q1 - 1);
        if (string.IsNullOrEmpty(body)) return null;

        var f = body.Split(',');
        // Field layout (Sina nf_ feed):
        // 0 name, 2 open, 3 high, 4 low, 8 last, 10 prev settle,
        // 13 open interest, 14 volume, 15 exchange
        if (f.Length < 18 || string.IsNullOrWhiteSpace(f[0])) return null;

        return new FuturesQuote
        {
            Code = code,
            Name = f[0],
            Open = Num(f[2]),
            High = Num(f[3]),
            Low = Num(f[4]),
            Last = Num(f[8]),
            PrevSettle = Num(f[10]),
            OpenInterest = Num(f[13]),
            Volume = Num(f[14]),
            Exchange = f[15],
        };
    }

    private static double Num(string s) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// <summary>安全地把 JSON 元素解析为 double（兼容字符串与数值两种类型）。</summary>
    private static double Num(JsonElement el) =>
        el.ValueKind == JsonValueKind.Number ? el.GetDouble() :
        el.ValueKind == JsonValueKind.String
            ? (double.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0)
            : 0;
}
