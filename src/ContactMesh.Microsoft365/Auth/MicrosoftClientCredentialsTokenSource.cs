// File: MicrosoftClientCredentialsTokenSource.cs
// Author: Zunair
// Producer: Copilot

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContactMesh.Microsoft365.Auth
{
    public sealed class MicrosoftClientCredentialsTokenSource : IMicrosoftGraphAccessTokenSource
    {
        private static readonly Uri DefaultAuthorityBaseAddress = new("https://login.microsoftonline.com/");
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient httpClient;
        private readonly Uri authorityBaseAddress;

        public MicrosoftClientCredentialsTokenSource(HttpClient httpClient, Uri? authorityBaseAddress = null)
        {
            ArgumentNullException.ThrowIfNull(httpClient);

            this.httpClient = httpClient;
            this.authorityBaseAddress = authorityBaseAddress ?? httpClient.BaseAddress ?? DefaultAuthorityBaseAddress;
        }

        public async Task<string> GetAccessTokenAsync(
            MicrosoftGraphTokenRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var response = await this.httpClient.PostAsync(
                this.BuildTokenUri(request.TenantId),
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = request.ClientId,
                    ["client_secret"] = request.ClientSecret,
                    ["scope"] = string.Join(" ", request.Scopes),
                    ["grant_type"] = "client_credentials"
                }),
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            return payload?.AccessToken ?? string.Empty;
        }

        private Uri BuildTokenUri(string tenantId)
        {
            return new Uri(
                this.authorityBaseAddress,
                $"{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token");
        }

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; init; }
        }
    }
}
