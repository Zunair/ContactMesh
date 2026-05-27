using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using ContactMesh.Hosting;

namespace ContactMesh.Cli.Commands;

public sealed class SyncCommand
{
    public string Name => "sync";

    public async Task<ContactSyncRunResult?> RunAsync(
        ContactMeshOptions options,
        ContactSyncRunPipeline pipeline,
        string? configPath,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        return await pipeline
            .RunAsync(options, output, hostKind: "Cli", configPath, cancellationToken)
            .ConfigureAwait(false);
    }
}
