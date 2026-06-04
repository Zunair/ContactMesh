using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContactMesh.Microsoft365.Auth;

namespace ContactMesh.Microsoft365.Groups;

public sealed class MicrosoftGraphGroupClient : IMicrosoftGraphGroupClient
{
    private const string GroupSelectFields = "id,mail,displayName,visibility,mailEnabled,securityEnabled,groupTypes";
    private const string MemberSelectFields = "id,mail,userPrincipalName,displayName,givenName,surname,companyName,department,jobTitle,proxyAddresses,businessPhones,mobilePhone";
    private static readonly Uri DefaultBaseAddress = new("https://graph.microsoft.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly IMicrosoftGraphAccessTokenProvider accessTokenProvider;
    private readonly Uri baseAddress;

    public MicrosoftGraphGroupClient(
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

    public async Task<IReadOnlyList<MicrosoftGraphGroup>> ListGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = new List<MicrosoftGraphGroup>();
        Uri? requestUri = this.BuildUri(
            "v1.0/groups",
            new Dictionary<string, string?>
            {
                ["$select"] = GroupSelectFields,
                ["$top"] = "999"
            });

        while (requestUri is not null)
        {
            using var request = await this.CreateRequestAsync(requestUri, cancellationToken).ConfigureAwait(false);
            using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<GraphCollectionResponse<GroupResource>>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            groups.AddRange((payload?.Value ?? Enumerable.Empty<GroupResource>()).Select(ToMicrosoftGraphGroup));
            requestUri = string.IsNullOrWhiteSpace(payload?.NextLink)
                ? null
                : new Uri(payload.NextLink, UriKind.Absolute);
        }

        return groups;
    }

    public async Task<IReadOnlyList<MicrosoftGraphGroupMember>> ListGroupMembersAsync(
        string groupId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        var members = new List<MicrosoftGraphGroupMember>();
        Uri? requestUri = this.BuildUri(
            $"v1.0/groups/{EscapePathSegment(groupId)}/transitiveMembers",
            new Dictionary<string, string?>
            {
                ["$select"] = MemberSelectFields,
                ["$top"] = "999"
            });

        while (requestUri is not null)
        {
            using var request = await this.CreateRequestAsync(requestUri, cancellationToken).ConfigureAwait(false);
            using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<GraphCollectionResponse<MemberResource>>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            members.AddRange((payload?.Value ?? Enumerable.Empty<MemberResource>()).Select(ToMicrosoftGraphGroupMember));
            requestUri = string.IsNullOrWhiteSpace(payload?.NextLink)
                ? null
                : new Uri(payload.NextLink, UriKind.Absolute);
        }

        return members;
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

    private static string EscapePathSegment(string value)
    {
        return Uri.EscapeDataString(value.Trim());
    }

    private static MicrosoftGraphGroup ToMicrosoftGraphGroup(GroupResource group)
    {
        return new MicrosoftGraphGroup
        {
            Id = group.Id,
            Mail = group.Mail,
            DisplayName = group.DisplayName,
            Visibility = group.Visibility,
            MailEnabled = group.MailEnabled,
            SecurityEnabled = group.SecurityEnabled,
            GroupTypes = (IReadOnlyList<string>?)group.GroupTypes ?? Array.Empty<string>()
        };
    }

    private static MicrosoftGraphGroupMember ToMicrosoftGraphGroupMember(MemberResource member)
    {
        return new MicrosoftGraphGroupMember
        {
            Id = member.Id,
            ODataType = member.ODataType,
            Mail = member.Mail,
            UserPrincipalName = member.UserPrincipalName,
            DisplayName = member.DisplayName,
            GivenName = member.GivenName,
            Surname = member.Surname,
            CompanyName = member.CompanyName,
            Department = member.Department,
            JobTitle = member.JobTitle,
            ProxyAddresses = (IReadOnlyList<string>?)member.ProxyAddresses ?? Array.Empty<string>(),
            BusinessPhones = (IReadOnlyList<string>?)member.BusinessPhones ?? Array.Empty<string>(),
            MobilePhone = member.MobilePhone
        };
    }

    private sealed class GraphCollectionResponse<T>
    {
        public List<T>? Value { get; init; }

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; init; }
    }

    private sealed class GroupResource
    {
        public string? Id { get; init; }
        public string? Mail { get; init; }
        public string? DisplayName { get; init; }
        public string? Visibility { get; init; }
        public bool? MailEnabled { get; init; }
        public bool? SecurityEnabled { get; init; }
        public List<string>? GroupTypes { get; init; }
    }

    private sealed class MemberResource
    {
        public string? Id { get; init; }

        [JsonPropertyName("@odata.type")]
        public string? ODataType { get; init; }

        public string? Mail { get; init; }
        public string? UserPrincipalName { get; init; }
        public string? DisplayName { get; init; }
        public string? GivenName { get; init; }
        public string? Surname { get; init; }
        public string? CompanyName { get; init; }
        public string? Department { get; init; }
        public string? JobTitle { get; init; }
        public List<string>? ProxyAddresses { get; init; }
        public List<string>? BusinessPhones { get; init; }
        public string? MobilePhone { get; init; }
    }
}
