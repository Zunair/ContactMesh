using ContactMesh.Core.Logging;
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
        var result = await orchestrator.RunAsync(
            options,
            cancellationToken,
            async (progress, _) =>
            {
                await output.WriteLineAsync(SyncProgressFormatter.Format(progress)).ConfigureAwait(false);
            }).ConfigureAwait(false);

        foreach (var line in SyncRunReportFormatter.Format(result))
        {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }

        return result;
    }
}
