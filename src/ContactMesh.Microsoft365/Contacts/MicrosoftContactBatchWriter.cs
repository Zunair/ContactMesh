using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Contacts;

public sealed class MicrosoftContactBatchWriter
{
    public Task ApplyAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
