namespace ContactMesh.Google.Auth;

public interface IGoogleAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(string userId, CancellationToken cancellationToken);
}
