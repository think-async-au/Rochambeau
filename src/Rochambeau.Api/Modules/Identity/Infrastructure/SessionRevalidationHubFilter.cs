using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Infrastructure;

/// <summary>
/// Re-checks the security stamp on connect and on every hub method invocation, for every hub
/// (accounts ADR-0004). A long-lived WebSocket never passes the cookie middleware again, so without
/// this a role change would not reach an open connection (AC-13b). One primary-key read per call.
/// </summary>
public sealed partial class SessionRevalidationHubFilter(
    IOptions<IdentityOptions> options,
    TimeProvider time,
    ILogger<SessionRevalidationHubFilter> logger) : IHubFilter
{
    // Per-connection sliding expiry (AC-08): an open socket never passes the cookie middleware again,
    // so the connection carries its own «logged out 7 days after the last activity» deadline.
    private static readonly object _expiresAtKey = new();

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        var caller = invocationContext.Context;
        if (!HasNotExpired(caller)
            || !await IsSessionValidAsync(caller, invocationContext.ServiceProvider).ConfigureAwait(false))
        {
            caller.Abort();
            throw new HubException("The session has ended. Log in again.");
        }

        // A hub invocation is activity: it slides the connection's expiry like a request slides the cookie.
        if (caller.Items.ContainsKey(_expiresAtKey))
        {
            caller.Items[_expiresAtKey] = time.GetUtcNow() + SessionCookie.Lifetime;
        }

        // ICurrentAccount inside the hub method reads the caller, not a possibly-ended HTTP request.
        invocationContext.ServiceProvider.GetRequiredService<HubCallerPrincipal>().User = invocationContext.Context.User;
        return await next(invocationContext).ConfigureAwait(false);
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!await IsSessionValidAsync(context.Context, context.ServiceProvider).ConfigureAwait(false))
        {
            context.Context.Abort();
            return;
        }

        if (context.Context.User?.Identity?.IsAuthenticated == true)
        {
            context.Context.Items[_expiresAtKey] = TicketExpiry(context.Context) ?? time.GetUtcNow() + SessionCookie.Lifetime;
        }

        context.ServiceProvider.GetRequiredService<HubCallerPrincipal>().User = context.Context.User;
        await next(context).ConfigureAwait(false);
    }

    /// <summary>The connecting request's cookie-ticket expiry, already slid by the cookie middleware.</summary>
    private static DateTimeOffset? TicketExpiry(HubCallerContext caller) =>
        caller.GetHttpContext()?.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult?.Properties?.ExpiresUtc;

    private bool HasNotExpired(HubCallerContext caller)
    {
        if (caller.Items.TryGetValue(_expiresAtKey, out var value) && value is DateTimeOffset expiresAt
            && time.GetUtcNow() >= expiresAt)
        {
            LogExpired();
            return false;
        }

        return true;
    }

    private async Task<bool> IsSessionValidAsync(HubCallerContext caller, IServiceProvider services)
    {
        var user = caller.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            // Nobody to revalidate; whether a Visitor may use a hub is the authorization policy's call.
            return true;
        }

        var types = options.Value.ClaimsIdentity;
        var stamp = user.FindFirstValue(types.SecurityStampClaimType);
        var valid = Guid.TryParse(user.FindFirstValue(types.UserIdClaimType), out var accountId)
            && stamp is not null
            && await services.GetRequiredService<AppDbContext>().Set<Account>()
                .AnyAsync(a => a.Id == accountId && a.SecurityStamp == stamp, caller.ConnectionAborted)
                .ConfigureAwait(false);

        if (!valid)
        {
            LogAborted(accountId);
        }

        return valid;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Hub connection of account {AccountId} aborted after revocation")]
    private partial void LogAborted(Guid accountId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hub connection aborted after 7 days without activity")]
    private partial void LogExpired();
}
