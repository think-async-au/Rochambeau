using System.Net;
using System.Text;
using Rochambeau.IntegrationTests.Identity;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Host;

/// <summary>
/// A body that cannot be bound never reaches a handler; its 400 must still carry the contract's
/// closed ProblemDetails shape with an <c>errorCode</c> (contracts/openapi.yaml · ProblemDetails).
/// </summary>
[Collection(PostgresTestGroup.Name)]
public sealed class BadRequestBodyTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("/api/v1/accounts", "{bad")]
    [InlineData("/api/v1/accounts", "")]
    [InlineData("/api/v1/accounts", """{"username": 42, "email": "a@example.test", "password": "long-enough-1"}""")]
    [InlineData("/api/v1/session", "{bad")]
    [InlineData("/api/v1/session", "")]
    public async Task An_unreadable_body_is_a_400_problem_with_an_error_code(string path, string body)
    {
        await using var factory = new ApiFactory(postgres);
        using var client = IdentityApi.CreateClient(factory);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri(path, UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("common.invalid_request", problem.GetProperty("errorCode").GetString());
        Assert.Equal("The request could not be read.", problem.GetProperty("title").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }
}
