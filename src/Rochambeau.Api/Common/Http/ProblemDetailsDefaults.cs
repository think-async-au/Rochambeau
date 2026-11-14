namespace Rochambeau.Api.Common.Http;

/// <summary>
/// ProblemDetails for every error response, including the ones the framework writes before any
/// handler runs: a request body that cannot be bound (malformed JSON, an empty body, a wrong type)
/// becomes a 400 with <c>common.invalid_request</c>, so every 4xx carries the contract's
/// <c>errorCode</c> (contracts/openapi.yaml · ProblemDetails).
/// </summary>
public static class ProblemDetailsDefaults
{
    public const string InvalidRequestCode = "common.invalid_request";
    public const string InvalidRequestTitle = "The request could not be read.";

    public static IServiceCollection AddProblemDetailsWithErrorCodes(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            if (problem.Status == StatusCodes.Status400BadRequest
                && !problem.Extensions.ContainsKey(ErrorResults.ErrorCodeExtension))
            {
                problem.Title = InvalidRequestTitle;
                problem.Extensions[ErrorResults.ErrorCodeExtension] = InvalidRequestCode;
            }
        });

        // Development's default throws on a binding failure, which the exception handler turns into
        // a 500; answer 400 in every environment instead.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

        return services;
    }
}
