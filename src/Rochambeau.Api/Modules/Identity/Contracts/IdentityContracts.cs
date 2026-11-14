namespace Rochambeau.Api.Modules.Identity.Contracts;

/// <summary>The single role an Account holds (accounts ADR-0003). <c>Moderator</c> arrives later.</summary>
public enum AccountRole
{
    Player,
    Administrator,
}

/// <summary>
/// Who is acting, as other modules see it (accounts ADR-0004): the account's UUIDv7 key — the only
/// reference other modules store — its Username as the owner typed it, and its role. No email, ever.
/// </summary>
public sealed record AccountIdentity(Guid AccountId, string Username, AccountRole Role);

/// <summary>Resolves the acting person for the current HTTP request or hub invocation (AC-08).</summary>
public interface ICurrentAccount
{
    /// <summary>The logged-in Account, or <see langword="null"/> for nobody (a Visitor, logged out, expired or revoked).</summary>
    AccountIdentity? Current { get; }
}

/// <summary>Looks Accounts up by id or by exact Username, letter case ignored (AC-11, AC-12).</summary>
public interface IAccountDirectory
{
    Task<AccountIdentity?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Exact match after trimming, case ignored; no partial matches.</summary>
    Task<AccountIdentity?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
}
