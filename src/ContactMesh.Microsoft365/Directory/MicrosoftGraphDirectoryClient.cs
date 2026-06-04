using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContactMesh.Microsoft365.Auth;

namespace ContactMesh.Microsoft365.Directory;

public sealed class MicrosoftGraphDirectoryClient : IMicrosoftGraphDirectoryClient
{
    private const string UserSelectFields = "id,mail,userPrincipalName,displayName,givenName,surname,companyName,department,jobTitle,proxyAddresses,businessPhones,mobilePhone,accountEnabled,userType";
    private static readonly Uri DefaultBaseAddress = new("https://graph.microsoft.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly IMicrosoftGraphAccessTokenProvider accessTokenProvider;
    private readonly Uri baseAddress;

    public MicrosoftGraphDirectoryClient(
        HttpClient httpClient,
        IMicrosoftGraphAccessTokenProvider accessTokenProvider,
        Uri? baseAddress = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        this.httpClient = httpClient;
        this.accessTokenProvider = accessTokenProvider;
        this.baseAddress = baseAddress ?? httpClient.BaseAddress ?? DefaultBaseAddress;
    }

    public async Task<IReadOnlyList<MicrosoftGraphUser>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = new List<MicrosoftGraphUser>();
        Uri? requestUri = this.BuildUri(
            "v1.0/users",
            new Dictionary<string, string?>
            {
                ["$select"] = UserSelectFields,
                ["$top"] = "999"
            });

        while (requestUri is not null)
        {
            using var request = await this.CreateRequestAsync(requestUri, cancellationToken).ConfigureAwait(false);
            using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<GraphCollectionResponse<MicrosoftGraphUser>>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            users.AddRange(payload?.Value ?? Enumerable.Empty<MicrosoftGraphUser>());
            requestUri = string.IsNullOrWhiteSpace(payload?.NextLink)
                ? null
                : new Uri(payload.NextLink, UriKind.Absolute);
        }

        return users;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        var accessToken = await this.accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Microsoft Graph access token provider returned an empty token.");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    private Uri BuildUri(string relativePath, IReadOnlyDictionary<string, string?> query)
    {
        var builder = new UriBuilder(new Uri(this.baseAddress, relativePath));
        var queryString = string.Join(
            "&",
            query
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        builder.Query = queryString;

        return builder.Uri;
    }

    private sealed class GraphCollectionResponse<T>
    {
        public List<T>? Value { get; init; }

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; init; }
    }
}
