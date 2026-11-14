using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>AC-15 registration cap, AC-03b race guard, QG-1 (h).</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class RegistrationCapAndRaceTests(PostgresFixture postgres)
{
    private const int _racers = 10;

    [Fact]
    public async Task AC15_sixth_registration_from_one_address_within_an_hour_is_refused_until_the_window_slides()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        using var client = IdentityApi.CreateClient(factory);
        var address = TestRemoteAddress.Next();
        for (var i = 0; i < 5; i++)
        {
            using var ok = await IdentityApi.RegisterFromAsync(client, address);
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
            time.Advance(TimeSpan.FromMinutes(1));
        }

        var refusedName = IdentityApi.NextUsername();
        using var refused = await IdentityApi.RegisterFromAsync(client, address, refusedName);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var body = await refused.ReadJsonAsync();
        Assert.Equal("identity.registration_cap", body.GetProperty("errorCode").GetString());
        Assert.Equal("Too many accounts were created from your connection recently. Try again later.", body.GetProperty("title").GetString());
        Assert.Equal(0, await CountByUsernameAsync(factory, refusedName));

        using var otherAddress = await IdentityApi.RegisterFromAsync(client, TestRemoteAddress.Next());
        Assert.Equal(HttpStatusCode.Created, otherAddress.StatusCode);

        time.Advance(TimeSpan.FromMinutes(56)); // the first registration is now 60 minutes old
        using var afterWindow = await IdentityApi.RegisterFromAsync(client, address);
        Assert.Equal(HttpStatusCode.Created, afterWindow.StatusCode);
    }

    [Fact]
    public async Task AC15_failed_attempts_do_not_count()
    {
        await using var factory = new ApiFactory(postgres, new FakeTimeProvider(DateTimeOffset.UtcNow));
        using var client = IdentityApi.CreateClient(factory);
        var address = TestRemoteAddress.Next();
        var taken = IdentityApi.NextUsername();
        (await IdentityApi.RegisterAsync(IdentityApi.CreateClient(factory), taken)).Dispose();

        for (var i = 0; i < 3; i++)
        {
            using var invalid = await IdentityApi.RegisterFromAsync(client, address, password: "short");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            using var conflict = await IdentityApi.RegisterFromAsync(client, address, taken);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        }

        for (var i = 0; i < 5; i++)
        {
            using var ok = await IdentityApi.RegisterFromAsync(client, address);
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }

        using var sixth = await IdentityApi.RegisterFromAsync(client, address);
        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
    }

    [Fact]
    public async Task AC15_registration_fails_closed_while_the_database_is_unreachable_and_consumes_no_cap_slot()
    {
        var outage = new DatabaseOutage();
        await using var factory = new ApiFactory(postgres, new FakeTimeProvider(DateTimeOffset.UtcNow));
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(outage))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Rochambeau-Client", "web");
        var address = TestRemoteAddress.Next();
        var failedName = IdentityApi.NextUsername();

        outage.Down = true;
        using var failed = await IdentityApi.RegisterFromAsync(client, address, failedName);
        outage.Down = false;

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.False(failed.Headers.Contains("Set-Cookie"));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var normalized = failedName.ToUpperInvariant();
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
                .CountAsync(a => a.NormalizedUserName == normalized));
        }

        for (var i = 0; i < 5; i++)
        {
            using var ok = await IdentityApi.RegisterFromAsync(client, address);
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }
    }

    /// <summary>Makes every new database connection fail while <see cref="Down"/> is set.</summary>
    private sealed class DatabaseOutage : DbConnectionInterceptor
    {
        public volatile bool Down;

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            System.Data.Common.DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            Down ? throw new NpgsqlException("Test: the database is unreachable.") : ValueTask.FromResult(result);

        public override InterceptionResult ConnectionOpening(
            System.Data.Common.DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            Down ? throw new NpgsqlException("Test: the database is unreachable.") : result;
    }

    [Fact]
    public async Task QG1h_ten_parallel_registrations_from_an_address_with_four_create_exactly_one_more()
    {
        await using var factory = new ApiFactory(postgres, new FakeTimeProvider(DateTimeOffset.UtcNow));
        using var client = IdentityApi.CreateClient(factory);
        var address = TestRemoteAddress.Next();
        for (var i = 0; i < 4; i++)
        {
            (await IdentityApi.RegisterFromAsync(client, address)).Dispose();
        }

        var names = Enumerable.Range(0, _racers).Select(_ => IdentityApi.NextUsername()).ToArray();
        var responses = await Task.WhenAll(names.Select(n => IdentityApi.RegisterFromAsync(client, address, n)));

        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Equal(_racers - 1, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
            var created = 0;
            foreach (var name in names)
            {
                created += await CountByUsernameAsync(factory, name);
            }

            Assert.Equal(1, created);
        }
        finally
        {
            Array.ForEach(responses, r => r.Dispose());
        }
    }

    [Fact]
    public async Task AC03b_parallel_registrations_of_one_username_in_any_case_create_exactly_one_account()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var username = "Paper" + Guid.NewGuid().ToString("N")[..10];
        var variants = Enumerable.Range(0, _racers).Select(i => i % 2 == 0 ? username : username.ToLowerInvariant());

        var responses = await Task.WhenAll(variants.Select(v => IdentityApi.RegisterFromAsync(client, TestRemoteAddress.Next(), v)));

        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            var losers = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToArray();
            Assert.All(losers, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
            foreach (var loser in losers)
            {
                Assert.Equal("identity.username_taken", (await loser.ReadJsonAsync()).GetProperty("errorCode").GetString());
            }

            Assert.Equal(1, await CountByUsernameAsync(factory, username));
        }
        finally
        {
            Array.ForEach(responses, r => r.Dispose());
        }
    }

    [Fact]
    public async Task AC03b_parallel_registrations_of_one_email_create_exactly_one_account()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var email = IdentityApi.NextEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, _racers).Select(i =>
            IdentityApi.RegisterFromAsync(client, TestRemoteAddress.Next(), email: i % 2 == 0 ? email : email.ToUpperInvariant())));

        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            var losers = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToArray();
            Assert.All(losers, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
            foreach (var loser in losers)
            {
                Assert.Equal("identity.email_unavailable", (await loser.ReadJsonAsync()).GetProperty("errorCode").GetString());
            }

            await using var scope = factory.Services.CreateAsyncScope();
            var normalized = email.ToUpperInvariant();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
                .CountAsync(a => a.NormalizedEmail == normalized));
        }
        finally
        {
            Array.ForEach(responses, r => r.Dispose());
        }
    }

    private static async Task<int> CountByUsernameAsync(ApiFactory factory, string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = username.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
            .CountAsync(a => a.NormalizedUserName == normalized);
    }
}
