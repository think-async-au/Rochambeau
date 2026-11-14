using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Rochambeau.Api.Common.Commands;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>Operator grant-admin / revoke-admin through ChangeRole — AC-13, AC-13b (HTTP side), AC-14.</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class ChangeRoleTests(PostgresFixture postgres)
{
    private static readonly Uri _me = new("/api/v1/me", UriKind.Relative);

    [Fact]
    public async Task AC13_an_account_the_operator_designated_logs_in_as_administrator()
    {
        await using var factory = new ApiFactory(postgres);
        var (username, email) = await RegisterAsync(factory);

        await IdentityTestData.PromoteToAdministratorAsync(factory.Services, username);

        using var client = IdentityApi.CreateClient(factory);
        using var logIn = await LogInAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, logIn.StatusCode);
        Assert.Equal("administrator", (await logIn.ReadJsonAsync()).GetProperty("role").GetString());
        using var me = await client.GetAsync(_me);
        Assert.Equal("administrator", (await me.ReadJsonAsync()).GetProperty("role").GetString());
    }

    [Fact]
    public async Task AC13b_a_role_change_ends_the_sessions_on_every_device()
    {
        await using var factory = new ApiFactory(postgres);
        using var deviceA = IdentityApi.CreateClient(factory);
        using var deviceB = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        var email = IdentityApi.NextEmail();
        (await IdentityApi.RegisterAsync(deviceA, username, email)).Dispose();
        (await LogInAsync(deviceB, email)).Dispose();

        await IdentityTestData.PromoteToAdministratorAsync(factory.Services, username);

        foreach (var device in new[] { deviceA, deviceB })
        {
            using var stale = await device.GetAsync(_me);
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
            Assert.Equal("identity.not_authenticated", (await stale.ReadJsonAsync()).GetProperty("errorCode").GetString());
        }

        using var again = await LogInAsync(deviceA, email);
        Assert.Equal("administrator", (await again.ReadJsonAsync()).GetProperty("role").GetString());

        await RunAsync(factory, "revoke-admin", username);
        using var revoked = await deviceA.GetAsync(_me);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using var asPlayer = await LogInAsync(deviceA, email);
        Assert.Equal("player", (await asPlayer.ReadJsonAsync()).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Granting_a_role_the_account_already_has_changes_nothing_and_keeps_sessions()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        (await IdentityApi.RegisterAsync(client, username)).Dispose();

        var (exitCode, output) = await RunAsync(factory, "revoke-admin", username);

        Assert.Equal(0, exitCode);
        Assert.Contains("nothing to do", output, StringComparison.OrdinalIgnoreCase);
        using var me = await client.GetAsync(_me);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Grant_admin_through_the_operator_command_changes_the_role()
    {
        await using var factory = new ApiFactory(postgres);
        var (username, email) = await RegisterAsync(factory);

        var (exitCode, output) = await RunAsync(factory, "grant-admin", username.ToUpperInvariant());

        Assert.Equal(0, exitCode);
        Assert.Contains(username, output, StringComparison.Ordinal);
        using var client = IdentityApi.CreateClient(factory);
        Assert.Equal("administrator", (await (await LogInAsync(client, email)).ReadJsonAsync()).GetProperty("role").GetString());
    }

    [Theory]
    [InlineData("grant-admin", "nobody_has_this", 1)]
    [InlineData("grant-admin", null, 2)]
    [InlineData("revoke-admin", null, 2)]
    public async Task Unknown_username_or_missing_argument_fails_and_changes_nothing(string command, string? username, int expected)
    {
        await using var factory = new ApiFactory(postgres);

        var (exitCode, output) = username is null ? await RunAsync(factory, command) : await RunAsync(factory, command, username);

        Assert.Equal(expected, exitCode);
        Assert.NotEmpty(output);
    }

    [Fact]
    public async Task AC14_the_portal_maps_exactly_the_contract_operations_and_none_accepts_a_role()
    {
        await using var factory = new ApiFactory(postgres);

        var api = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api", StringComparison.Ordinal) == true)
            .ToList();

        var operations = api
            .SelectMany(e => e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Select(m => $"{m} {e.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                "DELETE /api/v1/session",
                "GET /api/v1/me",
                "GET /api/v1/profiles/{username}",
                "POST /api/v1/accounts",
                "POST /api/v1/session",
            ],
            operations);

        var requestTypes = api.SelectMany(e => e.Metadata.OfType<IAcceptsMetadata>()).Select(m => m.RequestType).OfType<Type>();
        Assert.All(requestTypes, t => Assert.DoesNotContain(t.GetProperties(), p => p.Name.Contains("role", StringComparison.OrdinalIgnoreCase)));
        Assert.All(api, e => Assert.DoesNotContain(e.RoutePattern.Parameters, p => p.Name.Contains("role", StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(ApiFactory factory, params string[] args)
    {
        using var output = new StringWriter();
        var exitCode = await factory.Services.TryRunOperatorCommandAsync(args, output);
        Assert.NotNull(exitCode);
        return (exitCode.Value, output.ToString());
    }

    private static async Task<(string Username, string Email)> RegisterAsync(ApiFactory factory)
    {
        using var client = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        var email = IdentityApi.NextEmail();
        using var response = await IdentityApi.RegisterAsync(client, username, email);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (username, email);
    }

    private static Task<HttpResponseMessage> LogInAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync(new Uri("/api/v1/session", UriKind.Relative), new { email, password = IdentityApi.DefaultPassword });
}
