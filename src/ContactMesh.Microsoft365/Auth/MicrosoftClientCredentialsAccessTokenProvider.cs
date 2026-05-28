namespace ContactMesh.Microsoft365.Auth;

public sealed class MicrosoftClientCredentialsAccessTokenProvider : IMicrosoftGraphAccessTokenProvider
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(50);
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);

    private readonly Microsoft365Options options;
    private readonly IMicrosoftGraphAccessTokenSource tokenSource;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private string? cachedAccessToken;
    private DateTimeOffset cachedAccessTokenExpiresAt;

    public MicrosoftClientCredentialsAccessTokenProvider(
        Microsoft365Options options,
        IMicrosoftGraphAccessTokenSource tokenSource)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokenSource);

        this.options = options;
        this.tokenSource = tokenSource;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (this.HasUsableCachedToken())
        {
            return this.cachedAccessToken!;
        }

        await this.refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.HasUsableCachedToken())
            {
                return this.cachedAccessToken!;
            }

            var request = this.CreateRequest();
            var accessToken = await this.tokenSource.GetAccessTokenAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Microsoft Graph access token source returned an empty token.");
            }

            this.cachedAccessToken = accessToken;
            this.cachedAccessTokenExpiresAt = DateTimeOffset.UtcNow.Add(CacheLifetime);

            return accessToken;
        }
        finally
        {
            this.refreshLock.Release();
        }
    }

    private bool HasUsableCachedToken()
    {
        return !string.IsNullOrWhiteSpace(this.cachedAccessToken)
            && this.cachedAccessTokenExpiresAt > DateTimeOffset.UtcNow.Add(RefreshSkew);
    }

    private MicrosoftGraphTokenRequest CreateRequest()
    {
        var tenantId = RequireOption(this.options.TenantId, "Microsoft365:TenantId");
        var clientId = RequireOption(this.options.ClientId, "Microsoft365:ClientId");
        var clientSecret = RequireOption(this.options.ClientSecret, "Microsoft365:ClientSecret");
        var scopes = NormalizeScopes(this.options.Scopes);

        return new MicrosoftGraphTokenRequest(tenantId, clientId, clientSecret, scopes);
    }

    private static string RequireOption(string? value, string optionName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{optionName} must be configured.");
        }

        return trimmed;
    }

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyList<string> configuredScopes)
    {
        var scopes = configuredScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return scopes.Length == 0
            ? Microsoft365Options.DefaultGraphScopes
            : scopes;
    }
}
