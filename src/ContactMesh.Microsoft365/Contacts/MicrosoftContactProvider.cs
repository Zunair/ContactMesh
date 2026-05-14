using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Contacts;

public sealed class MicrosoftContactProvider : IContactProvider
{
    private readonly MicrosoftContactBatchWriter writer;

    public MicrosoftContactProvider(MicrosoftContactBatchWriter? writer = null)
    {
        this.writer = writer ?? new MicrosoftContactBatchWriter();
    }

    public Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
    }

    public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
    {
        return this.writer.ApplyAsync(userId, changes, cancellationToken);
    }
}
