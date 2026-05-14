using ContactMesh.Cli.Commands;
using ContactMesh.Hosting;

using var appHost = ContactMeshAppHost.Build(args);
var options = appHost.Options;

Console.WriteLine($"ContactMesh CLI");
Console.WriteLine($"Provider: {options.Provider}");
Console.WriteLine($"Dry run: {options.DryRun}");
Console.WriteLine($"Config: {appHost.ConfigPath}");

var command = new SyncCommand();
await command.RunAsync(options, appHost.Orchestrator, Console.Out, CancellationToken.None).ConfigureAwait(false);
