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

        if (account.AccessFailedCount != 0)
        {
            await db.Set<Account>()
                .Where(a => a.Id == account.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.AccessFailedCount, 0))
                .ConfigureAwait(false);
        }

        await signIn.SignInAsync(account, isPersistent: true).ConfigureAwait(false);
        return Result.Success(OwnProfile.Of(account));
    }

    public static Error LockedOut(DateTimeOffset end) =>
        Error.Locked("identity.locked_out", "Login is temporarily blocked. Try again after the time shown.") with
        {
            Extensions = new Dictionary<string, object?> { [LockoutEndExtension] = end.UtcDateTime },
        };

    private async Task<Error> RecordFailureAsync(Guid accountId, DateTimeOffset now)
    {
        var failures = await db.Database
            .SqlQuery<int>($"""
                UPDATE identity.accounts SET access_failed_count = access_failed_count + 1
                WHERE id = {accountId}
                RETURNING access_failed_count AS "Value"
                """)
            .ToListAsync()
            .ConfigureAwait(false);

        if (failures.Count == 1 && failures[0] >= LockoutPolicy.MaxFailedAttempts)
        {
            // The 5th failure in a row starts the block and resets the count (AC-06).
            var end = now + LockoutPolicy.Duration;
            await db.Set<Account>()
                .Where(a => a.Id == accountId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.LockoutEnd, end)
                    .SetProperty(a => a.AccessFailedCount, 0))
                .ConfigureAwait(false);
            LogLockedOut(accountId);
            return LockedOut(end);
        }

        LogFailed(accountId);
        return InvalidCredentials;
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
