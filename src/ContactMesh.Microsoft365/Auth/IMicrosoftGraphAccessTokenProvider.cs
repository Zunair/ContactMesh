// File: IMicrosoftGraphAccessTokenProvider.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Microsoft365.Auth
{
    public interface IMicrosoftGraphAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    }
}
