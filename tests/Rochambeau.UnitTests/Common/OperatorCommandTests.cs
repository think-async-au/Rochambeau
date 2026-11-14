using Microsoft.Extensions.DependencyInjection;
using Rochambeau.Api.Common.Commands;

namespace Rochambeau.UnitTests.Common;

/// <summary>The generic operator-command hook (SAD §1 override, §5 Operator command).</summary>
public class OperatorCommandTests
{
    [Fact]
    public async Task Arguments_naming_a_registered_command_run_it_and_return_its_exit_code()
    {
        await using var services = new ServiceCollection().AddOperatorCommand<EchoCommand>().BuildServiceProvider();
        using var output = new StringWriter();

        var exitCode = await services.TryRunOperatorCommandAsync(["echo", "hello", "world"], output);

        Assert.Equal(7, exitCode);
        Assert.Equal("hello world", output.ToString().Trim());
    }

    [Theory]
    [InlineData]
    [InlineData("unknown-command")]
    [InlineData("--urls", "http://localhost:5000")]
    public async Task Arguments_naming_no_registered_command_return_null_so_the_web_host_starts(params string[] args)
    {
        await using var services = new ServiceCollection().AddOperatorCommand<EchoCommand>().BuildServiceProvider();
        using var output = new StringWriter();

        Assert.Null(await services.TryRunOperatorCommandAsync(args, output));
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task Command_names_are_matched_exactly()
    {
        await using var services = new ServiceCollection().AddOperatorCommand<EchoCommand>().BuildServiceProvider();
        using var output = new StringWriter();

        Assert.Null(await services.TryRunOperatorCommandAsync(["ECHO"], output));
    }

    private sealed class EchoCommand : IOperatorCommand
    {
        public string Name => "echo";

        public Task<int> RunAsync(IReadOnlyList<string> arguments, TextWriter output, CancellationToken cancellationToken)
        {
            output.WriteLine(string.Join(' ', arguments));
            return Task.FromResult(7);
        }
    }
}
