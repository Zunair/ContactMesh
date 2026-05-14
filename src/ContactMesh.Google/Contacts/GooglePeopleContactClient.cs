using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContactMesh.Google.Auth;

namespace ContactMesh.Google.Contacts;

public sealed class GooglePeopleContactClient : IGooglePeopleContactClient
{
    private const string PersonFields = "names,emailAddresses,phoneNumbers,organizations,memberships,metadata,clientData";
    private static readonly Uri DefaultBaseAddress = new("https://people.googleapis.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;
    private readonly IGoogleAccessTokenProvider accessTokenProvider;
    private readonly Uri baseAddress;

    public GooglePeopleContactClient(
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

    public async Task<IReadOnlyList<GooglePersonContact>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var contacts = new List<GooglePersonContact>();
        string? pageToken = null;

        do
        {
            var requestUri = this.BuildUri(
                "v1/people/me/connections",
                new Dictionary<string, string?>
                {
                    ["personFields"] = PersonFields,
                    ["pageSize"] = "1000",
                    ["pageToken"] = pageToken
                });
            using var request = await this.CreateRequestAsync(HttpMethod.Get, requestUri, userId, cancellationToken)
                .ConfigureAwait(false);
            using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ConnectionsListResponse>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            foreach (var person in payload?.Connections ?? Enumerable.Empty<PersonResource>())
            {
                contacts.Add(ToGooglePersonContact(person));
            }

            pageToken = payload?.NextPageToken;
        }
        while (!string.IsNullOrWhiteSpace(pageToken));

        return contacts;
    }

    public Task CreateAsync(string userId, GooglePersonContact contact, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(contact);

        var requestUri = this.BuildUri(
            "v1/people:createContact",
            new Dictionary<string, string?> { ["personFields"] = PersonFields });

        return this.SendJsonAsync(HttpMethod.Post, requestUri, userId, ToPersonResource(contact), cancellationToken);
    }

    public Task UpdateAsync(string userId, GooglePersonContact contact, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentException.ThrowIfNullOrWhiteSpace(contact.ResourceName);

        var requestUri = this.BuildUri(
            $"v1/{NormalizePersonResourceName(contact.ResourceName)}:updateContact",
            new Dictionary<string, string?>
            {
                ["personFields"] = PersonFields,
                ["updatePersonFields"] = PersonFields
            });

        return this.SendJsonAsync(HttpMethod.Patch, requestUri, userId, ToPersonResource(contact), cancellationToken);
    }

    public async Task DeleteAsync(string userId, string resourceName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var requestUri = this.BuildUri(
            $"v1/{NormalizePersonResourceName(resourceName)}:deleteContact",
            new Dictionary<string, string?>());
        using var request = await this.CreateRequestAsync(HttpMethod.Delete, requestUri, userId, cancellationToken)
            .ConfigureAwait(false);
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

    private static string NormalizePersonResourceName(string resourceName)
    {
        var trimmed = resourceName.Trim();
        if (!trimmed.StartsWith("people/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "People contact resource names must start with 'people/'.",
                nameof(resourceName));
        }

        return trimmed;
    }

    private static GooglePersonContact ToGooglePersonContact(PersonResource person)
    {
        var name = person.Names?.FirstOrDefault();
        var organization = person.Organizations?.FirstOrDefault();

        return new GooglePersonContact
        {
            ResourceName = person.ResourceName,
            ETag = person.ETag,
            SourceId = GetClientData(person, GoogleContactMapper.SourceIdClientDataKey),
            DisplayName = name?.DisplayName,
            GivenName = name?.GivenName,
            FamilyName = name?.FamilyName,
            CompanyName = organization?.Name,
            Department = organization?.Department,
            JobTitle = organization?.Title,
            Emails = (person.EmailAddresses ?? Enumerable.Empty<EmailAddressResource>())
                .Where(email => !string.IsNullOrWhiteSpace(email.Value))
                .Select(email => new GooglePersonEmail(email.Value!, email.Type ?? "work", email.Metadata?.Primary == true))
                .ToList(),
            Phones = (person.PhoneNumbers ?? Enumerable.Empty<PhoneNumberResource>())
                .Where(phone => !string.IsNullOrWhiteSpace(phone.Value))
                .Select(phone => new GooglePersonPhone(phone.Value!, phone.Type ?? "work", phone.Metadata?.Primary == true))
                .ToList(),
            ContactGroupResourceNames = (person.Memberships ?? Enumerable.Empty<MembershipResource>())
                .Select(membership => membership.ContactGroupMembership?.ContactGroupResourceName)
                .Where(resourceName => !string.IsNullOrWhiteSpace(resourceName))
                .Select(resourceName => resourceName!)
                .ToList()
        };
    }

    private static PersonResource ToPersonResource(GooglePersonContact contact)
    {
        return new PersonResource
        {
            ResourceName = contact.ResourceName,
            ETag = contact.ETag,
            Names = string.IsNullOrWhiteSpace(contact.DisplayName)
                && string.IsNullOrWhiteSpace(contact.GivenName)
                && string.IsNullOrWhiteSpace(contact.FamilyName)
                    ? null
                    : new List<NameResource>
                    {
                        new NameResource
                        {
                            DisplayName = contact.DisplayName,
                            GivenName = contact.GivenName,
                            FamilyName = contact.FamilyName
                        }
                    },
            Organizations = string.IsNullOrWhiteSpace(contact.CompanyName)
                && string.IsNullOrWhiteSpace(contact.Department)
                && string.IsNullOrWhiteSpace(contact.JobTitle)
                    ? null
                    : new List<OrganizationResource>
                    {
                        new OrganizationResource
                        {
                            Name = contact.CompanyName,
                            Department = contact.Department,
                            Title = contact.JobTitle
                        }
                    },
            EmailAddresses = contact.Emails
                .Where(email => !string.IsNullOrWhiteSpace(email.Address))
                .Select(email => new EmailAddressResource
                {
                    Value = email.Address,
                    Type = email.Type,
                    Metadata = new FieldMetadataResource { Primary = email.IsPrimary }
                })
                .ToList(),
            PhoneNumbers = contact.Phones
                .Where(phone => !string.IsNullOrWhiteSpace(phone.Number))
                .Select(phone => new PhoneNumberResource
                {
                    Value = phone.Number,
                    Type = phone.Type,
                    Metadata = new FieldMetadataResource { Primary = phone.IsPrimary }
                })
                .ToList(),
            Memberships = contact.ContactGroupResourceNames
                .Where(resourceName => !string.IsNullOrWhiteSpace(resourceName))
                .Select(resourceName => new MembershipResource
                {
                    ContactGroupMembership = new ContactGroupMembershipResource
                    {
                        ContactGroupResourceName = resourceName
                    }
                })
                .ToList(),
            ClientData = string.IsNullOrWhiteSpace(contact.SourceId)
                ? null
                : new List<ClientDataResource>
                {
                    new ClientDataResource
                    {
                        Key = GoogleContactMapper.SourceIdClientDataKey,
                        Value = contact.SourceId
                    }
                }
        };
    }

    private static string? GetClientData(PersonResource person, string key)
    {
        return person.ClientData?.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal))?.Value;
    }

    private sealed class ConnectionsListResponse
    {
        public List<PersonResource>? Connections { get; init; }
        public string? NextPageToken { get; init; }
    }

    private sealed class PersonResource
    {
        public string? ResourceName { get; init; }
        [JsonPropertyName("etag")]
        public string? ETag { get; init; }
        public List<NameResource>? Names { get; init; }
        public List<EmailAddressResource>? EmailAddresses { get; init; }
        public List<PhoneNumberResource>? PhoneNumbers { get; init; }
        public List<OrganizationResource>? Organizations { get; init; }
        public List<MembershipResource>? Memberships { get; init; }
        public List<ClientDataResource>? ClientData { get; init; }
    }

    private sealed class NameResource
    {
        public string? DisplayName { get; init; }
        public string? GivenName { get; init; }
        public string? FamilyName { get; init; }
    }

    private sealed class EmailAddressResource
    {
        public string? Value { get; init; }
        public string? Type { get; init; }
        public FieldMetadataResource? Metadata { get; init; }
    }

    private sealed class PhoneNumberResource
    {
        public string? Value { get; init; }
        public string? Type { get; init; }
        public FieldMetadataResource? Metadata { get; init; }
    }

    private sealed class OrganizationResource
    {
        public string? Name { get; init; }
        public string? Department { get; init; }
        public string? Title { get; init; }
    }

    private sealed class MembershipResource
    {
        public ContactGroupMembershipResource? ContactGroupMembership { get; init; }
    }

    private sealed class ContactGroupMembershipResource
    {
        public string? ContactGroupResourceName { get; init; }
    }

    private sealed class FieldMetadataResource
    {
        public bool? Primary { get; init; }
    }

    private sealed class ClientDataResource
    {
        public string? Key { get; init; }
        public string? Value { get; init; }
    }
}
