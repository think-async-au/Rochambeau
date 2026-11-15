using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>POST /api/v1/accounts — AC-01, AC-02, AC-03, AC-14.</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class RegisterTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC01_valid_registration_creates_a_player_logs_in_and_returns_the_own_profile()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        using var client = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        var email = IdentityApi.NextEmail();

        using var response = await IdentityApi.RegisterAsync(client, $"  {username} ", $" {email} ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/me", response.Headers.Location?.OriginalString);
        var body = await response.ReadJsonAsync();
        Assert.Equal(["email", "joinedAt", "role", "username"], body.PropertyNames());
        Assert.Equal(username, body.GetProperty("username").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("player", body.GetProperty("role").GetString());
        Assert.Equal(time.GetUtcNow(), body.GetProperty("joinedAt").GetDateTimeOffset(), TimeSpan.FromMilliseconds(1));

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(IdentityApi.SessionCookie + "=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);

        var stored = await FindAsync(factory, username);
        Assert.NotNull(stored);
        Assert.Equal(AccountRole.Player, stored.Role);
        Assert.Equal(7, stored.Id.Version);
        Assert.NotEqual(IdentityApi.DefaultPassword, stored.PasswordHash);
    }

    [Fact]
    public async Task AC02_every_offending_field_is_reported_at_once_and_no_account_is_created()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        const string password = "short";

        using var response = await IdentityApi.RegisterAsync(client, "ab", "not-an-email", password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("identity.validation_failed", body.GetProperty("errorCode").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal(["email", "password", "username"], errors.PropertyNames());
        Assert.Equal("The password must be at least 8 characters.", errors.GetProperty("password")[0].GetString());
        Assert.DoesNotContain(password, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Null(await FindAsync(factory, "ab"));
    }

    [Theory]
    [InlineData("Rоcky", null, null, "username")]
    [InlineData(null, null, "1234567", "password")]
    [InlineData(null, "no-at-sign.example.test", null, "email")]
    public async Task AC02_single_offending_field_is_reported(string? username, string? email, string? password, string field)
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);

        using var response = await IdentityApi.RegisterAsync(client, username, email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([field], (await response.ReadJsonAsync()).GetProperty("errors").PropertyNames());
    }

    [Theory]
    [InlineData("email")]
    [InlineData("password")]
    public async Task AC02_a_missing_field_is_reported_as_that_field_and_no_account_is_created(string missing)
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        var fields = new Dictionary<string, string>
        {
            ["username"] = username,
            ["email"] = IdentityApi.NextEmail(),
            ["password"] = IdentityApi.DefaultPassword,
        };
        fields.Remove(missing);

        // A raw body: the property is absent, not sent as null.
        using var response = await client.PostAsJsonAsync(new Uri("/api/v1/accounts", UriKind.Relative), fields);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("identity.validation_failed", body.GetProperty("errorCode").GetString());
        Assert.Equal([missing], body.GetProperty("errors").PropertyNames());
        Assert.Null(await FindAsync(factory, username));
    }

    [Fact]
    public async Task AC02_overlong_email_is_a_field_error_not_a_server_error()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var email = new string('a', 64) + "@" + new string('b', 177) + ".example.test"; // 255

        using var response = await IdentityApi.RegisterAsync(client, email: email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["email"], (await response.ReadJsonAsync()).GetProperty("errors").PropertyNames());
    }

    [Fact]
    public async Task AC03_username_taken_in_any_letter_case_is_a_conflict_that_names_no_holder()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var username = "Rocky" + Guid.NewGuid().ToString("N")[..8];
        var holderEmail = IdentityApi.NextEmail();
        (await IdentityApi.RegisterAsync(client, username, holderEmail)).Dispose();

        using var other = IdentityApi.CreateClient(factory);
        using var response = await IdentityApi.RegisterAsync(other, username.ToLowerInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var body = await response.ReadJsonAsync();
        Assert.Equal("identity.username_taken", body.GetProperty("errorCode").GetString());
        Assert.Equal("This username is already taken.", body.GetProperty("title").GetString());
        Assert.DoesNotContain(holderEmail, text, StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(1, await CountAsync(factory, username));
    }

    [Fact]
    public async Task AC03_email_already_used_in_any_letter_case_is_a_conflict_that_names_no_holder()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var holder = IdentityApi.NextUsername();
        var email = IdentityApi.NextEmail();
        (await IdentityApi.RegisterAsync(client, holder, email)).Dispose();

        using var other = IdentityApi.CreateClient(factory);
        var challenger = IdentityApi.NextUsername();
        using var response = await IdentityApi.RegisterAsync(other, challenger, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("identity.email_unavailable", body.GetProperty("errorCode").GetString());
        Assert.Equal("This email address cannot be used.", body.GetProperty("title").GetString());
        Assert.DoesNotContain(holder, await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(await FindAsync(factory, challenger));
    }

    [Fact]
    public async Task AC14_a_role_in_the_body_is_ignored_and_the_account_is_a_player()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();

        using var content = new StringContent(
            $$"""{"username":"{{username}}","email":"{{IdentityApi.NextEmail()}}","password":"{{IdentityApi.DefaultPassword}}","role":"administrator","Role":"Administrator"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        using var response = await client.PostAsync(new Uri("/api/v1/accounts", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("player", (await response.ReadJsonAsync()).GetProperty("role").GetString());
        Assert.Equal(AccountRole.Player, (await FindAsync(factory, username))!.Role);
    }

    [Fact]
    public async Task Registration_without_the_client_header_is_rejected()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var username = IdentityApi.NextUsername();

        using var response = await IdentityApi.RegisterAsync(client, username);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("common.client_header_required", (await response.ReadJsonAsync()).GetProperty("errorCode").GetString());
        Assert.Null(await FindAsync(factory, username));
    }

    private static async Task<Account?> FindAsync(ApiFactory factory, string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = username.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.NormalizedUserName == normalized);
    }

    private static async Task<int> CountAsync(ApiFactory factory, string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = username.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
            .CountAsync(a => a.NormalizedUserName == normalized);
    }
}
