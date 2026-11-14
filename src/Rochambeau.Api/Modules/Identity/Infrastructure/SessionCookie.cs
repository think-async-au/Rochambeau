using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Rochambeau.Api.Common.Http;
using Rochambeau.Api.Modules.Identity.Domain;

namespace Rochambeau.Api.Modules.Identity.Infrastructure;

/// <summary>
/// The session cookie (accounts ADR-0002, ADR-0005): <c>HttpOnly</c>, <c>Secure</c>,
/// <c>SameSite=Strict</c>, 7-day sliding lifetime, security stamp validated on every request,
/// and 401/403 ProblemDetails instead of redirects.
/// </summary>
internal static class SessionCookie
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public static IServiceCollection AddSessionCookie(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<TimeProvider>((options, time) =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = Lifetime;
                options.SlidingExpiration = true;
                options.TimeProvider = time;
                options.Events.OnRedirectToLogin = context =>
                    IdentityErrors.NotAuthenticated.ToProblem().ExecuteAsync(context.HttpContext);
                options.Events.OnRedirectToAccessDenied = context =>
                    IdentityErrors.Forbidden.ToProblem().ExecuteAsync(context.HttpContext);
            });

        services.AddOptions<SecurityStampValidatorOptions>()
            .Configure<TimeProvider>((options, time) =>
            {
                options.ValidationInterval = TimeSpan.Zero;
                options.TimeProvider = time;
            });

        return services;
    }
}
