using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Rochambeau.IntegrationTests.Infrastructure;

/// <summary>Boots the real API (Development environment, so migrations apply) against the fixture's container.</summary>
public sealed class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Rochambeau", postgres.ConnectionString);
    }
}
