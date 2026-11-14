using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Rochambeau.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API (Development environment, so migrations apply) against the fixture's container.
/// Clients talk https so the <c>Secure</c> session cookie round-trips; Data Protection keys go to a
/// per-factory temp directory; <paramref name="time"/> swaps in a controlled clock.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgresFixture _postgres;
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), "rochambeau-tests", Guid.NewGuid().ToString("N"));

    public ApiFactory(PostgresFixture postgres, TimeProvider? time = null)
    {
        _postgres = postgres;
        Time = time;
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    public TimeProvider? Time { get; }

    public string KeysPath => _keysPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Rochambeau", _postgres.ConnectionString);
        builder.UseSetting("DataProtection:KeysPath", _keysPath);
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestRemoteAddress>());

        if (Time is { } time)
        {
            builder.ConfigureTestServices(services => services.AddSingleton(time));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_keysPath))
        {
            Directory.Delete(_keysPath, recursive: true);
        }
    }
}
