using Rochambeau.Api.Common.Results;

namespace Rochambeau.Api.Common.Http;

/// <summary>
/// CSRF barrier (accounts ADR-0005): every unsafe-method request under <c>/api</c> and every SignalR
/// negotiate under <c>/hubs</c> must carry a non-empty <c>X-Rochambeau-Client</c> header. The value
/// is not interpreted. A missing header is rejected before any handler runs.
/// </summary>
public sealed class ClientHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Rochambeau-Client";

    public static readonly Error ClientHeaderRequired = new(
        "common.client_header_required",
        "The X-Rochambeau-Client header is required.",
        ErrorType.Validation);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsGuarded(context.Request) && !HasClientHeader(context.Request))
        {
            await ClientHeaderRequired.ToProblem().ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsGuarded(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return !(HttpMethods.IsGet(request.Method)
                || HttpMethods.IsHead(request.Method)
                || HttpMethods.IsOptions(request.Method)
                || HttpMethods.IsTrace(request.Method));
        }

        return request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
            && request.Path.Value!.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasClientHeader(HttpRequest request) =>
        request.Headers.TryGetValue(HeaderName, out var values)
        && values.Any(v => !string.IsNullOrWhiteSpace(v));
}

public static class ClientHeaderExtensions
{
    /// <summary>Adds the <see cref="ClientHeaderMiddleware"/>; call before the endpoint mappings.</summary>
    public static IApplicationBuilder UseClientHeaderCheck(this IApplicationBuilder app) =>
        app.UseMiddleware<ClientHeaderMiddleware>();
}
