using Microsoft.AspNetCore.Identity;
using Rochambeau.Api.Modules.Identity.Domain;

namespace Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// The Account aggregate's persistence type (table <c>identity.accounts</c>, accounts ADR-0001).
/// Identity supplies credentials, normalisation, lockout and the security stamp; the module adds
/// the single role (accounts ADR-0003) and the join date.
/// </summary>
public sealed class Account : IdentityUser<Guid>
{
    public AccountRole Role { get; set; } = AccountRole.Player;

    /// <summary>Join date, UTC, set once from <see cref="TimeProvider"/> at registration.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
