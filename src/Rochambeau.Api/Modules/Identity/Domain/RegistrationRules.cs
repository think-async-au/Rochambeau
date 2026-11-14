using Rochambeau.Api.Common.Results;

namespace Rochambeau.Api.Modules.Identity.Domain;

/// <summary>The checked fields of a registration (AC-02).</summary>
public sealed record RegistrationInput(Username Username, EmailAddress Email, Password Password);

/// <summary>Checks every registration field at once, so one response can name every offending field (AC-02).</summary>
public static class RegistrationRules
{
    public const string ValidationFailedCode = "identity.validation_failed";
    public const string ValidationFailedMessage = "One or more fields are invalid.";

    public static Result<RegistrationInput> Validate(string? username, string? email, string? password)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!Username.TryCreate(username, out var checkedUsername))
        {
            errors["username"] = [Username.Rule];
        }

        if (!EmailAddress.TryCreate(email, out var checkedEmail))
        {
            errors["email"] = [EmailAddress.Rule];
        }

        if (!Password.TryCreate(password, out var checkedPassword, out var passwordRule))
        {
            errors["password"] = [passwordRule!];
        }

        return errors.Count == 0
            ? Result.Success(new RegistrationInput(checkedUsername!, checkedEmail!, checkedPassword!))
            : Result.Failure<RegistrationInput>(Error.Validation(ValidationFailedCode, ValidationFailedMessage, errors));
    }
}
