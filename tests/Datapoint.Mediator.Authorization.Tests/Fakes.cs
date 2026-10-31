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

public sealed record DerivedFromAnonymous : AnonymousBase;

public sealed class TestPrincipalAccessor(ClaimsPrincipal? principal) : IPrincipalAccessor
{
    public ClaimsPrincipal? Principal => principal;
}

// Allows or refuses as configured, logging its name and recording what it received.
public sealed class TestAuthorizer<TAction>(string name, bool allow, List<string> log) : IAuthorizer<TAction>
{
    public List<(TAction Action, ClaimsPrincipal Principal, CancellationToken Token)> Calls { get; } = [];

    public ValueTask<bool> IsAuthorizedAsync(TAction action, ClaimsPrincipal principal, CancellationToken ct = default)
    {
        log.Add(name);
        Calls.Add((action, principal, ct));
        return new(allow);
    }
}

public static class Principals
{
    public static ClaimsPrincipal SignedIn() => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));

    // An identity without an authentication type is not authenticated.
    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}
