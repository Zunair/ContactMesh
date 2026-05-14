using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Google.Contacts;

public sealed class GooglePeopleContactProvider : IContactProvider
{
    private readonly GoogleContactBatchWriter writer;

    public GooglePeopleContactProvider(GoogleContactBatchWriter? writer = null)
    {
        this.writer = writer ?? new GoogleContactBatchWriter();
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
