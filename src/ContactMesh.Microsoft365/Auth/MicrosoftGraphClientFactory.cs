// File: MicrosoftGraphClientFactory.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Microsoft365.Auth
{
    public sealed class MicrosoftGraphClientFactory
    {
        private readonly Microsoft365Options options;

        public MicrosoftGraphClientFactory(Microsoft365Options options)
        {
            this.options = options;
        }

        public string? GetTenantId()
        {
            return this.options.TenantId;
        }

        public IMicrosoftGraphAccessTokenProvider CreateAccessTokenProvider(HttpClient httpClient)
        {
            return new MicrosoftClientCredentialsAccessTokenProvider(
                this.options,
                new MicrosoftClientCredentialsTokenSource(httpClient));
        }
    }
}
