using System.Net.Http.Json;
using System.Text.Json;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>Test helpers that drive the real Identity endpoints (data-model.md · Test fixtures).</summary>
internal static class IdentityApi
{
    public const string DefaultPassword = "test-password-8";
    public const string SessionCookie = ".AspNetCore.Identity.Application";

    private static int _counter;

    /// <summary>An https client that sends the client header on every request, as the SPA does.</summary>
    public static HttpClient CreateClient(ApiFactory factory, string? remoteAddress = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Rochambeau-Client", "web");
        client.DefaultRequestHeaders.Add(TestRemoteAddress.HeaderName, remoteAddress ?? TestRemoteAddress.Next());
        return client;
    }

    public static string NextUsername() => $"player_{Interlocked.Increment(ref _counter)}";

    public static string NextEmail() => $"user-{Guid.NewGuid():N}@example.test";

    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string? username = null, string? email = null, string? password = null) =>
        client.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { username = username ?? NextUsername(), email = email ?? NextEmail(), password = password ?? DefaultPassword });

    /// <summary>Registers from <paramref name="remoteAddress"/>, overriding the client's default address.</summary>
    public static async Task<HttpResponseMessage> RegisterFromAsync(
        HttpClient client, string remoteAddress, string? username = null, string? email = null, string? password = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/accounts", UriKind.Relative))
        {
            Content = JsonContent.Create(new { username = username ?? NextUsername(), email = email ?? NextEmail(), password = password ?? DefaultPassword }),
        };
        request.Headers.Remove(TestRemoteAddress.HeaderName);
        request.Headers.Add(TestRemoteAddress.HeaderName, remoteAddress);
        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static string[] PropertyNames(this JsonElement element) =>
        [.. element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];
}
