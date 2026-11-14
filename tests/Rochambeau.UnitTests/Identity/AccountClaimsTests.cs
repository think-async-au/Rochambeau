using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Infrastructure;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.UnitTests.Identity;

/// <summary>Principal ↔ <see cref="AccountIdentity"/> (AC-08, accounts ADR-0004).</summary>
public class AccountClaimsTests
{
    private static readonly IOptions<IdentityOptions> _options = Options.Create(new IdentityOptions());

    [Theory]
    [InlineData(AccountRole.Player, "Player")]
    [InlineData(AccountRole.Administrator, "Administrator")]
    public async Task Principal_from_the_claims_factory_maps_to_the_account_identity(AccountRole role, string roleClaim)
    {
        var account = NewAccount(role);

        var principal = await new AccountClaimsPrincipalFactory(_options).CreateAsync(account);

        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal(IdentityConstants.ApplicationScheme, principal.Identity.AuthenticationType);
        Assert.True(principal.IsInRole(roleClaim));
        Assert.Equal(account.SecurityStamp, principal.FindFirstValue(_options.Value.ClaimsIdentity.SecurityStampClaimType));
        Assert.Equal(new AccountIdentity(account.Id, "Rocky", role), CurrentFor(principal).Current);
    }

    [Fact]
    public void Anonymous_principal_is_nobody()
    {
        Assert.Null(CurrentFor(new ClaimsPrincipal(new ClaimsIdentity())).Current);
    }

    [Fact]
    public void No_http_context_is_nobody()
    {
        Assert.Null(new CurrentAccount(new HubCallerPrincipal(), new HttpContextAccessor(), _options).Current);
    }

    [Fact]
    public void Authenticated_principal_with_missing_or_malformed_claims_is_nobody()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "not-a-guid"), new Claim(ClaimTypes.Name, "Rocky"), new Claim(ClaimTypes.Role, "Player")],
            IdentityConstants.ApplicationScheme));

        Assert.Null(CurrentFor(principal).Current);
    }

    [Fact]
    public void Account_identity_has_no_email()
    {
        Assert.DoesNotContain(
            typeof(AccountIdentity).GetProperties(),
            p => p.Name.Contains("mail", StringComparison.OrdinalIgnoreCase));
    }

    private static CurrentAccount CurrentFor(ClaimsPrincipal principal) =>
        new(new HubCallerPrincipal(), new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } }, _options);

    private static Account NewAccount(AccountRole role) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = "Rocky",
        NormalizedUserName = "ROCKY",
        Email = "user-1@example.test",
        NormalizedEmail = "USER-1@EXAMPLE.TEST",
        SecurityStamp = "stamp-1",
        Role = role,
    };
}
