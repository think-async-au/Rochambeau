using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>POST /api/v1/session — AC-04, AC-05, AC-06.</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class LogInTests(PostgresFixture postgres)
{
    private const string _wrongPassword = "wrong-password-1";
    private const int _failuresBeforeBlock = 4;

    [Fact]
    public async Task AC04_correct_email_in_any_case_with_spaces_and_password_logs_in()
    {
        await using var factory = new ApiFactory(postgres);
        var (username, email) = await RegisterAsync(factory);
        using var client = IdentityApi.CreateClient(factory);

        using var response = await LogInAsync(client, $"  {email.ToUpperInvariant()} ", IdentityApi.DefaultPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(["email", "joinedAt", "role", "username"], body.PropertyNames());
        Assert.Equal(username, body.GetProperty("username").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("player", body.GetProperty("role").GetString());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(IdentityApi.SessionCookie + "=", StringComparison.Ordinal));
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC05_unknown_email_and_wrong_password_get_the_same_answer()
    {
        await using var factory = new ApiFactory(postgres);
        var (_, email) = await RegisterAsync(factory);
        using var client = IdentityApi.CreateClient(factory);

        using var unknown = await LogInAsync(client, IdentityApi.NextEmail(), IdentityApi.DefaultPassword);
        using var wrong = await LogInAsync(client, email, _wrongPassword);
        using var malformed = await LogInAsync(client, "not-an-email", IdentityApi.DefaultPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        var unknownBody = await BodyWithoutTraceIdAsync(unknown);
        Assert.Equal("identity.invalid_credentials", unknownBody["errorCode"]!.GetValue<string>());
        Assert.Equal("Email or password is incorrect.", unknownBody["title"]!.GetValue<string>());
        Assert.Equal(unknownBody.ToJsonString(), (await BodyWithoutTraceIdAsync(wrong)).ToJsonString());
        Assert.Equal(unknownBody.ToJsonString(), (await BodyWithoutTraceIdAsync(malformed)).ToJsonString());
        Assert.Equal(unknown.Content.Headers.ContentType, wrong.Content.Headers.ContentType);
        Assert.False(wrong.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("", "x", "email")]
    [InlineData("user@example.test", "", "password")]
    [InlineData(null, null, null)]
    public async Task Missing_or_empty_fields_are_a_validation_error(string? email, string? password, string? field)
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);

        using var response = await LogInAsync(client, email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("identity.validation_failed", body.GetProperty("errorCode").GetString());
        string[] expected = field is null ? ["email", "password"] : [field];
        Assert.Equal(expected, body.GetProperty("errors").PropertyNames());
    }

    [Fact]
    public async Task AC06_five_failures_block_for_fifteen_minutes_without_extension()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        var (_, email) = await RegisterAsync(factory);
        using var client = IdentityApi.CreateClient(factory);

        for (var i = 0; i < 4; i++)
        {
            using var failed = await LogInAsync(client, email, _wrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using var fifth = await LogInAsync(client, email, _wrongPassword);
        var lockoutEnd = await AssertLockedAsync(fifth, time.GetUtcNow() + TimeSpan.FromMinutes(15));

        using var correctDuringBlock = await LogInAsync(client, email, IdentityApi.DefaultPassword);
        Assert.Equal(lockoutEnd, await AssertLockedAsync(correctDuringBlock, lockoutEnd));
        Assert.False(correctDuringBlock.Headers.Contains("Set-Cookie"));

        time.Advance(TimeSpan.FromMinutes(10));
        using var wrongDuringBlock = await LogInAsync(client, email, _wrongPassword);
        Assert.Equal(lockoutEnd, await AssertLockedAsync(wrongDuringBlock, lockoutEnd));

        time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        using var afterBlock = await LogInAsync(client, email, IdentityApi.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, afterBlock.StatusCode);
    }

    [Fact]
    public async Task AC06_after_a_block_ends_five_new_failures_are_needed_to_block_again()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        var (_, email) = await RegisterAsync(factory);
        using var client = IdentityApi.CreateClient(factory);
        for (var i = 0; i < 5; i++)
        {
            (await LogInAsync(client, email, _wrongPassword)).Dispose();
        }

        time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        for (var i = 0; i < 4; i++)
        {
            using var failed = await LogInAsync(client, email, _wrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using var fifth = await LogInAsync(client, email, _wrongPassword);
        await AssertLockedAsync(fifth, time.GetUtcNow() + TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task AC06_a_success_before_the_fifth_failure_resets_the_count()
    {
        await using var factory = new ApiFactory(postgres, new FakeTimeProvider(DateTimeOffset.UtcNow));
        var (_, email) = await RegisterAsync(factory);
        using var client = IdentityApi.CreateClient(factory);

        for (var i = 0; i < 4; i++)
        {
            (await LogInAsync(client, email, _wrongPassword)).Dispose();
        }

        using var success = await LogInAsync(client, email, IdentityApi.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        for (var i = 0; i < 4; i++)
        {
            using var failed = await LogInAsync(client, email, _wrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }
    }

    [Fact]
    public async Task AC05_log_in_with_an_unknown_email_creates_no_account_and_no_lockout_state()
    {
        await using var factory = new ApiFactory(postgres, new FakeTimeProvider(DateTimeOffset.UtcNow));
        using var client = IdentityApi.CreateClient(factory);
        var email = IdentityApi.NextEmail();

        for (var i = 0; i < 6; i++)
        {
            using var failed = await LogInAsync(client, email, _wrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        Assert.Equal(0, await CountByEmailAsync(factory, email));
        using var registered = await IdentityApi.RegisterAsync(client, IdentityApi.NextUsername(), email);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        using var loggedIn = await LogInAsync(client, email, IdentityApi.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
    }

    [Fact]
    public async Task AC06_parallel_wrong_passwords_get_no_more_than_five_guesses()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        var (_, email) = await RegisterAsync(factory);
        using var client = IdentityApi.CreateClient(factory);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => LogInAsync(client, email, _wrongPassword)));
        try
        {
            var statuses = responses.Select(r => (int)r.StatusCode).ToList();
            Assert.Equal(_failuresBeforeBlock, statuses.Count(s => s == 401));
            Assert.Equal(20 - _failuresBeforeBlock, statuses.Count(s => s == 423));
        }
        finally
        {
            Array.ForEach(responses, r => r.Dispose());
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = email.ToUpperInvariant();
        var account = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
            .SingleAsync(a => a.NormalizedEmail == normalized);
        Assert.Equal(0, account.AccessFailedCount);
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromMinutes(15), account.LockoutEnd!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task AC06_a_lockout_of_one_account_does_not_block_another()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(postgres, time);
        var (_, emailA) = await RegisterAsync(factory);
        var (usernameB, emailB) = await RegisterAsync(factory);
        using var client = IdentityApi.CreateClient(factory);
        for (var i = 0; i < 5; i++)
        {
            (await LogInAsync(client, emailA, _wrongPassword)).Dispose();
        }

        using var lockedA = await LogInAsync(client, emailA, IdentityApi.DefaultPassword);
        await AssertLockedAsync(lockedA, time.GetUtcNow() + TimeSpan.FromMinutes(15));

        using var b = await LogInAsync(client, emailB, IdentityApi.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        Assert.Equal(usernameB, (await b.ReadJsonAsync()).GetProperty("username").GetString());
    }

    [Theory]
    [InlineData(IdentityApi.DefaultPassword)]
    [InlineData(_wrongPassword)]
    public async Task AC06_an_attempt_whose_account_is_locked_while_its_password_is_checked_is_locked_out(string password)
    {
        // Deterministic stand-in for a parallel attempt: the account is locked between this
        // attempt's lock check and its write, so only the conditional writes can refuse it.
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var trap = new LockDuringPasswordCheck(time.GetUtcNow() + TimeSpan.FromMinutes(15));
        await using var factory = new ApiFactory(postgres, time);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IPasswordHasher<Account>>(sp => new LockingPasswordHasher(
                new PasswordHasher<Account>(sp.GetService<IOptions<PasswordHasherOptions>>()), trap, sp.GetRequiredService<IServiceScopeFactory>()))));
        var email = IdentityApi.NextEmail();
        using (var registering = host.CreateClient())
        {
            // Its own browser, so the log-in below carries no session cookie of its own.
            registering.DefaultRequestHeaders.Add("X-Rochambeau-Client", "web");
            using var registered = await IdentityApi.RegisterAsync(registering, IdentityApi.NextUsername(), email);
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Rochambeau-Client", "web");
        for (var i = 0; i < 2; i++)
        {
            using var failed = await LogInAsync(client, email, _wrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        trap.Arm();
        using var response = await LogInAsync(client, email, password);

        Assert.True(trap.Sprung);
        await AssertLockedAsync(response, trap.LockoutEnd);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        await using var scope = host.Services.CreateAsyncScope();
        var normalized = email.ToUpperInvariant();
        var account = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
            .SingleAsync(a => a.NormalizedEmail == normalized);
        Assert.Equal(2, account.AccessFailedCount);
        Assert.Equal(trap.LockoutEnd, account.LockoutEnd!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Log_in_without_the_client_header_is_rejected()
    {
        await using var factory = new ApiFactory(postgres);
        var (_, email) = await RegisterAsync(factory);
        using var client = factory.CreateClient();

        using var response = await LogInAsync(client, email, IdentityApi.DefaultPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("common.client_header_required", (await response.ReadJsonAsync()).GetProperty("errorCode").GetString());
    }

    private static async Task<DateTimeOffset> AssertLockedAsync(HttpResponseMessage response, DateTimeOffset expectedEnd)
    {
        Assert.Equal((HttpStatusCode)423, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("identity.locked_out", body.GetProperty("errorCode").GetString());
        Assert.Equal("Login is temporarily blocked. Try again after the time shown.", body.GetProperty("title").GetString());
        var end = body.GetProperty("lockoutEnd").GetDateTimeOffset();
        Assert.Equal(expectedEnd, end, TimeSpan.FromMilliseconds(1));
        Assert.Equal(TimeSpan.Zero, end.Offset);
        var retryAfter = int.Parse(Assert.Single(response.Headers.GetValues("Retry-After")), System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(retryAfter, 0, 15 * 60);
        return end;
    }

    private static async Task<(string Username, string Email)> RegisterAsync(ApiFactory factory)
    {
        using var client = IdentityApi.CreateClient(factory);
        var username = IdentityApi.NextUsername();
        var email = IdentityApi.NextEmail();
        using var response = await IdentityApi.RegisterAsync(client, username, email);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (username, email);
    }

    private static async Task<int> CountByEmailAsync(ApiFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
            .CountAsync(a => a.NormalizedEmail == normalized);
    }

    private static Task<HttpResponseMessage> LogInAsync(HttpClient client, string? email, string? password) =>
        client.PostAsJsonAsync(new Uri("/api/v1/session", UriKind.Relative), new { email, password });

    private static async Task<JsonObject> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        body.Remove("traceId");
        return body;
    }

    /// <summary>When armed, locks the account once, from another DbContext, during the next password check.</summary>
    private sealed class LockDuringPasswordCheck(DateTimeOffset lockoutEnd)
    {
        private int _armed;

        public DateTimeOffset LockoutEnd { get; } = lockoutEnd;

        public bool Sprung { get; private set; }

        public void Arm() => Volatile.Write(ref _armed, 1);

        public bool TryDisarm() => Interlocked.Exchange(ref _armed, 0) == 1;

        public void MarkSprung() => Sprung = true;
    }

    private sealed class LockingPasswordHasher(
        IPasswordHasher<Account> inner, LockDuringPasswordCheck trap, IServiceScopeFactory scopes) : IPasswordHasher<Account>
    {
        public string HashPassword(Account user, string password) => inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(Account user, string hashedPassword, string providedPassword)
        {
            if (user.Id != Guid.Empty && trap.TryDisarm())
            {
                using var scope = scopes.CreateScope();
                var end = trap.LockoutEnd;
                var locked = scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>()
                    .Where(a => a.Id == user.Id)
                    .ExecuteUpdate(s => s.SetProperty(a => a.LockoutEnd, end));
                Assert.Equal(1, locked);
                trap.MarkSprung();
            }

            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
