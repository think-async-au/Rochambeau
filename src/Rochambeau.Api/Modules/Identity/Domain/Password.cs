using System.Diagnostics.CodeAnalysis;

namespace Rochambeau.Api.Modules.Identity.Domain;

/// <summary>
/// A plain-text password on its way to the hasher. Length 8–128 is the only rule: no trimming and
/// no required digits, capitals or symbols (AC-02).
/// </summary>
public sealed record Password
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    public const string TooShort = "The password must be at least 8 characters.";
    public const string TooLong = "The password must be at most 128 characters.";

    private Password(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? raw, [NotNullWhen(true)] out Password? password) =>
        TryCreate(raw, out password, out _);

    public static bool TryCreate(string? raw, [NotNullWhen(true)] out Password? password, out string? rule)
    {
        var length = raw?.Length ?? 0;
        rule = length < MinLength ? TooShort : length > MaxLength ? TooLong : null;
        password = rule is null ? new Password(raw!) : null;
        return password is not null;
    }

    /// <summary>Never print the password.</summary>
    public override string ToString() => "***";
}
