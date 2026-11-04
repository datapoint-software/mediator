using System.Linq;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Fakes;
using Xunit;

namespace Datapoint.Mediator.Tests;

public sealed class ConcurrencyTests
{
    [Fact]
    public async Task ParallelFirstDispatches_OfNewTypes_ReturnCorrectResults()
    {
        // These command types are only used here, so the first dispatch of each one races to create its dispatcher.
        var provider = new FakeServiceProvider()
            .Add<ICommandHandler<Numbered<A>, int>>(new CommandHandler<Numbered<A>, int>((c, _) => new(c.Value + 1)))
            .Add<ICommandHandler<Numbered<B>, int>>(new CommandHandler<Numbered<B>, int>((c, _) => new(c.Value + 2)))
            .Add<ICommandHandler<Numbered<C>, int>>(new CommandHandler<Numbered<C>, int>((c, _) => new(c.Value + 3)))
            .Add<ICommandHandler<Numbered<D>, int>>(new CommandHandler<Numbered<D>, int>((c, _) => new(c.Value + 4)));
        var mediator = new ServiceProviderMediator(provider);

        var results = await Task.WhenAll(Enumerable.Range(0, 1000).Select(i => Task.Run(async () =>
        {
            var actual = (i % 4) switch
            {
                0 => await mediator.SendAsync(new Numbered<A>(i)),
                1 => await mediator.SendAsync(new Numbered<B>(i)),
                2 => await mediator.SendAsync(new Numbered<C>(i)),
                _ => await mediator.SendAsync(new Numbered<D>(i)),
            };
            return actual == i + (i % 4) + 1;
        }, TestContext.Current.CancellationToken)));

        Assert.All(results, Assert.True);
    }

    private sealed record Numbered<TMarker>(int Value) : ICommand<int>;

    private sealed class A;

    private sealed class B;

    private sealed class C;

    private sealed class D;
}
