using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Rochambeau.Api.Common.Http;
using Rochambeau.Api.Common.Results;
using Rochambeau.Api.Modules.Identity.Application;
using Rochambeau.Api.Modules.Identity.Application.LogIn;
using Rochambeau.Api.Modules.Identity.Application.Profiles;
using Rochambeau.Api.Modules.Identity.Application.Register;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity.Endpoints;

/// <summary>The Identity HTTP surface (contracts/openapi.yaml).</summary>
internal static class IdentityEndpoints
{
    public const string OwnProfilePath = "/api/v1/me";

    /// <summary>Cap key when the connection has no remote address, so the cap is never silently skipped.</summary>
    private const string _unknownAddress = "unknown";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Identity");

        api.MapPost("/accounts", RegisterAsync)
            .WithName("registerAccount")
            .AllowAnonymous();

        api.MapPost("/session", LogInAsync)
            .WithName("logIn")
            .AllowAnonymous();

        api.MapDelete("/session", LogOutAsync)
            .WithName("logOut");

        api.MapGet("/me", GetOwnProfileAsync)
            .WithName("getOwnProfile");

        return app;
    }

    private static async Task<IResult> RegisterAsync(RegistrationRequest request, RegisterAccountHandler handler, HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress?.ToString() ?? _unknownAddress;
        var result = await handler.HandleAsync(new RegisterAccountCommand(request.Username, request.Email, request.Password, address))
            .ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Created(OwnProfilePath, OwnProfileResponse.Of(result.Value))
            : result.Error!.ToProblem();
    }

    private static async Task<IResult> LogInAsync(CredentialsRequest request, LogInHandler handler, HttpContext context, TimeProvider time)
    {
        var result = await handler.HandleAsync(new LogInCommand(request.Email, request.Password)).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return TypedResults.Ok(OwnProfileResponse.Of(result.Value));
        }

        SetRetryAfter(result.Error!, context, time);
        return result.Error!.ToProblem();
    }

    /// <summary>Ends the session held by this browser only; the security stamp is not rotated (AC-07).</summary>
    private static async Task<IResult> LogOutAsync(SignInManager<Account> signIn)
    {
        await signIn.SignOutAsync().ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetOwnProfileAsync(GetOwnProfileHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(OwnProfileResponse.Of(result.Value)) : result.Error!.ToProblem();
    }

    /// <summary><c>Retry-After</c> in whole seconds until the lockout ends (openapi.yaml · logIn 423).</summary>
    private static void SetRetryAfter(Error error, HttpContext context, TimeProvider time)
    {
        if (error.Type == ErrorType.Locked
            && error.Extensions?.GetValueOrDefault(LogInHandler.LockoutEndExtension) is DateTime end)
        {
            var seconds = Math.Max(0, (int)Math.Ceiling((end - time.GetUtcNow().UtcDateTime).TotalSeconds));
            context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }
    }
}

/// <summary>Binds only the documented fields; anything else in the body (e.g. <c>role</c>) is ignored (AC-14).</summary>
internal sealed record RegistrationRequest(string? Username, string? Email, string? Password);

/// <summary>openapi.yaml · Credentials.</summary>
internal sealed record CredentialsRequest(string? Email, string? Password);

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
