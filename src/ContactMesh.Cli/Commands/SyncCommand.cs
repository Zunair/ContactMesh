using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;

namespace ContactMesh.Cli.Commands;

public sealed class SyncCommand
{
    public string Name => "sync";

    public async Task<ContactSyncRunResult> RunAsync(
        ContactMeshOptions options,
        ContactSyncOrchestrator orchestrator,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var result = await orchestrator.RunAsync(options, cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync($"Targets: {result.TargetCount}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Plan: {result.CreateCount} create, {result.UpdateCount} update, {result.DeleteCount} delete, {result.NoChangeCount} unchanged.")
            .ConfigureAwait(false);

        return result;
    }
}
