using System.Collections.ObjectModel;
using FuturesTicker.Models;

namespace FuturesTicker.Services;

/// <summary>
/// Merges fresh quotes into an observable collection in place (no flicker),
/// then reorders the collection to match the configured contract order.
/// </summary>
public static class QuoteMerger
{
    public static void Merge(ObservableCollection<FuturesQuote> quotes, List<FuturesQuote> fresh, IList<string> order)
    {
        var map = fresh.ToDictionary(q => q.Code);

        // Update existing in place, drop removed codes.
        for (int i = quotes.Count - 1; i >= 0; i--)
        {
            if (map.TryGetValue(quotes[i].Code, out var q))
            {
                Apply(quotes[i], q);
                map.Remove(quotes[i].Code);
            }
            else
            {
                quotes.RemoveAt(i);
            }
        }

        // Add any new codes.
        foreach (var q in map.Values)
        {
            quotes.Add(q);
        }

        // Reorder to match the configured order.
        var sorted = quotes.OrderBy(q =>
        {
            int i = order.IndexOf(q.Code);
            return i < 0 ? int.MaxValue : i;
        }).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            int cur = quotes.IndexOf(sorted[i]);
            if (cur != i) quotes.Move(cur, i);
        }
    }

    private static void Apply(FuturesQuote target, FuturesQuote src)
    {
        target.Name = src.Name;
        target.Exchange = src.Exchange;
        target.Last = src.Last;
        target.PrevSettle = src.PrevSettle;
        target.Open = src.Open;
        target.High = src.High;
        target.Low = src.Low;
        target.Volume = src.Volume;
        target.OpenInterest = src.OpenInterest;
    }
}
