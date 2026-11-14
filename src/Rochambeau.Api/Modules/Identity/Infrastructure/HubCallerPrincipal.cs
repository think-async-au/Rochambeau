using System.Security.Claims;

namespace Rochambeau.Api.Modules.Identity.Infrastructure;

/// <summary>
/// The hub caller's principal for the current hub scope (accounts ADR-0004). Set by
/// <see cref="SessionRevalidationHubFilter"/> on connect, on every invocation and on disconnect, because inside a hub the HTTP
/// request that opened the connection may be long gone (long polling, server-sent events), so
/// <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/> cannot be trusted there.
/// </summary>
public sealed class HubCallerPrincipal
{
    public ClaimsPrincipal? User { get; set; }
}
