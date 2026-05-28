using ContactMesh.Microsoft365.Auth;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftClientCredentialsAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_Normalizes_Configured_Client_Credentials_Request()
    {
        var tokenSource = new FakeGraphAccessTokenSource("graph-token");
        var provider = new MicrosoftClientCredentialsAccessTokenProvider(
            new Microsoft365Options
            {
                TenantId = " tenant-id ",
                ClientId = " client-id ",
                ClientSecret = " client-secret ",
                Scopes = new[] { " scope.b ", "scope.a", "scope.a", " " }
            },
            tokenSource);

        var token = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("graph-token", token);
        var request = Assert.Single(tokenSource.Requests);
        Assert.Equal("tenant-id", request.TenantId);
        Assert.Equal("client-id", request.ClientId);
        Assert.Equal("client-secret", request.ClientSecret);
        Assert.Equal(new[] { "scope.b", "scope.a" }, request.Scopes);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Defaults_To_Graph_Default_Scope()
    {
        var tokenSource = new FakeGraphAccessTokenSource("graph-token");
        var provider = new MicrosoftClientCredentialsAccessTokenProvider(
            new Microsoft365Options
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            },
            tokenSource);

        await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(
            Microsoft365Options.DefaultGraphScopes,
            Assert.Single(tokenSource.Requests).Scopes);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Reuses_Cached_Token()
    {
        var tokenSource = new FakeGraphAccessTokenSource("graph-token");
        var provider = new MicrosoftClientCredentialsAccessTokenProvider(
            new Microsoft365Options
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            },
            tokenSource);

        var first = await provider.GetAccessTokenAsync(CancellationToken.None);
        var second = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("graph-token", first);
        Assert.Equal("graph-token", second);
        Assert.Single(tokenSource.Requests);
    }

    [Theory]
    [InlineData(null, "client-id", "client-secret", "Microsoft365:TenantId must be configured.")]
    [InlineData("tenant-id", null, "client-secret", "Microsoft365:ClientId must be configured.")]
    [InlineData("tenant-id", "client-id", null, "Microsoft365:ClientSecret must be configured.")]
    [InlineData(" ", "client-id", "client-secret", "Microsoft365:TenantId must be configured.")]
    public async Task GetAccessTokenAsync_Rejects_Missing_Required_Options(
        string? tenantId,
        string? clientId,
        string? clientSecret,
        string expectedMessage)
    {
        var provider = new MicrosoftClientCredentialsAccessTokenProvider(
            new Microsoft365Options
            {
                TenantId = tenantId,
                ClientId = clientId,
                ClientSecret = clientSecret
            },
            new FakeGraphAccessTokenSource("graph-token"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync(CancellationToken.None));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Rejects_Empty_Token_Source_Response()
    {
        var provider = new MicrosoftClientCredentialsAccessTokenProvider(
            new Microsoft365Options
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            },
            new FakeGraphAccessTokenSource(" "));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync(CancellationToken.None));

        Assert.Equal("Microsoft Graph access token source returned an empty token.", ex.Message);
    }

    private sealed class FakeGraphAccessTokenSource : IMicrosoftGraphAccessTokenSource
    {
        private readonly string token;

        public FakeGraphAccessTokenSource(string token)
        {
            this.token = token;
        }

        public List<MicrosoftGraphTokenRequest> Requests { get; } = new();

        public Task<string> GetAccessTokenAsync(
            MicrosoftGraphTokenRequest request,
            CancellationToken cancellationToken)
        {
            this.Requests.Add(request);

            return Task.FromResult(this.token);
        }
    }
}
