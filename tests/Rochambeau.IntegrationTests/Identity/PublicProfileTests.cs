using System.Net;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>GET /api/v1/profiles/{username} — AC-10, AC-11, AC-12.</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class PublicProfileTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC11_player_opens_another_profile_by_username_in_any_case_and_sees_no_email()
    {
        await using var factory = new ApiFactory(postgres);
        using var owner = IdentityApi.CreateClient(factory);
        var scissors = "Scissors" + Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = IdentityApi.NextEmail();
        using var registered = await IdentityApi.RegisterAsync(owner, scissors, ownerEmail);
        var joinedAt = (await registered.ReadJsonAsync()).GetProperty("joinedAt").GetDateTimeOffset();
        using var viewer = IdentityApi.CreateClient(factory);
        (await IdentityApi.RegisterAsync(viewer)).Dispose();

        using var response = await viewer.GetAsync(ProfileUri(scissors.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ownerEmail, raw, StringComparison.OrdinalIgnoreCase);
        var body = await response.ReadJsonAsync();
        Assert.Equal(["joinedAt", "role", "username"], body.PropertyNames());
        Assert.Equal(scissors, body.GetProperty("username").GetString());
        Assert.Equal("player", body.GetProperty("role").GetString());
        Assert.Equal(joinedAt, body.GetProperty("joinedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task AC11_the_username_is_trimmed_before_the_lookup()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        (await IdentityApi.RegisterAsync(client, username)).Dispose();

        using var response = await client.GetAsync(ProfileUri($" {username.ToUpperInvariant()} "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(username, (await response.ReadJsonAsync()).GetProperty("username").GetString());
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("nobody_has_this")]
    [InlineData("a")]
    [InlineData("x!y")]
    public async Task AC12_unknown_partial_or_impossible_username_is_not_found(string lookup)
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var username = "Scissors" + Guid.NewGuid().ToString("N")[..8];
        (await IdentityApi.RegisterAsync(client, username)).Dispose();

        using var response = await client.GetAsync(ProfileUri(lookup == "partial" ? username[..5] : lookup));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("identity.player_not_found", body.GetProperty("errorCode").GetString());
        Assert.Equal("No player with that username exists.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task AC10_visitor_gets_no_profile_details()
    {
        await using var factory = new ApiFactory(postgres);
        using var owner = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        (await IdentityApi.RegisterAsync(owner, username)).Dispose();
        using var visitor = IdentityApi.CreateClient(factory);

        using var response = await visitor.GetAsync(ProfileUri(username));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("identity.not_authenticated", body.GetProperty("errorCode").GetString());
        Assert.DoesNotContain(username, await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private static Uri ProfileUri(string username) =>
        new($"/api/v1/profiles/{Uri.EscapeDataString(username)}", UriKind.Relative);
}
