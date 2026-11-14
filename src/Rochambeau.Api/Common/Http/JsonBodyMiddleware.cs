using Microsoft.AspNetCore.Http.Features;
using Rochambeau.Api.Common.Results;

namespace Rochambeau.Api.Common.Http;

/// <summary>
/// Every request body under <c>/api</c> is JSON (contracts/openapi.yaml). A body with any other
/// Content-Type is rejected with <c>400 common.invalid_request</c> before authentication. Routing would
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
            && HasBody(context)
            && !request.HasJsonContentType())
        {
            await InvalidRequest.ToProblem().ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// The server's body-detection feature covers every protocol (HTTP/2 and HTTP/3 have no
    /// Transfer-Encoding, so a streamed body there carries no length). The length and
    /// Transfer-Encoding test is the fallback for a host without that feature.
    /// </summary>
    private static bool HasBody(HttpContext context)
    {
        var request = context.Request;
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
