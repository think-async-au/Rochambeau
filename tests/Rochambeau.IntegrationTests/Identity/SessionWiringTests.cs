using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>Identity core, the cookie session and deny-by-default authorization (accounts ADR-0002, SAD §8).</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class SessionWiringTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Anonymous_call_to_an_endpoint_without_AllowAnonymous_gets_401_not_a_redirect()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = ProbeHost(factory).CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync(new Uri("/__probe/protected", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("identity.not_authenticated", body.GetProperty("errorCode").GetString());
        Assert.Equal("Log in or register first.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Forbidden_call_gets_403_not_a_redirect()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = ProbeHost(factory).CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync(new Uri("/__probe/forbid", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Health_stays_anonymous()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Session_cookie_is_strict_secure_http_only_and_slides_for_seven_days()
    {
        await using var factory = new ApiFactory(postgres);

        var cookie = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(SameSiteMode.Strict, cookie.Cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(TimeSpan.FromDays(7), cookie.ExpireTimeSpan);
        Assert.True(cookie.SlidingExpiration);
        Assert.Same(factory.Services.GetRequiredService<TimeProvider>(), cookie.TimeProvider);
    }

    [Fact]
    public async Task Security_stamp_is_validated_on_every_request()
    {
        await using var factory = new ApiFactory(postgres);

        var stamp = factory.Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;

        Assert.Equal(TimeSpan.Zero, stamp.ValidationInterval);
        Assert.Same(factory.Services.GetRequiredService<TimeProvider>(), stamp.TimeProvider);
    }

    [Fact]
    public async Task Lockout_and_password_options_match_the_spec()
    {
        await using var factory = new ApiFactory(postgres);

        var identity = factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), identity.Lockout.DefaultLockoutTimeSpan);
        Assert.True(identity.Lockout.AllowedForNewUsers);
        Assert.Equal(8, identity.Password.RequiredLength);
        Assert.False(identity.Password.RequireDigit);
        Assert.False(identity.Password.RequireUppercase);
        Assert.False(identity.Password.RequireLowercase);
        Assert.False(identity.Password.RequireNonAlphanumeric);
        Assert.Equal(1, identity.Password.RequiredUniqueChars);
        Assert.False(identity.User.RequireUniqueEmail);
    }

    [Fact]
    public async Task Data_protection_keys_are_persisted_to_the_configured_path()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = ProbeHost(factory).CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var _ = await client.GetAsync(new Uri("/__probe/sign-in", UriKind.Relative));

        Assert.NotEmpty(Directory.GetFiles(factory.KeysPath, "key-*.xml"));
    }

    /// <summary>
    /// The real host plus a test-only branch under <c>/__probe</c> that runs the host's authentication
    /// scheme and authorization policies with its own endpoints.
    /// </summary>
    private static WebApplicationFactory<Program> ProbeHost(ApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter, ProbeStartupFilter>()));

    private sealed class ProbeStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/__probe", branch =>
            {
                // Own route table, so the probe endpoints never join the real host's.
                branch.Properties.Remove("__GlobalEndpointRouteBuilder");
                branch.UseRouting();
                branch.UseAuthentication();
                branch.UseAuthorization();
                branch.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/protected", () => "secret");
                    endpoints.MapGet("/forbid", (HttpContext context) => context.ForbidAsync(IdentityConstants.ApplicationScheme))
                        .AllowAnonymous();
                    endpoints.MapGet("/sign-in", async (HttpContext context) =>
                    {
                        var principal = new System.Security.Claims.ClaimsPrincipal(
                            new System.Security.Claims.ClaimsIdentity([new("sub", "probe")], IdentityConstants.ApplicationScheme));
                        await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                    }).AllowAnonymous();
                });
            });
            next(app);
        };
    }
}
