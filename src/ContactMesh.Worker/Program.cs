using ContactMesh.Hosting;
using ContactMesh.Worker.Jobs;

using var appHost = ContactMeshAppHost.Build(args);
var options = appHost.Options;

Console.WriteLine("ContactMesh Worker");
Console.WriteLine($"Provider: {options.Provider}");
Console.WriteLine($"Dry run: {options.DryRun}");
Console.WriteLine($"Config: {appHost.ConfigPath}");

var job = new ContactSyncJob(options, appHost.Orchestrator);
await job.RunAsync(CancellationToken.None).ConfigureAwait(false);
