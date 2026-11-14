using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Rochambeau.Api.Modules.Identity.Contracts;

namespace Rochambeau.Api.Modules.Identity.Infrastructure;

/// <summary>
/// Maps the acting principal to an <see cref="AccountIdentity"/>, or nobody: inside a hub invocation
/// the hub caller (stamp-revalidated by <see cref="SessionRevalidationHubFilter"/>), otherwise the
/// HTTP request's principal (stamp-validated and renewed by the cookie middleware, accounts ADR-0002).
/// </summary>
public sealed class CurrentAccount(
    HubCallerPrincipal hubCaller,
    IHttpContextAccessor accessor,
    IOptions<IdentityOptions> options) : ICurrentAccount
{
    public AccountIdentity? Current => From(hubCaller.User ?? accessor.HttpContext?.User, options.Value.ClaimsIdentity);

    internal static AccountIdentity? From(ClaimsPrincipal? principal, ClaimsIdentityOptions types)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return Guid.TryParse(principal.FindFirstValue(types.UserIdClaimType), out var id)
            && principal.FindFirstValue(types.UserNameClaimType) is { Length: > 0 } username
            && Enum.TryParse<AccountRole>(principal.FindFirstValue(types.RoleClaimType), ignoreCase: false, out var role)
            && Enum.IsDefined(role)
                ? new AccountIdentity(id, username, role)
                : null;
    }
}
