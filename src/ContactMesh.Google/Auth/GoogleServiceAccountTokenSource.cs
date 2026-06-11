// File: GoogleServiceAccountTokenSource.cs
// Author: Zunair
// Producer: Copilot

using Google.Apis.Auth.OAuth2;

namespace ContactMesh.Google.Auth
{
    public sealed class GoogleServiceAccountTokenSource : IGoogleDelegatedAccessTokenSource
    {
        public async Task<string> GetAccessTokenAsync(
            GoogleDelegatedCredentialRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var serviceAccountCredential = await CredentialFactory
                .FromFileAsync<ServiceAccountCredential>(request.ServiceAccountFile, cancellationToken)
                .ConfigureAwait(false);

            var credential = serviceAccountCredential
                .ToGoogleCredential()
                .CreateScoped(request.Scopes)
                .CreateWithUser(request.SubjectUserEmail);

            return await credential
                .UnderlyingCredential
                .GetAccessTokenForRequestAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
