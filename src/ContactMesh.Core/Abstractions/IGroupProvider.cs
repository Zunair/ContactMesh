// File: IGroupProvider.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Core.Abstractions
{
    public interface IGroupProvider
    {
        Task<IReadOnlyList<MeshGroup>> GetGroupsAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<MeshContact>> GetGroupContactsAsync(string groupId, CancellationToken cancellationToken);
    }
}
