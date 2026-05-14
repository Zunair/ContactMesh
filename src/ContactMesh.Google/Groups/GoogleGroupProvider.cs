using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Google.Groups;

public sealed class GoogleGroupProvider : IGroupProvider
{
    public Task<IReadOnlyList<MeshGroup>> GetGroupsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MeshGroup>>(Array.Empty<MeshGroup>());
    }

    public Task<IReadOnlyList<MeshContact>> GetGroupContactsAsync(string groupId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
    }
}
