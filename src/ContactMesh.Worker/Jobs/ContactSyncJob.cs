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

        await this.output.WriteLineAsync(
            $"Sync completed for {result.TargetCount} target(s): {result.CreateCount} create, {result.UpdateCount} update, {result.DeleteCount} delete, {result.NoChangeCount} unchanged.")
            .ConfigureAwait(false);

        return result;
    }
}
