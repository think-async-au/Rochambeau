using Microsoft.AspNetCore.Http.Features;
using Rochambeau.Api.Common.Results;

namespace Rochambeau.Api.Common.Http;

/// <summary>
/// Every request body under <c>/api</c> is JSON (contracts/openapi.yaml). A request with a body, or a
/// POST / PUT / PATCH with a Content-Type even on an empty body, whose Content-Type is not JSON is rejected with
/// <c>400 common.invalid_request</c> before authentication. Routing would
/// otherwise pick the framework's 415 endpoint, which the deny-by-default policy answers with a 401
/// for a Visitor. An endpoint that ever accepts another media type (an upload) must be exempted here.
/// </summary>
public sealed class JsonBodyMiddleware(RequestDelegate next)
{
    public static readonly Error InvalidRequest = new(
        ProblemDetailsDefaults.InvalidRequestCode,
        ProblemDetailsDefaults.InvalidRequestTitle,
        ErrorType.Validation);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && (HasBody(context) || (request.ContentType is not null && CarriesABody(request.Method)))
            && !request.HasJsonContentType())
        {
            await InvalidRequest.ToProblem().ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// The methods whose operations take a request body. Only these are refused for a non-JSON
    /// Content-Type on an empty body; a bodyless GET or DELETE with a stray Content-Type passes, as the
    /// contract documents no <c>invalid_request</c> for them.
    /// </summary>
    private static bool CarriesABody(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);

    /// <summary>
    /// A declared <c>Content-Length: 0</c> is no body, even when an HTTP/2 client ends the stream on a
    /// separate empty DATA frame (the server then reports that a body can follow). Otherwise the
    /// server's body-detection feature covers every protocol (HTTP/2 and HTTP/3 have no
    /// Transfer-Encoding, so a streamed body there carries no length). The length and
    /// Transfer-Encoding test is the fallback for a host without that feature. On a POST / PUT /
    /// PATCH, a non-JSON Content-Type is rejected even with an empty body (see <see cref="CarriesABody"/>).
    /// </summary>
    private static bool HasBody(HttpContext context)
    {
        var request = context.Request;
        if (request.ContentLength == 0)
        {
            return false;
        }

        return context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true
            || request.ContentLength > 0
            || request.Headers.TransferEncoding.Count > 0;
    }
}

public static class JsonBodyExtensions
{
    /// <summary>Adds the <see cref="JsonBodyMiddleware"/>; call before authentication.</summary>
    public static IApplicationBuilder UseJsonBodyCheck(this IApplicationBuilder app) =>
        app.UseMiddleware<JsonBodyMiddleware>();
}
