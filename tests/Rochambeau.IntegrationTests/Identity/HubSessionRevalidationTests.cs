using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>
/// SessionRevalidationHubFilter — AC-13b over a real-time connection, AC-08. The test hub opts in to
/// nothing, so these tests also prove the filter is registered globally for every hub.
/// </summary>
[Collection(PostgresTestGroup.Name)]
public sealed class HubSessionRevalidationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Logged_in_player_connects_and_invokes()
    {
        await using var factory = new ApiFactory(postgres);
        await using var host = factory.WithTestHub();
        var (_, cookie) = await RegisterAsync(host);
        await using var connection = host.CreateHubConnection(cookie);

        await connection.StartAsync();

        Assert.Equal("rock", await connection.InvokeAsync<string>("Echo", "rock"));
    }

    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.LongPolling)]
    public async Task AC08_a_hub_invocation_sees_the_logged_in_player_as_the_current_account(HttpTransportType transport)
    {
        await using var factory = new ApiFactory(postgres);
        await using var host = factory.WithTestHub();
        var (username, cookie) = await RegisterAsync(host);
        await using var connection = host.CreateHubConnection(cookie, transport);
        await connection.StartAsync();

        var first = await connection.InvokeAsync<AccountIdentity?>("WhoAmI");
        var second = await connection.InvokeAsync<AccountIdentity?>("WhoAmI");

        Assert.NotNull(first);
        Assert.Equal(username, first.Username);
        Assert.Equal(AccountRole.Player, first.Role);
        Assert.NotEqual(Guid.Empty, first.AccountId);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.LongPolling)]
    public async Task AC08_on_disconnect_the_hub_sees_the_player_who_left_as_the_current_account(HttpTransportType transport)
    {
        await using var factory = new ApiFactory(postgres);
        await using var host = factory.WithTestHub();
        var (username, cookie) = await RegisterAsync(host);
        await using var connection = host.CreateHubConnection(cookie, transport);
        await connection.StartAsync();
        // A completed call proves the server finished OnConnectedAsync; SignalR runs a hub's
        // OnDisconnectedAsync only for a connection whose OnConnectedAsync completed.
        Assert.Equal("rock", await connection.InvokeAsync<string>("Echo", "rock"));

        await connection.StopAsync();

        var seen = await host.Services.GetRequiredService<TestHubDisconnects>().Seen.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(seen);
        Assert.Equal(username, seen.Username);
        Assert.Equal(AccountRole.Player, seen.Role);
    }

    [Fact]
    public async Task AC08_an_invocation_after_7_days_without_activity_is_refused_and_closes_the_connection()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        await using var host = factory.WithTestHub();
        var (_, cookie) = await RegisterAsync(host);
        await using var connection = host.CreateHubConnection(cookie);
        var closed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += error =>
        {
            closed.TrySetResult(error);
            return Task.CompletedTask;
        };
        await connection.StartAsync();

        time.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));

        await Assert.ThrowsAnyAsync<Exception>(() => connection.InvokeAsync<AccountIdentity?>("WhoAmI"));
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    [Fact]
    public async Task AC08_hub_invocations_are_activity_and_keep_the_connection_logged_in()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        await using var host = factory.WithTestHub();
        var (username, cookie) = await RegisterAsync(host);
        await using var connection = host.CreateHubConnection(cookie);
        await connection.StartAsync();

        time.Advance(TimeSpan.FromDays(3.5) - TimeSpan.FromMinutes(1));
        Assert.Equal(username, (await connection.InvokeAsync<AccountIdentity?>("WhoAmI"))?.Username);

        time.Advance(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1));
        Assert.Equal(username, (await connection.InvokeAsync<AccountIdentity?>("WhoAmI"))?.Username);
    }

    [Fact]
    public async Task AC08_opening_a_real_time_connection_counts_as_activity()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        await using var host = factory.WithTestHub();
        var (username, cookie) = await RegisterAsync(host);
        using var browser = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        time.Advance(TimeSpan.FromDays(6));
        using var negotiate = new HttpRequestMessage(HttpMethod.Post, new Uri($"{TestHubHost.HubPath}/negotiate?negotiateVersion=1", UriKind.Relative));
        negotiate.Headers.Add("Cookie", cookie);
        negotiate.Headers.Add("X-Rochambeau-Client", "web");
        using var negotiated = await browser.SendAsync(negotiate);
        Assert.Equal(HttpStatusCode.OK, negotiated.StatusCode);
        var renewed = Assert.Single(negotiated.Headers.GetValues("Set-Cookie"), c => c.StartsWith(IdentityApi.SessionCookie + "=", StringComparison.Ordinal)).Split(';')[0];

        time.Advance(TimeSpan.FromDays(6));
        using var withRenewed = await GetMeAsync(browser, renewed);
        using var withOriginal = await GetMeAsync(browser, cookie);

        Assert.Equal(HttpStatusCode.OK, withRenewed.StatusCode);
        Assert.Equal(username, (await withRenewed.ReadJsonAsync()).GetProperty("username").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, withOriginal.StatusCode);
    }

    [Fact]
    public async Task AC13b_the_first_invocation_after_a_role_change_closes_the_connection()
    {
        await using var factory = new ApiFactory(postgres);
        await using var host = factory.WithTestHub();
        var (username, cookie) = await RegisterAsync(host);
        await using var connection = host.CreateHubConnection(cookie);
        var closed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += error =>
        {
            closed.TrySetResult(error);
            return Task.CompletedTask;
        };
        await connection.StartAsync();
        Assert.Equal("paper", await connection.InvokeAsync<string>("Echo", "paper"));

        await IdentityTestData.PromoteToAdministratorAsync(host.Services, username);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.InvokeAsync<string>("Echo", "scissors"));
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    [Fact]
    public async Task Anonymous_negotiate_is_not_authenticated()
    {
        await using var factory = new ApiFactory(postgres);
        await using var host = factory.WithTestHub();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Rochambeau-Client", "web");

        using var response = await client.PostAsync(new Uri($"{TestHubHost.HubPath}/negotiate?negotiateVersion=1", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<HttpResponseMessage> GetMeAsync(HttpClient browser, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/me", UriKind.Relative));
        request.Headers.Add("Cookie", cookie);
        return browser.SendAsync(request);
    }

    private static async Task<(string Username, string Cookie)> RegisterAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Rochambeau-Client", "web");
        var username = IdentityApi.NextUsername();
        using var response = await IdentityApi.RegisterAsync(client, username);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(IdentityApi.SessionCookie + "=", StringComparison.Ordinal));
        return (username, setCookie.Split(';')[0]);
    }
}
