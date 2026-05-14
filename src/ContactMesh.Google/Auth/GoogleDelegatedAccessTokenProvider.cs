namespace ContactMesh.Google.Auth;

public sealed class GoogleDelegatedAccessTokenProvider : IGoogleAccessTokenProvider
{
    private readonly GoogleWorkspaceOptions options;
    private readonly IGoogleDelegatedAccessTokenSource tokenSource;

    public GoogleDelegatedAccessTokenProvider(
        GoogleWorkspaceOptions options,
        IGoogleDelegatedAccessTokenSource? tokenSource = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        this.tokenSource = tokenSource ?? new GoogleServiceAccountTokenSource();
    }

    public async Task<string> GetAccessTokenAsync(string userId, CancellationToken cancellationToken)
    {
        var request = this.CreateRequest(userId);
        var accessToken = await this.tokenSource.GetAccessTokenAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Google delegated access token source returned an empty token.");
        }

        return accessToken;
    }

    private GoogleDelegatedCredentialRequest CreateRequest(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var serviceAccountFile = this.options.ServiceAccountFile.Trim();
        if (string.IsNullOrWhiteSpace(serviceAccountFile))
        {
            throw new InvalidOperationException("GoogleWorkspace:ServiceAccountFile must be configured.");
        }

        var subjectUserEmail = userId.Trim();
        var scopes = NormalizeScopes(this.options.Scopes);

        return new GoogleDelegatedCredentialRequest(serviceAccountFile, subjectUserEmail, scopes);
    }

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyList<string> configuredScopes)
    {
        var scopes = configuredScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return scopes.Length == 0
            ? GoogleWorkspaceOptions.DefaultPeopleApiScopes
            : scopes;
    }
}
