using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Groups;

public sealed class MicrosoftGroupProvider : IGroupProvider
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
