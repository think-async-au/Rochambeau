using System.Security.Claims;
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
    ILogger<SessionRevalidationHubFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        if (!await IsSessionValidAsync(invocationContext.Context, invocationContext.ServiceProvider).ConfigureAwait(false))
        {
            invocationContext.Context.Abort();
            throw new HubException("The session has ended. Log in again.");
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

        context.ServiceProvider.GetRequiredService<HubCallerPrincipal>().User = context.Context.User;
        await next(context).ConfigureAwait(false);
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
}
