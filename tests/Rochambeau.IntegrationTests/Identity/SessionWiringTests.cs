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
using Rochambeau.Api.Modules.Identity.Contracts;
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

    [Fact]
    public async Task AC08_an_http_request_sees_the_logged_in_player_as_the_current_account()
    {
        await using var factory = new ApiFactory(postgres);
        await using var host = ProbeHost(factory);
        using var client = ProbeClient(host);
        var username = IdentityApi.NextUsername();
        using (var registered = await IdentityApi.RegisterAsync(client, username))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        var first = await WhoAmIAsync(client);
        var second = await WhoAmIAsync(client);

        var expected = await FindAsync(host, username);
        Assert.Equal(expected.AccountId.ToString(), first.GetProperty("accountId").GetString());
        Assert.Equal(username, first.GetProperty("username").GetString());
        Assert.Equal(nameof(AccountRole.Player), first.GetProperty("role").GetString());
        Assert.Equal(first.GetRawText(), second.GetRawText());
    }

    [Fact]
    public async Task AC13_an_http_request_sees_an_operator_designated_administrator_as_the_current_account()
    {
        await using var factory = new ApiFactory(postgres);
        await using var host = ProbeHost(factory);
        using var client = ProbeClient(host);
        var username = IdentityApi.NextUsername();
        var email = IdentityApi.NextEmail();
        (await IdentityApi.RegisterAsync(client, username, email)).Dispose();
        await IdentityTestData.PromoteToAdministratorAsync(host.Services, username);
        using (var loggedIn = await client.PostAsJsonAsync(
            new Uri("/api/v1/session", UriKind.Relative), new { email, password = IdentityApi.DefaultPassword }))
        {
            Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        }

        var current = await WhoAmIAsync(client);

        Assert.Equal((await FindAsync(host, username)).AccountId.ToString(), current.GetProperty("accountId").GetString());
        Assert.Equal(username, current.GetProperty("username").GetString());
        Assert.Equal(nameof(AccountRole.Administrator), current.GetProperty("role").GetString());
    }

    [Fact]
    public async Task AC08_an_http_request_from_a_visitor_has_no_current_account()
    {
        await using var factory = new ApiFactory(postgres);
        await using var host = ProbeHost(factory);
        using var client = ProbeClient(host);

        using var response = await client.GetAsync(new Uri("/__probe/whoami", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await response.ReadJsonAsync()).GetProperty("current").ValueKind);
    }

    private static HttpClient ProbeClient(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Rochambeau-Client", "web");
        client.DefaultRequestHeaders.Add(TestRemoteAddress.HeaderName, TestRemoteAddress.Next());
        return client;
    }

    /// <summary>The acting account as <see cref="ICurrentAccount"/> resolves it for this request.</summary>
    private static async Task<JsonElement> WhoAmIAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri("/__probe/whoami", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = (await response.ReadJsonAsync()).GetProperty("current");
        Assert.Equal(JsonValueKind.Object, current.ValueKind);
        return current;
    }

    private static async Task<AccountIdentity> FindAsync(WebApplicationFactory<Program> host, string username)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountDirectory>().FindByUsernameAsync(username);
        Assert.NotNull(account);
        return account;
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
                    endpoints.MapGet("/whoami", (ICurrentAccount current) => Results.Ok(new
                    {
                        current = current.Current is { } acting
                            ? new { accountId = acting.AccountId, username = acting.Username, role = acting.Role.ToString() }
                            : null,
                    }))
                        .AllowAnonymous();
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
