// File: IGoogleDelegatedAccessTokenSource.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Google.Auth
{
    public interface IGoogleDelegatedAccessTokenSource
    {
        Task<string> GetAccessTokenAsync(
            GoogleDelegatedCredentialRequest request,
            CancellationToken cancellationToken);
    }
}
