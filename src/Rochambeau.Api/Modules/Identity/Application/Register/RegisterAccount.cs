using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rochambeau.Api.Common.Results;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Domain;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Application.Register;

/// <summary>What a Visitor submits to register. There is no role: registration always creates a Player (AC-14).</summary>
/// <param name="NetworkAddress">Keys the registration cap (AC-15); never stored with the Account or logged.</param>
public sealed record RegisterAccountCommand(string? Username, string? Email, string? Password, string NetworkAddress);

/// <summary>
/// Registers an Account and logs this browser in as its Player (AC-01–AC-03, AC-15, SAD §6 critical
/// flow 1): field rules → reserve a cap slot → insert → confirm the slot, or release it on any failure.
/// The unique indexes are the only race guard; the handler never checks before inserting.
/// </summary>
public sealed partial class RegisterAccountHandler(
    UserManager<Account> users,
    SignInManager<Account> signIn,
    RegistrationCap cap,
    TimeProvider time,
    ILogger<RegisterAccountHandler> logger)
{
    public static readonly Error UsernameTaken =
        Error.Conflict("identity.username_taken", "This username is already taken.");

    public static readonly Error EmailUnavailable =
        Error.Conflict("identity.email_unavailable", "This email address cannot be used.");

    public static readonly Error RegistrationCapped = Error.TooManyRequests(
        "identity.registration_cap",
        "Too many accounts were created from your connection recently. Try again later.");

    public async Task<Result<OwnProfile>> HandleAsync(RegisterAccountCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validated = RegistrationRules.Validate(command.Username, command.Email, command.Password);
        if (validated.IsFailure)
        {
            return Result.Failure<OwnProfile>(validated.Error!);
        }

        var reservation = cap.TryReserve(command.NetworkAddress);
        if (reservation is null)
        {
            LogCapHit();
            return Result.Failure<OwnProfile>(RegistrationCapped);
        }

        var input = validated.Value;
        var account = new Account
        {
            Id = Guid.CreateVersion7(),
            UserName = input.Username.Value,
            Email = input.Email.Value,
            Role = AccountRole.Player,
            CreatedAt = time.GetUtcNow(),
        };

        Error? conflict;
        try
        {
            conflict = await CreateAsync(account, input.Password).ConfigureAwait(false);
        }
        catch
        {
            cap.Release(reservation);
            throw;
        }

        if (conflict is not null)
        {
            cap.Release(reservation);
            return Result.Failure<OwnProfile>(conflict);
        }

        // The Account exists from here on, so the slot counts even if signing in fails.
        cap.Confirm(reservation);
        LogRegistered(account.Id);
        await signIn.SignInAsync(account, isPersistent: true).ConfigureAwait(false);
        return Result.Success(OwnProfile.Of(account));
    }

    private async Task<Error?> CreateAsync(Account account, Password password)
    {
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(account, password.Value).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique)
        {
            return unique.ConstraintName switch
            {
                AccountConfiguration.UsernameIndex => UsernameTaken,
                AccountConfiguration.EmailIndex => EmailUnavailable,
                _ => throw new InvalidOperationException($"Unexpected unique violation on '{unique.ConstraintName}'.", ex),
            };
        }

        if (result.Succeeded)
        {
            return null;
        }

        var codes = result.Errors.Select(e => e.Code).ToHashSet(StringComparer.Ordinal);
        var describer = new IdentityErrorDescriber();
        if (codes.Contains(describer.DuplicateUserName(string.Empty).Code))
        {
            return UsernameTaken;
        }

        if (codes.Contains(describer.DuplicateEmail(string.Empty).Code))
        {
            return EmailUnavailable;
        }

        // Domain already enforced every rule Identity checks, so anything else is a bug.
        throw new InvalidOperationException($"Identity rejected a validated registration: {string.Join(", ", codes)}.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Account {AccountId} registered")]
    private partial void LogRegistered(Guid accountId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registration cap hit")]
    private partial void LogCapHit();
}
