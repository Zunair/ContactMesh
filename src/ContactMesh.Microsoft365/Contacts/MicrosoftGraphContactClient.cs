using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContactMesh.Microsoft365.Auth;

namespace ContactMesh.Microsoft365.Contacts;

public sealed class MicrosoftGraphContactClient : IMicrosoftGraphContactClient
{
    private const string ContactSelectFields = "id,changeKey,displayName,givenName,surname,companyName,department,jobTitle,primaryEmailAddress,secondaryEmailAddress,tertiaryEmailAddress,emailAddresses,businessPhones,mobilePhone,categories,personalNotes";
    private const string BetaContactSelectFields = "id,displayName,emailAddresses";
    private static readonly Uri DefaultBaseAddress = new("https://graph.microsoft.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;
    private readonly IMicrosoftGraphAccessTokenProvider accessTokenProvider;
    private readonly Uri baseAddress;

    public MicrosoftGraphContactClient(
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

    public async Task<IReadOnlyList<MicrosoftGraphContact>> ListAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var contacts = new List<MicrosoftGraphContact>();
        Uri? requestUri = this.BuildUri(
            $"v1.0/users/{EscapePathSegment(userId)}/contacts",
            new Dictionary<string, string?>
            {
                ["$select"] = ContactSelectFields,
                ["$expand"] = $"singleValueExtendedProperties($filter=id eq '{MicrosoftContactMapper.SourceIdExtendedPropertyId}')",
                ["$top"] = "999"
            });

        while (requestUri is not null)
        {
            using var request = await this.CreateRequestAsync(HttpMethod.Get, requestUri, cancellationToken)
                .ConfigureAwait(false);
            using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            await EnsureSuccessAsync(response, request, cancellationToken).ConfigureAwait(false);

            //var payload1 = await response.Content.ReadAsStringAsync();
            var payload = await response.Content.ReadFromJsonAsync<GraphCollectionResponse<ContactResource>>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            contacts.AddRange((payload?.Value ?? Enumerable.Empty<ContactResource>()).Select(ToMicrosoftGraphContact));
            requestUri = string.IsNullOrWhiteSpace(payload?.NextLink)
                ? null
                : new Uri(payload.NextLink, UriKind.Absolute);
        }

        return contacts;
    }

    public async Task<MicrosoftGraphContact> GetContactBetaAsync(
        string userId,
        string contactId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);

        var requestUri = this.BuildUri(
            $"beta/users/{EscapePathSegment(userId)}/contacts/{EscapePathSegment(contactId)}",
            new Dictionary<string, string?>
            {
                ["$select"] = BetaContactSelectFields
            });

        using var request = await this.CreateRequestAsync(HttpMethod.Get, requestUri, cancellationToken)
            .ConfigureAwait(false);
        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, request, cancellationToken).ConfigureAwait(false);

        var contact = await response.Content.ReadFromJsonAsync<ContactResource>(
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return ToMicrosoftGraphContact(contact ?? new ContactResource());
    }


    public Task CreateAsync(
        string userId,
        MicrosoftGraphContact contact,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(contact);

        var requestUri = this.BuildUri(
            $"v1.0/users/{EscapePathSegment(userId)}/contacts",
            new Dictionary<string, string?>());

        return this.SendJsonAsync(
            HttpMethod.Post,
            requestUri,
            ToContactResource(contact),
            cancellationToken);
    }

    public Task UpdateAsync(
        string userId,
        MicrosoftGraphContact contact,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentException.ThrowIfNullOrWhiteSpace(contact.Id);

        var requestUri = this.BuildUri(
            $"v1.0/users/{EscapePathSegment(userId)}/contacts/{EscapePathSegment(contact.Id)}",
            new Dictionary<string, string?>());

        return this.SendJsonAsync(
            HttpMethod.Patch,
            requestUri,
            ToContactResource(contact),
            cancellationToken);
    }

    public Task UpdateEmailAddressesBetaAsync(
        string userId,
        string contactId,
        IReadOnlyList<MicrosoftGraphEmailAddress> emailAddresses,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        ArgumentNullException.ThrowIfNull(emailAddresses);

        var requestUri = this.BuildUri(
            $"beta/users/{EscapePathSegment(userId)}/contacts/{EscapePathSegment(contactId)}",
            new Dictionary<string, string?>());

        return this.SendJsonAsync(
            HttpMethod.Patch,
            requestUri,
            new BetaContactEmailAddressResource
            {
                EmailAddresses = emailAddresses
                    .Where(email => !string.IsNullOrWhiteSpace(email.Address))
                    .Select(email => new EmailAddressResource
                    {
                        Address = email.Address.Trim(),
                        Name = email.Name,
                        Type = string.IsNullOrWhiteSpace(email.Type) ? "unknown" : email.Type.Trim()
                    })
                    .ToList()
            },
            cancellationToken);
    }

    public async Task DeleteAsync(string userId, string contactId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);

        var requestUri = this.BuildUri(
            $"v1.0/users/{EscapePathSegment(userId)}/contacts/{EscapePathSegment(contactId)}",
            new Dictionary<string, string?>());
        using var request = await this.CreateRequestAsync(HttpMethod.Delete, requestUri, cancellationToken)
            .ConfigureAwait(false);
        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendJsonAsync<TPayload>(
        HttpMethod method,
        Uri requestUri,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        using var request = await this.CreateRequestAsync(method, requestUri, cancellationToken)
            .ConfigureAwait(false);
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        var accessToken = await this.accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Microsoft Graph access token provider returned an empty token.");
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

    private static string EscapePathSegment(string value)
    {
        return Uri.EscapeDataString(value.Trim());
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var bodyExcerpt = body.Length <= 2048 ? body : body[..2048];
        var requestUri = request.RequestUri?.PathAndQuery ?? request.RequestUri?.ToString() ?? "(unknown URI)";

        throw new HttpRequestException(
            $"Microsoft Graph contact request failed: {(int)response.StatusCode} ({response.ReasonPhrase}) for {request.Method} {requestUri}. Response: {bodyExcerpt}");
    }

    private static MicrosoftGraphContact ToMicrosoftGraphContact(ContactResource contact)
    {
        return new MicrosoftGraphContact
        {
            Id = contact.Id,
            ChangeKey = contact.ChangeKey,
            SourceId = GetSourceId(contact),
            DisplayName = contact.DisplayName,
            GivenName = contact.GivenName,
            Surname = contact.Surname,
            CompanyName = contact.CompanyName,
            Department = contact.Department,
            JobTitle = contact.JobTitle,
            PrimaryEmailAddress = ToMicrosoftGraphEmailAddress(contact.PrimaryEmailAddress),
            SecondaryEmailAddress = ToMicrosoftGraphEmailAddress(contact.SecondaryEmailAddress),
            TertiaryEmailAddress = ToMicrosoftGraphEmailAddress(contact.TertiaryEmailAddress),
            EmailAddresses = ToEmailAddresses(contact.EmailAddresses).ToList(),
            BusinessPhones = (contact.BusinessPhones ?? Enumerable.Empty<string>())
                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                .Select(phone => phone.Trim())
                .ToList(),
            MobilePhone = contact.MobilePhone,
            Categories = (contact.Categories ?? Enumerable.Empty<string>())
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category.Trim())
                .ToList(),
            PersonalNotes = contact.PersonalNotes
        };
    }

    private static ContactResource ToContactResource(MicrosoftGraphContact contact)
    {
        return new ContactResource
        {
            DisplayName = contact.DisplayName,
            GivenName = contact.GivenName,
            Surname = contact.Surname,
            CompanyName = contact.CompanyName,
            Department = contact.Department,
            JobTitle = contact.JobTitle,
            PrimaryEmailAddress = ToEmailAddressResource(contact.PrimaryEmailAddress),
            SecondaryEmailAddress = ToEmailAddressResource(contact.SecondaryEmailAddress),
            TertiaryEmailAddress = ToEmailAddressResource(contact.TertiaryEmailAddress),
            EmailAddresses = null,
            BusinessPhones = contact.BusinessPhones
                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                .Select(phone => phone.Trim())
                .ToList(),
            MobilePhone = contact.MobilePhone,
            Categories = contact.Categories
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category.Trim())
                .ToList(),
            PersonalNotes = contact.PersonalNotes,
            SingleValueExtendedProperties = new List<SingleValueExtendedPropertyResource>
            {
                new()
                {
                    Id = MicrosoftContactMapper.SourceIdExtendedPropertyId,
                    Value = contact.SourceId ?? string.Empty
                }
            }
        };
    }

    private static string? GetSourceId(ContactResource contact)
    {
        var sourceId = contact.SingleValueExtendedProperties?
            .FirstOrDefault(property => string.Equals(
                property.Id,
                MicrosoftContactMapper.SourceIdExtendedPropertyId,
                StringComparison.Ordinal))
            ?.Value;

        return string.IsNullOrWhiteSpace(sourceId) ? null : sourceId;
    }

    private static IEnumerable<MicrosoftGraphEmailAddress> ToEmailAddresses(IEnumerable<EmailAddressResource>? emails)
    {
        foreach (var email in emails ?? Enumerable.Empty<EmailAddressResource>())
        {
            var mapped = ToMicrosoftGraphEmailAddress(email);
            if (mapped is not null)
            {
                yield return mapped;
            }
        }
    }

    private static MicrosoftGraphEmailAddress? ToMicrosoftGraphEmailAddress(EmailAddressResource? email)
    {
        return string.IsNullOrWhiteSpace(email?.Address)
            ? null
            : new MicrosoftGraphEmailAddress(
                email.Address.Trim(),
                email.Name,
                string.IsNullOrWhiteSpace(email.Type) ? null : email.Type.Trim());
    }

    private static EmailAddressResource? ToEmailAddressResource(MicrosoftGraphEmailAddress? email)
    {
        return string.IsNullOrWhiteSpace(email?.Address)
            ? null
            : new EmailAddressResource
            {
                Address = email.Address.Trim(),
                Name = email.Name
            };
    }

    private sealed class GraphCollectionResponse<T>
    {
        public List<T>? Value { get; init; }

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; init; }
    }

    private sealed class ContactResource
    {
        public string? Id { get; init; }
        public string? ChangeKey { get; init; }
        public string? DisplayName { get; init; }
        public string? GivenName { get; init; }
        public string? Surname { get; init; }
        public string? CompanyName { get; init; }
        public string? Department { get; init; }
        public string? JobTitle { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public EmailAddressResource? PrimaryEmailAddress { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public EmailAddressResource? SecondaryEmailAddress { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public EmailAddressResource? TertiaryEmailAddress { get; init; }

        public List<EmailAddressResource>? EmailAddresses { get; init; }
        public List<string>? BusinessPhones { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? MobilePhone { get; init; }

        public List<string>? Categories { get; init; }
        public string? PersonalNotes { get; init; }
        public List<SingleValueExtendedPropertyResource>? SingleValueExtendedProperties { get; init; }
    }

    private sealed class EmailAddressResource
    {
        public string? Name { get; init; }

        public string? Address { get; init; }

        public string? Type { get; init; }
    }

    private sealed class BetaContactEmailAddressResource
    {
        public List<EmailAddressResource> EmailAddresses { get; init; } = new();
    }

    private sealed class SingleValueExtendedPropertyResource
    {
        public string? Id { get; init; }
        public string? Value { get; init; }
    }
}
