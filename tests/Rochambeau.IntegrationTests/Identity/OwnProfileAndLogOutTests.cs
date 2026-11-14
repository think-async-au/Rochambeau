using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>GET /api/v1/me and DELETE /api/v1/session — AC-07, AC-08 (session lifetime), AC-09, AC-10.</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class OwnProfileAndLogOutTests(PostgresFixture postgres)
{
    private static readonly Uri _me = new("/api/v1/me", UriKind.Relative);
    private static readonly Uri _session = new("/api/v1/session", UriKind.Relative);

    [Fact]
    public async Task AC09_logged_in_player_sees_username_email_join_date_and_role()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        var email = IdentityApi.NextEmail();
        using var registered = await IdentityApi.RegisterAsync(client, username, email);
        var joinedAt = (await registered.ReadJsonAsync()).GetProperty("joinedAt").GetDateTimeOffset();

        using var response = await client.GetAsync(_me);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(["email", "joinedAt", "role", "username"], body.PropertyNames());
        Assert.Equal(username, body.GetProperty("username").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("player", body.GetProperty("role").GetString());
        Assert.Equal(joinedAt, body.GetProperty("joinedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task AC10_visitor_gets_no_profile_details_and_is_asked_to_log_in()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);

        using var response = await client.GetAsync(_me);

        await AssertNotAuthenticatedAsync(response);
    }

    [Fact]
    public async Task AC07_log_out_ends_the_session_in_this_browser_only()
    {
        await using var factory = new ApiFactory(postgres);
        using var deviceA = IdentityApi.CreateClient(factory);
        using var deviceB = IdentityApi.CreateClient(factory);
        var email = IdentityApi.NextEmail();
        (await IdentityApi.RegisterAsync(deviceA, email: email)).Dispose();
        (await deviceB.PostAsJsonAsync(_session, new { email, password = IdentityApi.DefaultPassword })).Dispose();

        using var logOut = await deviceA.DeleteAsync(_session);

        Assert.Equal(HttpStatusCode.NoContent, logOut.StatusCode);
        var cleared = Assert.Single(logOut.Headers.GetValues("Set-Cookie"), c => c.StartsWith(IdentityApi.SessionCookie + "=", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        await AssertNotAuthenticatedAsync(await deviceA.GetAsync(_me));
        using var other = await deviceB.GetAsync(_me);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task Log_out_without_a_session_is_not_authenticated()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);

        await AssertNotAuthenticatedAsync(await client.DeleteAsync(_session));
    }

    [Fact]
    public async Task Log_out_without_the_client_header_is_rejected_even_with_a_valid_cookie()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        (await IdentityApi.RegisterAsync(client)).Dispose();
        client.DefaultRequestHeaders.Remove("X-Rochambeau-Client");

        using var response = await client.DeleteAsync(_session);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("common.client_header_required", (await response.ReadJsonAsync()).GetProperty("errorCode").GetString());
        using var stillLoggedIn = await client.GetAsync(_me);
        Assert.Equal(HttpStatusCode.OK, stillLoggedIn.StatusCode);
    }

    [Fact]
    public async Task AC08_session_lasts_at_least_three_and_a_half_and_at_most_seven_days_after_the_last_activity()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        using var client = IdentityApi.CreateClient(factory);
        (await IdentityApi.RegisterAsync(client)).Dispose();

        time.Advance(TimeSpan.FromDays(3.5) - TimeSpan.FromMinutes(1));
        using var stillIn = await client.GetAsync(_me);
        Assert.Equal(HttpStatusCode.OK, stillIn.StatusCode);

        time.Advance(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1));
        using var renewed = await client.GetAsync(_me);
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);

        time.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));
        await AssertNotAuthenticatedAsync(await client.GetAsync(_me));
    }

    private static async Task AssertNotAuthenticatedAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
            var body = await response.ReadJsonAsync();
            Assert.Equal("identity.not_authenticated", body.GetProperty("errorCode").GetString());
            Assert.Equal("Log in or register first.", body.GetProperty("title").GetString());
            Assert.False(body.TryGetProperty("username", out _));
            Assert.False(body.TryGetProperty("email", out _));
        }
    }
}
