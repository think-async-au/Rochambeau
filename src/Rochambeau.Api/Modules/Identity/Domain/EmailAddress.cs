using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;

namespace Rochambeau.Api.Modules.Identity.Domain;

/// <summary>
/// A well-formed email address of at most 254 characters (RFC 5321 path limit), trimmed, with its
/// letter case kept for display (AC-02). Comparison ignoring case is Identity's normalisation.
/// </summary>
public sealed record EmailAddress
{
    public const int MaxLength = 254;
    public const string Rule = "The email address must be a valid address of at most 254 characters.";

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? raw, [NotNullWhen(true)] out EmailAddress? email)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        email = trimmed.Length is > 0 and <= MaxLength
            && MailAddress.TryCreate(trimmed, out var parsed)
            && string.Equals(parsed.Address, trimmed, StringComparison.Ordinal)
            && string.IsNullOrEmpty(parsed.DisplayName)
                ? new EmailAddress(trimmed)
                : null;
        return email is not null;
    }

    public override string ToString() => Value;
}
