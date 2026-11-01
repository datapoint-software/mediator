using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Datapoint.Mediator.Authorization.AspNetCore;

/// <summary>
/// Provides the user of the current ASP.NET Core request.
/// </summary>
/// <remarks>
/// <para>
/// The user is set by ASP.NET Core authentication, so register <c>UseAuthentication</c> before the endpoints that send
/// commands and queries. An anonymous request has a user without an authenticated identity, which the authorization
/// behaviour treats as no user.
/// </para>
/// <para>
/// Outside a request, such as in a background service, <see cref="Principal"/> is <see langword="null"/>.
/// </para>
/// </remarks>
public sealed class HttpPrincipalAccessor : IPrincipalAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpPrincipalAccessor"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Provides the current request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/>.</exception>
    public HttpPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;
}
