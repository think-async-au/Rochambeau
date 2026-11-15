using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Rochambeau.Api.Common.Http;

namespace Rochambeau.UnitTests.Common;

/// <summary>A non-JSON request body under <c>/api</c> is <c>400 common.invalid_request</c> (contracts/openapi.yaml).</summary>
public class JsonBodyMiddlewareTests
{
    [Fact]
    public async Task A_streamed_body_without_a_content_length_is_detected_on_http2()
    {
        var http = Request("POST", "text/plain", contentLength: null, canHaveBody: true);

        var calledNext = await RunAsync(http);

        Assert.False(calledNext);
        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
        Assert.Equal("common.invalid_request", await ErrorCodeAsync(http));
    }

    [Fact]
    public async Task A_body_with_a_content_length_is_detected_without_the_detection_feature()
    {
        var http = Request("POST", "text/plain", contentLength: 5, canHaveBody: null);

        var calledNext = await RunAsync(http);

        Assert.False(calledNext);
        Assert.Equal("common.invalid_request", await ErrorCodeAsync(http));
    }

    [Fact]
    public async Task A_json_body_without_a_content_length_passes()
    {
        var http = Request("POST", "application/json; charset=utf-8", contentLength: null, canHaveBody: true);

        Assert.True(await RunAsync(http));
    }

    [Fact]
    public async Task A_request_the_server_knows_has_no_body_passes()
    {
        var http = Request("DELETE", contentType: null, contentLength: null, canHaveBody: false);

        Assert.True(await RunAsync(http));
    }

    [Fact]
    public async Task A_declared_zero_length_without_a_content_type_passes_even_when_the_server_reports_a_body()
    {
        // HTTP/2: HEADERS with content-length 0, then END_STREAM on an empty DATA frame.
        var http = Request("DELETE", contentType: null, contentLength: 0, canHaveBody: true);

        Assert.True(await RunAsync(http));
    }

    [Fact]
    public async Task An_empty_body_with_a_content_type_that_is_not_json_is_rejected()
    {
        var http = Request("POST", "text/plain", contentLength: 0, canHaveBody: false);

        var calledNext = await RunAsync(http);

        Assert.False(calledNext);
        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
        Assert.Equal("common.invalid_request", await ErrorCodeAsync(http));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task A_bodyless_method_with_a_stray_content_type_that_is_not_json_passes(string method)
    {
        // logOut, getOwnProfile and getPublicProfile document no invalid_request (contracts/openapi.yaml).
        var http = Request(method, "text/plain", contentLength: 0, canHaveBody: false);

        Assert.True(await RunAsync(http));
    }

    [Fact]
    public async Task An_empty_body_with_a_json_content_type_passes_to_the_binder()
    {
        var http = Request("POST", "application/json", contentLength: 0, canHaveBody: false);

        Assert.True(await RunAsync(http));
    }

    private static DefaultHttpContext Request(string method, string? contentType, long? contentLength, bool? canHaveBody)
    {
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddProblemDetailsWithErrorCodes().BuildServiceProvider(),
        };
        http.Request.Method = method;
        http.Request.Path = "/api/v1/session";
        http.Request.ContentType = contentType;
        http.Request.ContentLength = contentLength;
        http.Response.Body = new MemoryStream();
        if (canHaveBody is { } value)
        {
            http.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(value));
        }

        return http;
    }

    private static async Task<bool> RunAsync(HttpContext http)
    {
        var calledNext = false;
        var middleware = new JsonBodyMiddleware(_ =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(http);
        return calledNext;
    }

    private static async Task<string?> ErrorCodeAsync(HttpContext http)
    {
        http.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(http.Response.Body);
        return body.RootElement.GetProperty("errorCode").GetString();
    }

    private sealed class BodyDetection(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
