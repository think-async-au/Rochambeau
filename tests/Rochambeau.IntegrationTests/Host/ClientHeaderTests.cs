using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Host;

/// <summary>accounts ADR-0005: unsafe requests under /api (and SignalR negotiate) need X-Rochambeau-Client.</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class ClientHeaderTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Unsafe_api_request_without_the_header_is_rejected_with_400(string method)
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/anything");

        using var response = await client.SendAsync(request);

        await AssertClientHeaderRequired(response);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_or_whitespace_header_counts_as_missing(string value)
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/anything");
        request.Headers.TryAddWithoutValidation("X-Rochambeau-Client", value);

        using var response = await client.SendAsync(request);

        await AssertClientHeaderRequired(response);
    }

    [Fact]
    public async Task Signalr_negotiate_without_the_header_is_rejected()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(new Uri("/hubs/play/negotiate?negotiateVersion=1", UriKind.Relative), null);

        await AssertClientHeaderRequired(response);
    }

    [Fact]
    public async Task Unsafe_api_request_with_any_non_empty_header_passes_the_barrier()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/anything");
        request.Headers.Add("X-Rochambeau-Client", "anything-at-all");

        using var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Safe_requests_without_the_header_are_not_checked()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        using var api = await client.GetAsync(new Uri("/api/v1/anything", UriKind.Relative));
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.NotEqual(HttpStatusCode.BadRequest, api.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static async Task AssertClientHeaderRequired(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("common.client_header_required", body.GetProperty("errorCode").GetString());
        Assert.Equal("The X-Rochambeau-Client header is required.", body.GetProperty("title").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
    }
}
