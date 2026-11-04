using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datapoint.Mediator.Tests.Shared;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datapoint.Mediator.FluentValidation.Tests;

public sealed class ValidationBehaviorTests
{
    private static readonly CreateUser ValidUser = new("Ana", "ana@example.com");

    private bool _nextCalled;

    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ValidationBehavior(null!));

        Assert.Equal("serviceProvider", exception.ParamName);
    }

    [Fact]
    public void HandleAsync_NullArguments_Throw()
    {
        var behavior = new ValidationBehavior(new ServiceCollection().BuildServiceProvider());

        BehaviorArguments.AssertNullsAreRefused(behavior, ValidUser, new AddNumbers(1, 2), new FindUser("Ana"), new ListUsers(""));
    }

    [Fact]
    public async Task Command_NoValidators_CallsNext()
    {
        var behavior = CreateBehavior(_ => { });

        await behavior.HandleAsync(new CreateUser("", ""), Next, TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task Command_Valid_CallsNext()
    {
        var behavior = CreateBehavior(s => s.AddScoped<IValidator<CreateUser>, NameValidator>());

        await behavior.HandleAsync(ValidUser, Next, TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task Command_Invalid_ThrowsWithoutCallingNext()
    {
        var behavior = CreateBehavior(s => s.AddScoped<IValidator<CreateUser>, NameValidator>());

        await Assert.ThrowsAsync<ValidationException>(
            async () => await behavior.HandleAsync(new CreateUser("", "ana@example.com"), Next, TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task Command_Invalid_ExceptionContainsFailuresFromAllValidators()
    {
        var behavior = CreateBehavior(s => s
            .AddScoped<IValidator<CreateUser>, NameValidator>()
            .AddScoped<IValidator<CreateUser>, EmailValidator>());

        var exception = await Assert.ThrowsAsync<ValidationException>(
            async () => await behavior.HandleAsync(new CreateUser("", ""), Next, TestContext.Current.CancellationToken));

        Assert.Equal(["Name", "Email"], exception.Errors.Select(e => e.PropertyName));
    }

    [Fact]
    public async Task CommandWithResult_Invalid_ThrowsWithoutCallingNext()
    {
        var behavior = CreateBehavior(s => s.AddScoped<IValidator<AddNumbers>, AddNumbersValidator>());

        await Assert.ThrowsAsync<ValidationException>(
            async () => await behavior.HandleAsync(new AddNumbers(-1, 1), NextWithResult(3), TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task CommandWithResult_Valid_ReturnsNextResult()
    {
        var behavior = CreateBehavior(s => s.AddScoped<IValidator<AddNumbers>, AddNumbersValidator>());

        var result = await behavior.HandleAsync(new AddNumbers(1, 2), NextWithResult(3), TestContext.Current.CancellationToken);

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task Query_Invalid_ThrowsWithoutCallingNext()
    {
        var behavior = CreateBehavior(s => s.AddScoped<IValidator<FindUser>, FindUserValidator>());

        await Assert.ThrowsAsync<ValidationException>(
            async () => await behavior.HandleAsync(new FindUser(""), NextWithResult("Ana"), TestContext.Current.CancellationToken));

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task Query_Valid_ReturnsNextResult()
    {
        var behavior = CreateBehavior(s => s.AddScoped<IValidator<FindUser>, FindUserValidator>());

        var result = await behavior.HandleAsync(new FindUser("Ana"), NextWithResult("Ana"), TestContext.Current.CancellationToken);

        Assert.Equal("Ana", result);
    }

    [Fact]
    public async Task StreamQuery_Invalid_ThrowsOnEnumerationWithoutCallingNext()
    {
        var behavior = CreateBehavior(s => s.AddScoped<IValidator<ListUsers>, ListUsersValidator>());

        var results = behavior.HandleAsync(new ListUsers(""), NextStream, TestContext.Current.CancellationToken);
        Assert.False(_nextCalled);

        await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await foreach (var _ in results)
            {
            }
        });

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task StreamQuery_Valid_ReturnsNextResults()
    {
        var behavior = CreateBehavior(s => s.AddScoped<IValidator<ListUsers>, ListUsersValidator>());
        var results = new List<string>();

        await foreach (var user in behavior.HandleAsync(new ListUsers("a"), NextStream, TestContext.Current.CancellationToken))
        {
            results.Add(user);
        }

        Assert.Equal(["Ana", "Rui"], results);
    }

    [Fact]
    public void StreamQuery_NoValidators_ReturnsNextDirectly()
    {
        var behavior = CreateBehavior(_ => { });
        var expected = NextStream(TestContext.Current.CancellationToken);

        var results = behavior.HandleAsync(new ListUsers(""), _ => expected, TestContext.Current.CancellationToken);

        Assert.Same(expected, results);
    }

    [Fact]
    public async Task Message_IsNotValidated()
    {
        // The message overload is a default interface method, so it is called through IBehavior, as the mediator does.
        IBehavior behavior = CreateBehavior(s => s.AddScoped<IValidator<UserCreated>, UserCreatedValidator>());

        await behavior.HandleAsync(new UserCreated(""), Next, TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task Validators_RunSequentiallyInRegistrationOrder()
    {
        var log = new List<string>();
        var tokens = new List<CancellationToken>();
        // The first validator completes asynchronously, so running them concurrently would log "second" first.
        var behavior = CreateBehavior(s => s
            .AddSingleton<IValidator<CreateUser>>(new LoggingValidator("first", yieldFirst: true, log, tokens))
            .AddSingleton<IValidator<CreateUser>>(new LoggingValidator("second", yieldFirst: false, log, tokens)));

        await behavior.HandleAsync(ValidUser, Next, TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], log);
    }

    [Fact]
    public async Task Validators_ReceiveToken()
    {
        using var cts = new CancellationTokenSource();
        var tokens = new List<CancellationToken>();
        var behavior = CreateBehavior(s =>
            s.AddSingleton<IValidator<CreateUser>>(new LoggingValidator("only", yieldFirst: false, [], tokens)));

        await behavior.HandleAsync(ValidUser, Next, cts.Token);

        Assert.Equal([cts.Token], tokens);
    }

    [Fact]
    public async Task NullValidators_AreTreatedAsNone()
    {
        var behavior = new ValidationBehavior(new DelegateServiceProvider(_ => null));

        await behavior.HandleAsync(new CreateUser("", ""), Next, TestContext.Current.CancellationToken);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task NonArrayValidators_AreUsed()
    {
        var validators = new List<IValidator<CreateUser>> { new NameValidator() };
        var behavior = new ValidationBehavior(new DelegateServiceProvider(
            type => type == typeof(IEnumerable<IValidator<CreateUser>>) ? validators : null));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await behavior.HandleAsync(new CreateUser("", "ana@example.com"), Next, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScopedValidator_ResolvesFromScope()
    {
        await using var provider = new ServiceCollection()
            .AddScoped<ScopedDependency>()
            .AddScoped<IValidator<CreateUser>, ScopedDependencyValidator>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var behavior = new ValidationBehavior(scope.ServiceProvider);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await behavior.HandleAsync(new CreateUser("", "ana@example.com"), Next, TestContext.Current.CancellationToken));
    }

    private static ValidationBehavior CreateBehavior(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new ValidationBehavior(services.BuildServiceProvider());
    }

    private ValueTask Next(CancellationToken cancellationToken)
    {
        _nextCalled = true;
        return default;
    }

    private async IAsyncEnumerable<string> NextStream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _nextCalled = true;
        await Task.Yield();
        yield return "Ana";
        yield return "Rui";
    }

    private Func<CancellationToken, ValueTask<TResult>> NextWithResult<TResult>(TResult result) => _ =>
    {
        _nextCalled = true;
        return new(result);
    };
}
