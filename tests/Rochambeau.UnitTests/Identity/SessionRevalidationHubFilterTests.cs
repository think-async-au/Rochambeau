using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rochambeau.Api.Modules.Identity.Infrastructure;

namespace Rochambeau.UnitTests.Identity;

/// <summary>SessionRevalidationHubFilter — «who is acting» inside every hub hook (AC-08, accounts ADR-0004).</summary>
public class SessionRevalidationHubFilterTests
{
    [Fact]
    public async Task AC08_on_disconnect_the_hub_sees_the_caller_as_the_current_principal()
    {
        var services = new ServiceCollection().AddScoped<HubCallerPrincipal>().BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Rocky")], "Identity.Application"));
        var context = new HubLifetimeContext(new FakeCaller(user), scope.ServiceProvider, new EmptyHub());
        IHubFilter filter = new SessionRevalidationHubFilter(
            Options.Create(new IdentityOptions()), TimeProvider.System, NullLogger<SessionRevalidationHubFilter>.Instance);
        ClaimsPrincipal? seenByHub = null;

        await filter.OnDisconnectedAsync(context, exception: null, (lifetime, _) =>
        {
            seenByHub = lifetime.ServiceProvider.GetRequiredService<HubCallerPrincipal>().User;
            return Task.CompletedTask;
        });

        Assert.Same(user, seenByHub);
    }

    private sealed class EmptyHub : Hub;

    private sealed class FakeCaller(ClaimsPrincipal user) : HubCallerContext
    {
        public override string ConnectionId => "connection-1";

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal? User => user;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }
}
