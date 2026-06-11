// File: Program.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Hosting;
using ContactMesh.Worker.Jobs;

namespace ContactMesh.Worker
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            using var appHost = ContactMeshAppHost.Build(args);
            var options = appHost.Options;

            Console.WriteLine("ContactMesh Worker");
            Console.WriteLine($"Provider: {options.Provider}");
            Console.WriteLine($"Dry run: {options.DryRun}");
            Console.WriteLine($"Config: {appHost.ConfigPath}");

            var job = new ContactSyncJob(options, appHost.Pipeline, appHost.ConfigPath);
            await job.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
