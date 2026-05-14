namespace ContactMesh.Worker.Jobs;

public sealed class ContactSyncJob
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
