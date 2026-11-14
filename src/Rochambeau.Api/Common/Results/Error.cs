using System.Diagnostics.CodeAnalysis;

namespace Rochambeau.Api.Common.Results;

/// <summary>The kind of an expected failure; the HTTP edge picks a status code from it.</summary>
public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Locked,
    TooManyRequests,
}

/// <summary>
/// An expected failure (CLAUDE.md · Errors): a stable <paramref name="Code"/> in the
/// <c>module.error_name</c> form, a human-readable <paramref name="Message"/> and a <paramref name="Type"/>.
/// Framework-free so Domain and Application code can return it.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Error is the project-wide name fixed by CLAUDE.md · Errors; the API is C#-only.")]
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Field name → what that field must be. Set only for field-level validation failures.</summary>
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; init; }

    /// <summary>Extra machine-readable members for the problem body (e.g. <c>lockoutEnd</c>).</summary>
    public IReadOnlyDictionary<string, object?>? Extensions { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(code, message, ErrorType.Validation) { FieldErrors = fieldErrors };

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Locked(string code, string message) => new(code, message, ErrorType.Locked);

    public static Error TooManyRequests(string code, string message) => new(code, message, ErrorType.TooManyRequests);
}
