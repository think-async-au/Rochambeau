using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
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
