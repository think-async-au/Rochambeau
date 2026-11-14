using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rochambeau.Api.Common.Results;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Domain;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Application.Register;

/// <summary>What a Visitor submits to register. There is no role: registration always creates a Player (AC-14).</summary>
public sealed record RegisterAccountCommand(string? Username, string? Email, string? Password);

/// <summary>
/// Registers an Account and logs this browser in as its Player (AC-01–AC-03, SAD §6 critical flow 1).
/// The unique indexes are the only race guard; the handler never checks before inserting.
/// </summary>
public sealed partial class RegisterAccountHandler(
    UserManager<Account> users,
    SignInManager<Account> signIn,
    TimeProvider time,
    ILogger<RegisterAccountHandler> logger)
{
    public static readonly Error UsernameTaken =
        Error.Conflict("identity.username_taken", "This username is already taken.");

    public static readonly Error EmailUnavailable =
        Error.Conflict("identity.email_unavailable", "This email address cannot be used.");

    public async Task<Result<OwnProfile>> HandleAsync(RegisterAccountCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validated = RegistrationRules.Validate(command.Username, command.Email, command.Password);
        if (validated.IsFailure)
        {
            return Result.Failure<OwnProfile>(validated.Error!);
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

        var conflict = await CreateAsync(account, input.Password).ConfigureAwait(false);
        if (conflict is not null)
        {
            return Result.Failure<OwnProfile>(conflict);
        }

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
}
