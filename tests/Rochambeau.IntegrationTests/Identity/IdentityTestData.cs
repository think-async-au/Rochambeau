using Microsoft.Extensions.DependencyInjection;
using Rochambeau.Api.Modules.Identity.Application.ChangeRole;
using Rochambeau.Api.Modules.Identity.Contracts;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>Fixtures that go through the same handlers as production (data-model.md · Test fixtures).</summary>
internal static class IdentityTestData
{
    /// <summary>Runs the ChangeRole handler the operator command uses; never writes <c>role</c> directly.</summary>
    public static async Task PromoteToAdministratorAsync(IServiceProvider services, string username)
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ChangeRoleHandler>()
            .HandleAsync(username, AccountRole.Administrator);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ChangeRoleOutcome.Changed, result.Value.Outcome);
    }
}
