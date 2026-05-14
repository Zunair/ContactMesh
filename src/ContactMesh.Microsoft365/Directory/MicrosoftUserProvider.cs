using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Directory;

public sealed class MicrosoftUserProvider : IDirectoryProvider
{
    public Task<IReadOnlyList<MeshUser>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MeshUser>>(Array.Empty<MeshUser>());
    }
}
