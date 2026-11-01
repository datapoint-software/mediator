using System.Security.Claims;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Provides the same user for every command and query.
/// </summary>
/// <remarks>
/// Use it in hosts that act for a single user, such as a CLI after the user signs in, an MCP server over stdio, or a
/// background service with its own service account. Pass <see langword="null"/> when no one is signed in, so that
/// actions that need a user fail with <see cref="AuthenticationRequiredException"/>.
/// </remarks>
/// <param name="principal">The user, or <see langword="null"/> when there is none.</param>
public sealed class FixedPrincipalAccessor(ClaimsPrincipal? principal) : IPrincipalAccessor
{
    /// <inheritdoc/>
    public ClaimsPrincipal? Principal { get; } = principal;
}
