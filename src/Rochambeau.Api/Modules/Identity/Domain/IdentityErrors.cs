using Rochambeau.Api.Common.Results;

namespace Rochambeau.Api.Modules.Identity.Domain;

/// <summary>The Identity module's expected failures (openapi.yaml · ProblemDetails.errorCode).</summary>
public static class IdentityErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized("identity.not_authenticated", "Log in or register first.");

    public static readonly Error Forbidden =
        new("identity.forbidden", "You are not allowed to do this.", ErrorType.Forbidden);
}
