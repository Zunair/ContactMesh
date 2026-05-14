using ContactMesh.Core.Logging;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;

namespace ContactMesh.Worker.Jobs;

public sealed class ContactSyncJob
{
    private readonly ContactMeshOptions options;
    private readonly ContactSyncOrchestrator orchestrator;
    private readonly TextWriter output;

    public ContactSyncJob(
        ContactMeshOptions options,
        ContactSyncOrchestrator orchestrator,
        TextWriter? output = null)
    {
        this.options = options;
        this.orchestrator = orchestrator;
        this.output = output ?? Console.Out;
    }

    public async Task<ContactSyncRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var result = await this.orchestrator.RunAsync(this.options, cancellationToken).ConfigureAwait(false);

        foreach (var line in SyncRunReportFormatter.Format(result))
        {
            await this.output.WriteLineAsync(line).ConfigureAwait(false);
        }

        return result;
    }
}
