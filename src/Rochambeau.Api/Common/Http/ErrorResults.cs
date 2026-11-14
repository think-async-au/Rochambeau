using Microsoft.AspNetCore.Mvc;
using Rochambeau.Api.Common.Results;

namespace Rochambeau.Api.Common.Http;

/// <summary>Maps an expected <see cref="Error"/> to an RFC 9457 ProblemDetails response with an <c>errorCode</c> extension.</summary>
public static class ErrorResults
{
    public const string ErrorCodeExtension = "errorCode";

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var (status, type) = StatusOf(error.Type);
        ProblemDetails problem = error.FieldErrors is { } fieldErrors
            ? new HttpValidationProblemDetails(fieldErrors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Type = type;
        problem.Title = error.Message;
        foreach (var (key, value) in error.Extensions ?? new Dictionary<string, object?>())
        {
            problem.Extensions[key] = value;
        }

        problem.Extensions[ErrorCodeExtension] = error.Code;

        return TypedResults.Problem(problem);
    }

    private static (int Status, string Type) StatusOf(ErrorType type) => type switch
    {
        ErrorType.Validation => (StatusCodes.Status400BadRequest, "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
        ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
        ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "https://tools.ietf.org/html/rfc9110#section-15.5.4"),
        ErrorType.NotFound => (StatusCodes.Status404NotFound, "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
        ErrorType.Conflict => (StatusCodes.Status409Conflict, "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
        ErrorType.Locked => (StatusCodes.Status423Locked, "https://tools.ietf.org/html/rfc4918#section-11.3"),
        ErrorType.TooManyRequests => (StatusCodes.Status429TooManyRequests, "https://tools.ietf.org/html/rfc6585#section-4"),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown error type."),
    };
}
