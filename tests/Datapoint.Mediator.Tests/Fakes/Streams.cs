using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Tests.Fakes;

public static class Streams
{
    // Yields 1..count, logging each item, and observes cancellation between items.
    public static async IAsyncEnumerable<int> CountTo(
        int count,
        List<string>? log = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 1; i <= count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            log?.Add(i.ToString());
            yield return i;
        }
    }

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
    {
        var list = new List<T>();

        await foreach (var item in source.WithCancellation(ct))
        {
            list.Add(item);
        }

        return list;
    }
}
