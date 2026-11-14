using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Common.Results;
using Rochambeau.Api.Common.Time;
using Rochambeau.Api.Modules.Identity.Domain;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Application.LogIn;

public sealed record LogInCommand(string? Email, string? Password);

/// <summary>
/// Logs this browser in (AC-04–AC-06, SAD §6 «Log in, with lockout»). Lockout follows
/// <see cref="LockoutPolicy"/> against the injected <see cref="TimeProvider"/> — Identity's own
/// lockout reads the system clock — and the failure count is updated atomically in the database.
/// </summary>
public sealed partial class LogInHandler(
    UserManager<Account> users,
    SignInManager<Account> signIn,
    AppDbContext db,
    TimeProvider time,
    ILogger<LogInHandler> logger)
{
    public const string LockoutEndExtension = "lockoutEnd";

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("identity.invalid_credentials", "Email or password is incorrect.");

    private static readonly Lock _dummyLock = new();
    private static string? _dummyHash;

    public async Task<Result<OwnProfile>> HandleAsync(LogInCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var missing = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            missing["email"] = ["The email address is required."];
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            missing["password"] = ["The password is required."];
        }

        if (missing.Count > 0)
        {
            return Result.Failure<OwnProfile>(
                Error.Validation(RegistrationRules.ValidationFailedCode, RegistrationRules.ValidationFailedMessage, missing));
        }

        var account = await users.FindByEmailAsync(command.Email!.Trim()).ConfigureAwait(false);
        if (account is null)
        {
            // Same work as a wrong password, so timing does not reveal which emails exist (AC-05).
            users.PasswordHasher.VerifyHashedPassword(new Account(), DummyHash(), command.Password!);
            return Result.Failure<OwnProfile>(InvalidCredentials);
        }

        var now = time.GetUtcNowForStorage();
        if (account.LockoutEnd is { } end && end > now)
        {
            // Attempts during the block neither count nor extend it (AC-06).
            return Result.Failure<OwnProfile>(LockedOut(end));
        }

        if (!await users.CheckPasswordAsync(account, command.Password!).ConfigureAwait(false))
        {
            return Result.Failure<OwnProfile>(await RecordFailureAsync(account.Id, now).ConfigureAwait(false));
        }

        // Reset the count only while still unlocked: a parallel attempt may have started the block
        // while this password was being checked, and then this correct guess must not log in (AC-06).
        var reset = await db.Set<Account>()
            .Where(a => a.Id == account.Id && (a.LockoutEnd == null || a.LockoutEnd <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.AccessFailedCount, 0))
            .ConfigureAwait(false);
        if (reset == 0)
        {
            return Result.Failure<OwnProfile>(await StoredLockAsync(account.Id, now).ConfigureAwait(false));
        }

        await signIn.SignInAsync(account, isPersistent: true).ConfigureAwait(false);
        return Result.Success(OwnProfile.Of(account));
    }

    public static Error LockedOut(DateTimeOffset end) =>
        Error.Locked("identity.locked_out", "Login is temporarily blocked. Try again after the time shown.") with
        {
            Extensions = new Dictionary<string, object?> { [LockoutEndExtension] = end.UtcDateTime },
        };

    /// <summary>
    /// Counts one failure in a single statement that applies only while the account is unlocked, so
    /// parallel attempts cannot get past the block (AC-06): the 5th failure in a row starts the block
    /// and resets the count; an attempt that lost the race to a block counts nothing and answers locked.
    /// </summary>
    private async Task<Error> RecordFailureAsync(Guid accountId, DateTimeOffset now)
    {
        var max = LockoutPolicy.MaxFailedAttempts;
        var end = now + LockoutPolicy.Duration;
        var updated = await db.Database
            .SqlQuery<DateTimeOffset?>($"""
                UPDATE identity.accounts SET
                    access_failed_count = CASE WHEN access_failed_count + 1 >= {max} THEN 0 ELSE access_failed_count + 1 END,
                    lockout_end = CASE WHEN access_failed_count + 1 >= {max} THEN {end} ELSE lockout_end END
                WHERE id = {accountId} AND (lockout_end IS NULL OR lockout_end <= {now})
                RETURNING lockout_end AS "Value"
                """)
            .ToListAsync()
            .ConfigureAwait(false);

        if (updated.Count == 0)
        {
            return await StoredLockAsync(accountId, now).ConfigureAwait(false);
        }

        if (updated[0] is { } lockedUntil && lockedUntil > now)
        {
            LogLockedOut(accountId);
            return LockedOut(lockedUntil);
        }

        LogFailed(accountId);
        return InvalidCredentials;
    }

    /// <summary>The block a parallel attempt started; attempts during it neither count nor extend it.</summary>
    private async Task<Error> StoredLockAsync(Guid accountId, DateTimeOffset now)
    {
        var end = await db.Set<Account>()
            .Where(a => a.Id == accountId)
            .Select(a => a.LockoutEnd)
            .SingleAsync()
            .ConfigureAwait(false);
        return end is { } lockedUntil && lockedUntil > now ? LockedOut(lockedUntil) : InvalidCredentials;
    }

    private string DummyHash()
    {
        lock (_dummyLock)
        {
            return _dummyHash ??= users.PasswordHasher.HashPassword(new Account(), Guid.NewGuid().ToString("N"));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login failed for account {AccountId}")]
    private partial void LogFailed(Guid accountId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Account {AccountId} locked out")]
    private partial void LogLockedOut(Guid accountId);
}
