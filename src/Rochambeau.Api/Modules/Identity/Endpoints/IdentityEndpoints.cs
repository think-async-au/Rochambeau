using Rochambeau.Api.Common.Http;
using Rochambeau.Api.Modules.Identity.Application;
using Rochambeau.Api.Modules.Identity.Application.Register;
using Rochambeau.Api.Modules.Identity.Contracts;

namespace Rochambeau.Api.Modules.Identity.Endpoints;

/// <summary>The Identity HTTP surface (contracts/openapi.yaml).</summary>
internal static class IdentityEndpoints
{
    public const string OwnProfilePath = "/api/v1/me";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Identity");

        api.MapPost("/accounts", RegisterAsync)
            .WithName("registerAccount")
            .AllowAnonymous();

        return app;
    }

    /// <summary>Cap key when the connection has no remote address, so the cap is never silently skipped.</summary>
    private const string _unknownAddress = "unknown";

    private static async Task<IResult> RegisterAsync(RegistrationRequest request, RegisterAccountHandler handler, HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress?.ToString() ?? _unknownAddress;
        var result = await handler.HandleAsync(new RegisterAccountCommand(request.Username, request.Email, request.Password, address))
            .ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Created(OwnProfilePath, OwnProfileResponse.Of(result.Value))
            : result.Error!.ToProblem();
    }
}

/// <summary>Binds only the documented fields; anything else in the body (e.g. <c>role</c>) is ignored (AC-14).</summary>
internal sealed record RegistrationRequest(string? Username, string? Email, string? Password);

/// <summary>openapi.yaml · OwnProfile — a closed shape.</summary>
internal sealed record OwnProfileResponse(string Username, string Email, DateTime JoinedAt, string Role)
{
    public static OwnProfileResponse Of(OwnProfile profile) =>
        new(profile.Username, profile.Email, profile.JoinedAt.UtcDateTime, RoleNames.Of(profile.Role));
}

/// <summary>The lower-case role names of the contract (openapi.yaml · AccountRole).</summary>
internal static class RoleNames
{
    public static string Of(AccountRole role) => role switch
    {
        AccountRole.Player => "player",
        AccountRole.Administrator => "administrator",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
    };
}
