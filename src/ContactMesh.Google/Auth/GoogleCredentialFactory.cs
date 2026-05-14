namespace ContactMesh.Google.Auth;

public sealed class GoogleCredentialFactory
{
    private readonly GoogleWorkspaceOptions options;

    public GoogleCredentialFactory(GoogleWorkspaceOptions options)
    {
        this.options = options;
    }

    public string GetCredentialPath()
    {
        return this.options.ServiceAccountFile;
    }

    public IGoogleAccessTokenProvider CreateDelegatedAccessTokenProvider(
        IGoogleDelegatedAccessTokenSource? tokenSource = null)
    {
        return new GoogleDelegatedAccessTokenProvider(this.options, tokenSource);
    }
}
