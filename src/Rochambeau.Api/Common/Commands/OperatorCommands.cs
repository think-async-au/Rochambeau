namespace Rochambeau.Api.Common.Commands;

/// <summary>
/// A command the portal operator runs on the host with the API executable (SAD §5 Operator command),
/// e.g. <c>Rochambeau.Api grant-admin rocky</c>. Modules register theirs in <c>Add&lt;Name&gt;Module</c>.
/// </summary>
public interface IOperatorCommand
{
    /// <summary>The first command-line argument that selects this command (matched exactly).</summary>
    string Name { get; }

    /// <summary>Runs the command with the remaining arguments and returns the process exit code.</summary>
    Task<int> RunAsync(IReadOnlyList<string> arguments, TextWriter output, CancellationToken cancellationToken);
}

/// <summary>The module-agnostic registry and dispatch of operator commands.</summary>
public static class OperatorCommandExtensions
{
    public static IServiceCollection AddOperatorCommand<TCommand>(this IServiceCollection services)
        where TCommand : class, IOperatorCommand => services.AddScoped<IOperatorCommand, TCommand>();

    /// <summary>
    /// When <paramref name="args"/> name a registered command, runs it in its own scope and returns its
    /// exit code; otherwise returns <see langword="null"/> so the web host starts normally.
    /// </summary>
    public static async Task<int?> TryRunOperatorCommandAsync(
        this IServiceProvider services, IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
        {
            return null;
        }

        await using var scope = services.CreateAsyncScope();
        var command = scope.ServiceProvider.GetServices<IOperatorCommand>()
            .FirstOrDefault(c => string.Equals(c.Name, args[0], StringComparison.Ordinal));
        if (command is null)
        {
            return null;
        }

        return await command.RunAsync([.. args.Skip(1)], output, cancellationToken).ConfigureAwait(false);
    }
}
