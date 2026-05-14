using ContactMesh.Core.Models;

namespace ContactMesh.Google.Contacts;

public sealed class GoogleContactBatchWriter
{
    public Task ApplyAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
