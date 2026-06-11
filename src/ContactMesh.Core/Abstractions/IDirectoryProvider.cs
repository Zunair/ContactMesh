// File: IDirectoryProvider.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Core.Abstractions
{
    public interface IDirectoryProvider
    {
        Task<IReadOnlyList<MeshUser>> GetUsersAsync(CancellationToken cancellationToken);
    }
}
