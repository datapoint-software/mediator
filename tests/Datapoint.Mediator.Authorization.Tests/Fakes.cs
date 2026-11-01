using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Datapoint.Mediator.Authorization.Tests;

public sealed record DeleteInvoice(int Id) : ICommand;

public sealed record CreateInvoice : ICommand<int>;

public sealed record GetInvoice : IQuery<string>;

public sealed record ListInvoices : IStreamQuery<int>;

public sealed record InvoiceCreated : IMessage;

[AllowAnonymousAccess]
public sealed record GetPrices : IQuery<int>;

[AllowAnonymousAccess]
public record AnonymousBase : ICommand;

[AllowAnonymousAccess]
public sealed record ListPrices : IStreamQuery<int>;

public sealed record DerivedFromAnonymous : AnonymousBase;

// Returns the configured decision, logging its name and recording what it received.
public sealed class TestAuthorizer<TAction>(string name, AuthorizationDecision decision, List<string> log) : IAuthorizer<TAction>
{
    public TestAuthorizer(string name, bool allow, List<string> log)
        : this(name, AuthorizationDecision.AllowIf(allow), log)
    {
    }

    public List<(TAction Action, ClaimsPrincipal Principal, CancellationToken Token)> Calls { get; } = [];

    public ValueTask<AuthorizationDecision> AuthorizeAsync(TAction action, ClaimsPrincipal principal, CancellationToken ct = default)
    {
        log.Add(name);
        Calls.Add((action, principal, ct));
        return new(decision);
    }
}

// Returns null, as a careless implementation might.
public sealed class NullAuthorizer<TAction> : IAuthorizer<TAction>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(TAction action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        new((AuthorizationDecision)null!);
}

public sealed class DelegateServiceProvider(Func<Type, object?> getService) : IServiceProvider
{
    public object? GetService(Type serviceType) => getService(serviceType);
}

public static class Principals
{
    public static ClaimsPrincipal SignedIn() => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));

    // A principal with no identity at all.
    public static ClaimsPrincipal WithoutIdentity() => new();

    // An identity without an authentication type is not authenticated.
    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}

// Records every event, logging its name; throws when configured to.
public sealed class RecordingObserver(string name, List<string> log, Exception? toThrow = null) : IAuthorizationObserver
{
    public List<AuthorizationEvent> Events { get; } = [];

    public ValueTask OnAuthorizedAsync(AuthorizationEvent authorization, CancellationToken ct = default)
    {
        log.Add(name);
        Events.Add(authorization);
        return toThrow is null ? default : ValueTask.FromException(toThrow);
    }
}

public sealed class ThrowingAuthorizer<TAction>(Exception exception) : IAuthorizer<TAction>
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(TAction action, ClaimsPrincipal principal, CancellationToken ct = default) =>
        ValueTask.FromException<AuthorizationDecision>(exception);
}

public sealed record ReadGrant(string Role, bool Watermark);
