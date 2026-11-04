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
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            log?.Add(i.ToString());
            yield return i;
        }
    }
}
