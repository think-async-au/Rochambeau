using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Application;

/// <summary>The caller's own Profile (AC-09) — the only read model that carries an email.</summary>
public sealed record OwnProfile(string Username, string Email, DateTimeOffset JoinedAt, AccountRole Role)
{
    public static OwnProfile Of(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new(account.UserName!, account.Email!, account.CreatedAt, account.Role);
    }
}
