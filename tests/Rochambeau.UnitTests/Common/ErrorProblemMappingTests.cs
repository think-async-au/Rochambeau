using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Rochambeau.Api.Common.Http;
using Rochambeau.Api.Common.Results;

namespace Rochambeau.UnitTests.Common;

public class ErrorProblemMappingTests
{
    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Locked, StatusCodes.Status423Locked)]
    [InlineData(ErrorType.TooManyRequests, StatusCodes.Status429TooManyRequests)]
    public void Status_is_chosen_by_error_type(ErrorType type, int expectedStatus)
    {
        var error = new Error("test.some_error", "Something went wrong.", type);

        var problem = AsProblem(error.ToProblem());

        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.Equal(expectedStatus, problem.ProblemDetails.Status);
    }

    [Fact]
    public void Problem_carries_the_error_code_extension_and_the_message_as_title()
    {
        var error = Error.Conflict("identity.username_taken", "This username is already taken.");

        var problem = AsProblem(error.ToProblem());

        Assert.Equal("identity.username_taken", problem.ProblemDetails.Extensions["errorCode"]);
        Assert.Equal("This username is already taken.", problem.ProblemDetails.Title);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.10", problem.ProblemDetails.Type);
        Assert.Equal("application/problem+json", problem.ContentType);
    }

    [Theory]
    [InlineData(ErrorType.Validation, "https://tools.ietf.org/html/rfc9110#section-15.5.1")]
    [InlineData(ErrorType.Unauthorized, "https://tools.ietf.org/html/rfc9110#section-15.5.2")]
    [InlineData(ErrorType.NotFound, "https://tools.ietf.org/html/rfc9110#section-15.5.5")]
    [InlineData(ErrorType.Locked, "https://tools.ietf.org/html/rfc4918#section-11.3")]
    [InlineData(ErrorType.TooManyRequests, "https://tools.ietf.org/html/rfc6585#section-4")]
    public void Problem_type_matches_the_contract(ErrorType type, string expectedType)
    {
        var problem = AsProblem(new Error("test.some_error", "x", type).ToProblem());

        Assert.Equal(expectedType, problem.ProblemDetails.Type);
    }

    [Fact]
    public void Validation_error_becomes_validation_problem_with_errors_keyed_by_field()
    {
        var error = Error.Validation(
            "identity.validation_failed",
            "One or more fields are invalid.",
            new Dictionary<string, string[]>
            {
                ["username"] = ["The username must be 3 to 20 characters: Latin letters, digits, underscore or hyphen."],
                ["password"] = ["The password must be at least 8 characters."],
            });

        var problem = AsProblem(error.ToProblem());

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var validation = Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails);
        Assert.Equal("identity.validation_failed", validation.Extensions["errorCode"]);
        Assert.Equal(["password", "username"], validation.Errors.Keys.Order());
        Assert.Equal(["The password must be at least 8 characters."], validation.Errors["password"]);
    }

    [Fact]
    public void Error_without_field_errors_is_a_plain_problem_even_when_it_is_a_validation_type()
    {
        var error = new Error("common.client_header_required", "The X-Rochambeau-Client header is required.", ErrorType.Validation);

        var problem = AsProblem(error.ToProblem());

        Assert.IsNotType<HttpValidationProblemDetails>(problem.ProblemDetails);
        Assert.Equal("common.client_header_required", problem.ProblemDetails.Extensions["errorCode"]);
    }

    [Fact]
    public void Error_extensions_are_copied_to_the_problem()
    {
        var end = new DateTime(2026, 11, 14, 10, 15, 0, DateTimeKind.Utc);
        var error = Error.Locked("identity.locked_out", "Blocked.") with
        {
            Extensions = new Dictionary<string, object?> { ["lockoutEnd"] = end },
        };

        var problem = AsProblem(error.ToProblem());

        Assert.Equal(end, problem.ProblemDetails.Extensions["lockoutEnd"]);
        Assert.Equal("identity.locked_out", problem.ProblemDetails.Extensions["errorCode"]);
    }

    [Fact]
    public void Result_carries_value_or_error()
    {
        var ok = Result.Success(42);
        var failed = Result.Failure<int>(Error.NotFound("test.missing", "Missing."));

        Assert.True(ok.IsSuccess);
        Assert.Equal(42, ok.Value);
        Assert.True(failed.IsFailure);
        Assert.Equal("test.missing", failed.Error!.Code);
        Assert.Throws<InvalidOperationException>(() => failed.Value);
    }

    private static ProblemHttpResult AsProblem(IResult result) => Assert.IsType<ProblemHttpResult>(result);
}
