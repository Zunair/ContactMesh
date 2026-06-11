// File: IMicrosoftGraphAccessTokenSource.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Microsoft365.Auth
{
    public interface IMicrosoftGraphAccessTokenSource
    {
        Task<string> GetAccessTokenAsync(
            MicrosoftGraphTokenRequest request,
            CancellationToken cancellationToken);
    }
}
