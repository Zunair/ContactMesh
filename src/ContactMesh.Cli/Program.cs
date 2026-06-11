// File: Program.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Cli.Commands;
using ContactMesh.Hosting;
using ContactMesh.Microsoft365.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContactMesh.Cli
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var commandName = args.FirstOrDefault();
            var hostArgs = string.Equals(commandName, MicrosoftContactEmailSlotCommand.Name, StringComparison.OrdinalIgnoreCase)
                ? args.Skip(1).ToArray()
                : args;

            using var appHost = ContactMeshAppHost.Build(hostArgs);
            var options = appHost.Options;

            Console.WriteLine($"ContactMesh CLI");
            Console.WriteLine($"Provider: {options.Provider}");
            Console.WriteLine($"Dry run: {options.DryRun}");
            Console.WriteLine($"Config: {appHost.ConfigPath}");

            if (string.Equals(commandName, MicrosoftContactEmailSlotCommand.Name, StringComparison.OrdinalIgnoreCase))
            {
                var microsoft365 = appHost.Services.GetRequiredService<IOptions<Microsoft365Options>>().Value;
                Environment.ExitCode = await new MicrosoftContactEmailSlotCommand()
                    .RunAsync(args.Skip(1).ToArray(), options, microsoft365, Console.Out, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            else
            {
                var command = new SyncCommand();
                await command.RunAsync(options, appHost.Pipeline, appHost.ConfigPath, Console.Out, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
