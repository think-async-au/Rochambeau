using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Rochambeau.Api.Modules.Identity.Domain;

/// <summary>
/// The unique public name of a Player, as the owner typed it after trimming (AC-02, AC-03):
/// 3–20 Latin letters a–z/A–Z, digits, <c>_</c> or <c>-</c>. ASCII-only, so case-insensitive
/// normalisation is unambiguous.
/// </summary>
public sealed partial record Username
{
    public const int MinLength = 3;
    public const int MaxLength = 20;
    public const string Rule = "The username must be 3 to 20 characters: Latin letters, digits, underscore or hyphen.";

    private Username(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? raw, [NotNullWhen(true)] out Username? username)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        username = trimmed.Length is >= MinLength and <= MaxLength && Allowed().IsMatch(trimmed)
            ? new Username(trimmed)
            : null;
        return username is not null;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Allowed();
}
