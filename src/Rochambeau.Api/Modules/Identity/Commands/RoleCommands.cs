using Rochambeau.Api.Common.Commands;
using Rochambeau.Api.Modules.Identity.Application.ChangeRole;
using Rochambeau.Api.Modules.Identity.Contracts;

namespace Rochambeau.Api.Modules.Identity.Commands;

/// <summary><c>grant-admin &lt;username&gt;</c>: gives an Account the administrator role (AC-13).</summary>
public sealed class GrantAdminCommand(ChangeRoleHandler handler) : IOperatorCommand
{
    public string Name => "grant-admin";

    public Task<int> RunAsync(IReadOnlyList<string> arguments, TextWriter output, CancellationToken cancellationToken) =>
        RoleCommand.RunAsync(handler, Name, AccountRole.Administrator, arguments, output);
}

/// <summary><c>revoke-admin &lt;username&gt;</c>: makes an Administrator a Player again (AC-13b).</summary>
public sealed class RevokeAdminCommand(ChangeRoleHandler handler) : IOperatorCommand
{
    public string Name => "revoke-admin";

    public Task<int> RunAsync(IReadOnlyList<string> arguments, TextWriter output, CancellationToken cancellationToken) =>
        RoleCommand.RunAsync(handler, Name, AccountRole.Player, arguments, output);
}

internal static class RoleCommand
{
    public const int Success = 0;
    public const int NotFound = 1;
    public const int Usage = 2;

    public static async Task<int> RunAsync(
        ChangeRoleHandler handler, string name, AccountRole role, IReadOnlyList<string> arguments, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);

        if (arguments.Count != 1)
        {
            await output.WriteLineAsync($"Usage: {name} <username>").ConfigureAwait(false);
            return Usage;
        }

        var username = arguments[0];
        var result = await handler.HandleAsync(username, role).ConfigureAwait(false);
        if (result.IsFailure)
        {
            await output.WriteLineAsync($"No account has the username '{username}'. Nothing changed.").ConfigureAwait(false);
            return NotFound;
        }

        var (outcome, stored) = result.Value;
        var message = outcome == ChangeRoleOutcome.Changed
            ? $"'{stored}' is now {RoleName(role)}. All of its sessions have ended."
            : $"'{stored}' is already {RoleName(role)}. Nothing to do; sessions kept.";
        await output.WriteLineAsync(message).ConfigureAwait(false);
        return Success;
    }

    private static string RoleName(AccountRole role) => role == AccountRole.Administrator ? "an administrator" : "a player";
}
