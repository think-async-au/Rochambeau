using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Rochambeau.Api.Modules.Identity.Contracts;

namespace Rochambeau.IntegrationTests.Infrastructure;

/// <summary>
/// The real host plus a test-only hub at <see cref="HubPath"/> (a stand-in for Play's or Chat's hub),
/// mapped through the host's own authentication scheme and authorization policies.
/// </summary>
internal static class TestHubHost
{
    public const string HubPath = "/hubs/__test";

    public static WebApplicationFactory<Program> WithTestHub(this ApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter, TestHubStartupFilter>()));

    /// <summary>
    /// A WebSocket SignalR client over the TestServer that sends the given cookie and the client header.
    /// WebSockets, not long polling: a poll is a fresh HTTP request the cookie middleware would revalidate
    /// on its own, whereas one long-lived socket is exactly what only the hub filter can guard.
    /// </summary>
    public static HubConnection CreateHubConnection(this WebApplicationFactory<Program> factory, string? cookie) =>
        factory.CreateHubConnection(cookie, HttpTransportType.WebSockets);

    /// <summary>A SignalR client over the TestServer on the given transport, sending the cookie and the client header.</summary>
    public static HubConnection CreateHubConnection(this WebApplicationFactory<Program> factory, string? cookie, HttpTransportType transport)
    {
        var server = factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, HubPath), options =>
            {
                options.Transports = transport;
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var client = server.CreateWebSocketClient();
                    client.ConfigureRequest = request =>
                    {
                        if (cookie is not null)
                        {
                            request.Headers.Cookie = cookie;
                        }
                    };
                    return await client.ConnectAsync(context.Uri, cancellationToken);
                };
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Headers["X-Rochambeau-Client"] = "web";
                if (cookie is not null)
                {
                    options.Headers["Cookie"] = cookie;
                }
            })
            .Build();
    }

    private sealed class TestHubStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.MapWhen(context => context.Request.Path.StartsWithSegments(HubPath, StringComparison.Ordinal), branch =>
            {
                // Own route table, so the test hub never joins the real host's endpoints.
                branch.Properties.Remove("__GlobalEndpointRouteBuilder");
                branch.UseRouting();
                branch.UseAuthentication();
                branch.UseAuthorization();
                branch.UseEndpoints(endpoints => endpoints.MapHub<TestHub>(HubPath));
            });
            next(app);
        };
    }
}

/// <summary>Test-only hub; invoking <see cref="Echo"/> is «a hub call, for example a move» (SAD §6 critical flow 2).</summary>
internal sealed class TestHub(ICurrentAccount currentAccount) : Hub
{
    /// <summary>What a Play or Chat hub method would see as «who is acting» (AC-08).</summary>
    public AccountIdentity? WhoAmI() => currentAccount.Current;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "SignalR hub methods must be instance methods.")]
    public string Echo(string value) => value;
}
