using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Domain;
using Rochambeau.Api.Modules.Identity.Infrastructure;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

namespace Rochambeau.Api.Modules.Identity;

/// <summary>Wires the Identity module: accounts, cookie sessions, roles and the identity contract for other modules.</summary>
public static class IdentityModule
{
    public const string DataProtectionKeysPathKey = "DataProtection:KeysPath";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddIdentityCore<Account>(options =>
            {
                options.Password.RequiredLength = Password.MinLength;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = LockoutPolicy.MaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutPolicy.Duration;
                options.Lockout.AllowedForNewUsers = true;

                // The unique indexes are the race guard (SAD §6); Domain owns the username alphabet.
                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<AccountClaimsPrincipalFactory>();

        services.AddSessionCookie();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentAccount, CurrentAccount>();
        services.AddScoped<IAccountDirectory, AccountDirectory>();

        // Deny by default (SAD §8): every endpoint needs a logged-in Account unless it opts out.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        var keysPath = configuration[DataProtectionKeysPathKey]
            ?? throw new InvalidOperationException($"'{DataProtectionKeysPathKey}' is not configured.");
        var contentRoot = configuration[HostDefaults.ContentRootKey] ?? Directory.GetCurrentDirectory();
        services.AddDataProtection()
            .SetApplicationName("Rochambeau")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keysPath, contentRoot)));

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder app) => app;
}
