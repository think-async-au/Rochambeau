using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Rochambeau.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only: TestServer has no remote address, so a request may name one in
/// <see cref="HeaderName"/>. Lets cap and race tests give each caller its own network address.
/// </summary>
internal sealed class TestRemoteAddress : IStartupFilter
{
    public const string HeaderName = "X-Test-Remote-Address";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var value) && IPAddress.TryParse(value, out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            return nextMiddleware(context);
        });
        next(app);
    };

    private static int _next;

    /// <summary>A fresh address from the documentation range, unique within this test run.</summary>
    public static string Next()
    {
        var n = Interlocked.Increment(ref _next);
        return $"2001:db8::{n:x}";
    }
}
