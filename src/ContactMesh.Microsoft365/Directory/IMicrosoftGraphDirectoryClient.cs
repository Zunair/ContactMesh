// File: IMicrosoftGraphDirectoryClient.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Microsoft365.Directory
{
    public interface IMicrosoftGraphDirectoryClient
    {
        Task<IReadOnlyList<MicrosoftGraphUser>> ListUsersAsync(CancellationToken cancellationToken);
    }
}
