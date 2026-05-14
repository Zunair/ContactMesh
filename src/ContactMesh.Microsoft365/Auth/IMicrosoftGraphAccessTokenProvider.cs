namespace ContactMesh.Microsoft365.Auth;

public interface IMicrosoftGraphAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
