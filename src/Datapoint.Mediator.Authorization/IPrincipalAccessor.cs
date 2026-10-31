using System.Security.Claims;

namespace Datapoint.Mediator.Authorization;

/// <summary>
/// Provides the user on whose behalf commands and queries are run.
/// </summary>
/// <remarks>
/// Each host implements this interface and registers it as a scoped service: an API returns the request's user, a CLI
/// the signed-in user or service account, and an MCP server the user the server acts for.
/// </remarks>
public interface IPrincipalAccessor
{
    /// <summary>
    /// Gets the current user, or <see langword="null"/> when there is none.
    /// </summary>
    ClaimsPrincipal? Principal { get; }
}
