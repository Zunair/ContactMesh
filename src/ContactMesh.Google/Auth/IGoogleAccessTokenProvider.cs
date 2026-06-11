// File: IGoogleAccessTokenProvider.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Google.Auth
{
    public interface IGoogleAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(string userId, CancellationToken cancellationToken);
    }
}
