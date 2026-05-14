using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContactMesh.Google.Auth;

namespace ContactMesh.Google.Contacts;

public sealed class GooglePeopleContactGroupLabelClient : IGoogleContactGroupLabelClient
{
    private static readonly Uri DefaultBaseAddress = new("https://people.googleapis.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;
    private readonly IGoogleAccessTokenProvider accessTokenProvider;
    private readonly Uri baseAddress;

    public GooglePeopleContactGroupLabelClient(
        HttpClient httpClient,
        IGoogleAccessTokenProvider accessTokenProvider,
        Uri? baseAddress = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        this.httpClient = httpClient;
        this.accessTokenProvider = accessTokenProvider;
        this.baseAddress = baseAddress ?? httpClient.BaseAddress ?? DefaultBaseAddress;
    }

    public async Task<IReadOnlyList<GoogleContactGroupLabel>> ListAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var labels = new List<GoogleContactGroupLabel>();
        string? pageToken = null;

        do
        {
            var requestUri = this.BuildUri(
                "v1/contactGroups",
                new Dictionary<string, string?>
                {
                    ["groupFields"] = "clientData,name",
                    ["pageSize"] = "1000",
                    ["pageToken"] = pageToken
                });
            using var request = await this.CreateRequestAsync(
                HttpMethod.Get,
                requestUri,
                userId,
                cancellationToken).ConfigureAwait(false);
            using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ContactGroupsListResponse>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            foreach (var group in payload?.ContactGroups ?? Enumerable.Empty<ContactGroupResource>())
            {
                labels.Add(ToGoogleContactGroupLabel(group));
            }

            pageToken = payload?.NextPageToken;
        }
        while (!string.IsNullOrWhiteSpace(pageToken));

        return labels;
    }

    public async Task CreateAsync(
        string userId,
        string labelName,
        IReadOnlyDictionary<string, string> clientData,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelName);
        ArgumentNullException.ThrowIfNull(clientData);

        var requestUri = this.BuildUri(
            "v1/contactGroups",
            new Dictionary<string, string?> { ["readGroupFields"] = "clientData,name" });
        var payload = new ContactGroupMutationRequest
        {
            ContactGroup = ToContactGroupResource(labelName, clientData)
        };

        await this.SendJsonAsync(
            HttpMethod.Post,
            requestUri,
            userId,
            payload,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        string userId,
        string resourceName,
        string labelName,
        IReadOnlyDictionary<string, string> clientData,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelName);
        ArgumentNullException.ThrowIfNull(clientData);

        var requestUri = this.BuildUri(
            $"v1/{NormalizeResourceName(resourceName)}",
            new Dictionary<string, string?>
            {
                ["readGroupFields"] = "clientData,name",
                ["updateGroupFields"] = "clientData,name"
            });
        var payload = new ContactGroupMutationRequest
        {
            ContactGroup = ToContactGroupResource(labelName, clientData, resourceName)
        };

        await this.SendJsonAsync(
            HttpMethod.Patch,
            requestUri,
            userId,
            payload,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string userId, string resourceName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var requestUri = this.BuildUri(
            $"v1/{NormalizeResourceName(resourceName)}",
            new Dictionary<string, string?> { ["deleteContacts"] = "false" });
        using var request = await this.CreateRequestAsync(
            HttpMethod.Delete,
            requestUri,
            userId,
            cancellationToken).ConfigureAwait(false);
        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    private async Task SendJsonAsync<TPayload>(
        HttpMethod method,
        Uri requestUri,
        string userId,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        using var request = await this.CreateRequestAsync(method, requestUri, userId, cancellationToken)
            .ConfigureAwait(false);
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        Uri requestUri,
        string userId,
        CancellationToken cancellationToken)
    {
        var accessToken = await this.accessTokenProvider.GetAccessTokenAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Google access token provider returned an empty token.");
        }

        var request = new HttpRequestMessage(method, requestUri);
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

    private static string NormalizeResourceName(string resourceName)
    {
        var trimmed = resourceName.Trim();
        if (!trimmed.StartsWith("contactGroups/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Contact group resource names must start with 'contactGroups/'.",
                nameof(resourceName));
        }

        return trimmed;
    }

    private static GoogleContactGroupLabel ToGoogleContactGroupLabel(ContactGroupResource group)
    {
        return new GoogleContactGroupLabel(
            group.ResourceName,
            group.Name ?? string.Empty,
            (group.ClientData ?? Enumerable.Empty<ClientDataResource>())
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(
                    item => item.Key!,
                    item => item.Value ?? string.Empty,
                    StringComparer.Ordinal));
    }

    private static ContactGroupResource ToContactGroupResource(
        string labelName,
        IReadOnlyDictionary<string, string> clientData,
        string? resourceName = null)
    {
        return new ContactGroupResource
        {
            ResourceName = resourceName,
            Name = labelName.Trim(),
            ClientData = clientData
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ClientDataResource { Key = pair.Key, Value = pair.Value })
                .ToList()
        };
    }

    private sealed class ContactGroupsListResponse
    {
        public List<ContactGroupResource>? ContactGroups { get; init; }
        public string? NextPageToken { get; init; }
    }

    private sealed class ContactGroupMutationRequest
    {
        public ContactGroupResource? ContactGroup { get; init; }
    }

    private sealed class ContactGroupResource
    {
        public string? ResourceName { get; init; }
        public string? Name { get; init; }
        public List<ClientDataResource>? ClientData { get; init; }
    }

    private sealed class ClientDataResource
    {
        public string? Key { get; init; }
        public string? Value { get; init; }
    }
}
