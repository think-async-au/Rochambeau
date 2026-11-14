using Microsoft.EntityFrameworkCore;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Common.Results;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Domain;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Application.Profiles;

/// <summary>The acting Player's own Profile, email included (AC-09); nobody → not logged in (AC-10).</summary>
public sealed class GetOwnProfileHandler(ICurrentAccount current, AppDbContext db)
{
    public async Task<Result<OwnProfile>> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (current.Current is not { } acting)
        {
            return Result.Failure<OwnProfile>(IdentityErrors.NotAuthenticated);
        }

        var account = await db.Set<Account>()
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == acting.AccountId, cancellationToken)
            .ConfigureAwait(false);

        return account is null
            ? Result.Failure<OwnProfile>(IdentityErrors.NotAuthenticated)
            : Result.Success(OwnProfile.Of(account));
    }
}
