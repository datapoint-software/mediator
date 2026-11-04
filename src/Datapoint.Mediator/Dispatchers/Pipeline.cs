using System;

namespace Datapoint.Mediator.Dispatchers;

internal static class Pipeline
{
    // Wraps next in each behaviour, from the innermost outwards, so the first registered behaviour runs first. wrap is a
    // static lambda, so the only allocations are the closure and delegate of each behaviour's step (ADR 0006).
    public static TNext Wrap<TRequest, TNext>(
        IBehavior[] behaviors,
        TRequest request,
        TNext next,
        Func<IBehavior, TRequest, TNext, TNext> wrap)
    {
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            next = wrap(behaviors[i], request, next);
        }

        return next;
    }
}
