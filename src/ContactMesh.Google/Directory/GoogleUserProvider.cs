// File: GoogleUserProvider.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Google.Directory
{
    public sealed class GoogleUserProvider : IDirectoryProvider
    {
        public Task<IReadOnlyList<MeshUser>> GetUsersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<MeshUser>>(Array.Empty<MeshUser>());
        }
    }
}
