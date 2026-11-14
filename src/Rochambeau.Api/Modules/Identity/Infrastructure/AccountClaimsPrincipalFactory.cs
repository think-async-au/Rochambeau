using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Infrastructure;

/// <summary>
/// Builds the session principal from the account row alone: id, Username, security stamp and the
/// standard role claim from the <c>role</c> column (accounts ADR-0003). It never reads Identity's
/// claim or role stores, which are not mapped (accounts ADR-0001).
/// </summary>
public sealed class AccountClaimsPrincipalFactory(IOptions<IdentityOptions> options) : IUserClaimsPrincipalFactory<Account>
{
    public Task<ClaimsPrincipal> CreateAsync(Account user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var types = options.Value.ClaimsIdentity;

        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme, types.UserNameClaimType, types.RoleClaimType);
        identity.AddClaim(new Claim(types.UserIdClaimType, user.Id.ToString("D", CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(types.UserNameClaimType, user.UserName!));
        identity.AddClaim(new Claim(types.RoleClaimType, user.Role.ToString()));
        identity.AddClaim(new Claim(types.SecurityStampClaimType, user.SecurityStamp!));

        return Task.FromResult(new ClaimsPrincipal(identity));
    }
}
