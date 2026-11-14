using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Common.Results;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Application.Profiles;

/// <summary>Another Player's Profile (AC-11). The type has no email member at all.</summary>
public sealed record PublicProfile(string Username, DateTimeOffset JoinedAt, AccountRole Role);

/// <summary>Exact Username lookup after trimming, letter case ignored, no partial matches (AC-11, AC-12).</summary>
public sealed class GetPublicProfileHandler(AppDbContext db, ILookupNormalizer normalizer)
{
    public static readonly Error PlayerNotFound =
        Error.NotFound("identity.player_not_found", "No player with that username exists.");

    public async Task<Result<PublicProfile>> HandleAsync(string username, CancellationToken cancellationToken = default)
    {
        var trimmed = username?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<PublicProfile>(PlayerNotFound);
        }

        var normalized = normalizer.NormalizeName(trimmed);
        var profile = await db.Set<Account>()
            .Where(a => a.NormalizedUserName == normalized)
            .Select(a => new PublicProfile(a.UserName!, a.CreatedAt, a.Role))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return profile is null ? Result.Failure<PublicProfile>(PlayerNotFound) : Result.Success(profile);
    }
}
