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

        // 期权：新浪 getOptionData 期权链接口，按 (product, exchange, pinzhong) 分组拉整链后提取目标合约。
        if (options.Count > 0)
        {
            var groups = options
                .Select(code => (Code: code, Parsed: OptionProducts.TryParse(code)))
                .Where(x => x.Parsed != null)
                .GroupBy(x => (x.Parsed!.Info.Product, x.Parsed!.Info.Exchange, x.Parsed!.PinZhong));

            foreach (var g in groups)
            {
                var chain = await GetOptionChainAsync(g.Key.Product, g.Key.Exchange, g.Key.PinZhong);
                foreach (var (code, parsed) in g)
                {
                    if (chain.TryGetValue(code, out var row))
                    {
                        result.Add(ToQuote(code, parsed!, row));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>期权链中的一行行情（已按字段位映射）。</summary>
    private sealed record OptionRow(double Bid, double Ask, double Last, double OpenInterest, double ChangePercent);

    /// <summary>
    /// 拉取指定品种+标的的完整期权链（含看涨 up / 看跌 down），返回 合约代码 -> 行情 映射。
    /// 字段：up = [买量, 买价, 最新价, 卖价, 卖量, 持仓量, 涨跌幅%, 行权价, 合约代码]；
    ///       down = [买量, 买价, 最新价, 卖价, 卖量, 持仓量, 涨跌幅%, 合约代码]。
    /// </summary>
    private async Task<Dictionary<string, OptionRow>> GetOptionChainAsync(string product, string exchange, string pinZhong)
    {
        var map = new Dictionary<string, OptionRow>(StringComparer.OrdinalIgnoreCase);

        var url = "https://stock.finance.sina.com.cn/futures/api/openapi.php/OptionService.getOptionData" +
                  $"?type=futures&product={product}&exchange={exchange}&pinzhong={pinZhong}";
        var json = await Http.GetStringAsync(url);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var resultEl)) return map;
        if (!resultEl.TryGetProperty("data", out var dataEl)) return map;

        foreach (var side in new[] { "up", "down" })
        {
            if (!dataEl.TryGetProperty(side, out var arr)) continue;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Array) continue;
                int len = el.GetArrayLength();
                if (len < 7) continue;

                var contract = Str(el[len - 1]);
                if (string.IsNullOrWhiteSpace(contract)) continue;

                map[contract] = new OptionRow(
                    Bid: Num(Str(el[1])),
                    Ask: Num(Str(el[3])),
                    Last: Num(Str(el[2])),
                    OpenInterest: Num(Str(el[5])),
                    ChangePercent: Num(Str(el[6])));
            }
        }
        return map;
    }

    private static string Str(JsonElement el) =>
        el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" :
        el.ValueKind == JsonValueKind.Number ? el.GetRawText() : "";

    /// <summary>把期权链中的一行转成 FuturesQuote；昨结算价由涨跌幅反推。</summary>
    private static FuturesQuote ToQuote(string code, OptionProducts.Parsed parsed, OptionRow row)
    {
        // 涨跌幅% -> 昨结算价反推：prev = last / (1 + pct/100)。
        // pct <= -99.9 视为价格归零，昨结算价置 0（ChangePercent 有除零保护，返回 0）。
        double prevSettle = 0;
        if (row.Last > 0 && row.ChangePercent > -99.9)
        {
            prevSettle = row.Last / (1 + row.ChangePercent / 100.0);
        }

        return new FuturesQuote
        {
            Code = code,
            Name = $"{parsed.Info.Name}{(parsed.IsCall ? "购" : "沽")}{parsed.Strike}",
            Exchange = parsed.Info.ExchangeName,
            Last = row.Last,
            PrevSettle = prevSettle,
            High = row.Ask,   // 卖一价
            Low = row.Bid,    // 买一价
            OpenInterest = row.OpenInterest,
        };
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
