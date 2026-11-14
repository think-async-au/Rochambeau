using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Infrastructure;

/// <summary>Account lookups for other modules: by primary key or by normalised Username. Never returns the email.</summary>
public sealed class AccountDirectory(AppDbContext db, ILookupNormalizer normalizer) : IAccountDirectory
{
    public Task<AccountIdentity?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        db.Set<Account>()
            .Where(a => a.Id == accountId)
            .Select(a => new AccountIdentity(a.Id, a.UserName!, a.Role))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AccountIdentity?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var trimmed = username?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var normalized = normalizer.NormalizeName(trimmed);
        return await db.Set<Account>()
            .Where(a => a.NormalizedUserName == normalized)
            .Select(a => new AccountIdentity(a.Id, a.UserName!, a.Role))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
