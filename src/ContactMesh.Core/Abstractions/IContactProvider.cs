using ContactMesh.Core.Models;

namespace ContactMesh.Core.Abstractions;

public interface IContactProvider
{
    Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken);
    Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken);
}
