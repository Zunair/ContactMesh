using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using ContactMesh.Hosting;

namespace ContactMesh.Worker.Jobs;

public sealed class ContactSyncJob
{
    private readonly ContactMeshOptions options;
    private readonly ContactSyncRunPipeline pipeline;
    private readonly string? configPath;
    private readonly TextWriter output;

    public ContactSyncJob(
        ContactMeshOptions options,
        ContactSyncRunPipeline pipeline,
        string? configPath = null,
        TextWriter? output = null)
    {
        this.options = options;
        this.pipeline = pipeline;
        this.configPath = configPath;
        this.output = output ?? Console.Out;
    }

    public Task<ContactSyncRunResult?> RunAsync(CancellationToken cancellationToken)
    {
        return this.pipeline.RunAsync(this.options, this.output, hostKind: "Worker", this.configPath, cancellationToken);
    }
}
