using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary><see cref="IAccountDirectory"/>: lookup by id and by exact Username, case ignored (AC-11, AC-12).</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class AccountDirectoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Finds_by_username_ignoring_case_and_returns_it_as_typed()
    {
        await using var factory = new ApiFactory(postgres);
        var (id, username) = await SeedAsync(factory, "Scissors");

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAccountDirectory>();

        Assert.Equal(new AccountIdentity(id, username, AccountRole.Player), await directory.FindByUsernameAsync(username.ToLowerInvariant()));
        Assert.Equal(new AccountIdentity(id, username, AccountRole.Player), await directory.FindByUsernameAsync($"  {username.ToUpperInvariant()} "));
    }

    [Fact]
    public async Task Finds_by_id()
    {
        await using var factory = new ApiFactory(postgres);
        var (id, username) = await SeedAsync(factory, "Paper");

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAccountDirectory>();

        Assert.Equal(new AccountIdentity(id, username, AccountRole.Player), await directory.FindByIdAsync(id));
        Assert.Null(await directory.FindByIdAsync(Guid.CreateVersion7()));
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a-name-far-longer-than-twenty-characters")]
    public async Task Unknown_or_partial_username_is_not_found(string kind)
    {
        await using var factory = new ApiFactory(postgres);
        var (_, username) = await SeedAsync(factory, "Rocky");
        var lookup = kind switch
        {
            "partial" => username[..5],
            "unknown" => "nobody_has_this",
            _ => kind,
        };

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAccountDirectory>();

        Assert.Null(await directory.FindByUsernameAsync(lookup));
    }

    private static async Task<(Guid Id, string Username)> SeedAsync(ApiFactory factory, string prefix)
    {
        var username = $"{prefix}_{Guid.NewGuid():N}"[..20];
        var account = new Account
        {
            Id = Guid.CreateVersion7(),
            UserName = username,
            Email = $"{Guid.NewGuid():N}@example.test",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().CreateAsync(account, "test-password-8");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return (account.Id, username);
    }
}
