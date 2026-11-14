using System.Net;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Host;

[Collection(PostgresTestGroup.Name)]
public sealed class SmokeTests(PostgresFixture postgres)
{
    [Fact]
    public async Task App_boots_against_postgres_and_health_returns_200()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
