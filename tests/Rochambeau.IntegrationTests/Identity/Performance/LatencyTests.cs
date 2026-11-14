using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Rochambeau.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace Rochambeau.IntegrationTests.Identity.Performance;

/// <summary>
/// spec §6 latency NFRs, measured exactly as the spec says: p95 of 20 timed requests after 3 warm-up
/// requests, in the integration test suite, local single instance (sad §10 QG-3).
/// </summary>
[Collection(PostgresTestGroup.Name)]
public sealed class LatencyTests(PostgresFixture postgres, ITestOutputHelper output)
{
    [Fact]
    public async Task Register_p95_is_at_most_500_ms()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);

        var p95 = await Latency.P95Async(async () =>
        {
            using var response = await IdentityApi.RegisterFromAsync(client, TestRemoteAddress.Next());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        });

        output.WriteLine($"register p95 = {p95.TotalMilliseconds:F1} ms (target <= 500 ms)");
        Assert.True(p95 <= TimeSpan.FromMilliseconds(500), $"register p95 = {p95.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public async Task Log_in_p95_is_at_most_400_ms()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        var email = IdentityApi.NextEmail();
        (await IdentityApi.RegisterAsync(client, email: email)).Dispose();

        var p95 = await Latency.P95Async(async () =>
        {
            using var response = await client.PostAsJsonAsync(
                new Uri("/api/v1/session", UriKind.Relative), new { email, password = IdentityApi.DefaultPassword });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        output.WriteLine($"log in p95 = {p95.TotalMilliseconds:F1} ms (target <= 400 ms)");
        Assert.True(p95 <= TimeSpan.FromMilliseconds(400), $"log in p95 = {p95.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public async Task View_a_profile_p95_is_at_most_150_ms()
    {
        await using var factory = new ApiFactory(postgres);
        using var owner = IdentityApi.CreateClient(factory);
        var scissors = IdentityApi.NextUsername();
        (await IdentityApi.RegisterAsync(owner, scissors)).Dispose();
        using var viewer = IdentityApi.CreateClient(factory);
        (await IdentityApi.RegisterAsync(viewer)).Dispose();
        var profile = new Uri($"/api/v1/profiles/{scissors.ToUpperInvariant()}", UriKind.Relative);

        var p95 = await Latency.P95Async(async () =>
        {
            using var response = await viewer.GetAsync(profile);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        output.WriteLine($"view a profile p95 = {p95.TotalMilliseconds:F1} ms (target <= 150 ms)");
        Assert.True(p95 <= TimeSpan.FromMilliseconds(150), $"view a profile p95 = {p95.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public void P95_of_twenty_samples_is_the_nineteenth_smallest()
    {
        var samples = Enumerable.Range(1, 20).Select(i => TimeSpan.FromMilliseconds(i * 10)).Reverse().ToList();

        Assert.Equal(TimeSpan.FromMilliseconds(190), Latency.P95(samples));
    }
}

internal static class Latency
{
    public const int WarmUps = 3;
    public const int Timed = 20;

    /// <summary>Runs <see cref="WarmUps"/> untimed then <see cref="Timed"/> timed calls; a failing call fails the test.</summary>
    public static async Task<TimeSpan> P95Async(Func<Task> request)
    {
        for (var i = 0; i < WarmUps; i++)
        {
            await request();
        }

        var samples = new List<TimeSpan>(Timed);
        for (var i = 0; i < Timed; i++)
        {
            var started = Stopwatch.GetTimestamp();
            await request();
            samples.Add(Stopwatch.GetElapsedTime(started));
        }

        return P95(samples);
    }

    /// <summary>Nearest-rank 95th percentile.</summary>
    public static TimeSpan P95(IReadOnlyCollection<TimeSpan> samples)
    {
        var rank = (int)Math.Ceiling(0.95 * samples.Count);
        return samples.Order().ElementAt(rank - 1);
    }
}
