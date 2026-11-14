using Microsoft.AspNetCore.Identity;
using Rochambeau.Api.Common.Results;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Application.ChangeRole;

/// <summary>What ChangeRole did, and the Username as its owner typed it.</summary>
public sealed record ChangeRoleResult(ChangeRoleOutcome Outcome, string Username);

public enum ChangeRoleOutcome
{
    /// <summary>The role changed and every session of the Account ended (AC-13b).</summary>
    Changed,

    /// <summary>The Account already had that role; nothing changed and sessions were kept.</summary>
    Unchanged,
}

/// <summary>
/// The only writer of <c>role</c> (accounts ADR-0003), reached only from the operator command — no
/// endpoint or hub calls it (AC-14). Sets the role and rotates the security stamp in one save, so
/// every device resolves as nobody on its next request (AC-13, AC-13b).
/// </summary>
public sealed partial class ChangeRoleHandler(UserManager<Account> users, ILogger<ChangeRoleHandler> logger)
{
    public static readonly Error AccountNotFound =
        Error.NotFound("identity.account_not_found", "No account has that username.");

    public async Task<Result<ChangeRoleResult>> HandleAsync(string username, AccountRole role)
    {
        var account = string.IsNullOrWhiteSpace(username)
            ? null
            : await users.FindByNameAsync(username.Trim()).ConfigureAwait(false);
        if (account is null)
        {
            return Result.Failure<ChangeRoleResult>(AccountNotFound);
        }

        if (account.Role == role)
        {
            return Result.Success(new ChangeRoleResult(ChangeRoleOutcome.Unchanged, account.UserName!));
        }

        account.Role = role;
        var saved = await users.UpdateSecurityStampAsync(account).ConfigureAwait(false);
        if (!saved.Succeeded)
        {
            throw new InvalidOperationException(
                $"Changing the role of account {account.Id} failed: {string.Join(", ", saved.Errors.Select(e => e.Code))}.");
        }

        LogRoleChanged(account.Id, role);
        return Result.Success(new ChangeRoleResult(ChangeRoleOutcome.Changed, account.UserName!));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Role of account {AccountId} changed to {Role}")]
    private partial void LogRoleChanged(Guid accountId, AccountRole role);
}
