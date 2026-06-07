using System.Net;
using System.Text;
using System.Text.Json;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Microsoft365.Contacts;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftGraphContactClientTests
{
    [Fact]
    public async Task ListAsync_Pages_Contacts_And_Maps_Source_Id()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "value": [
                        {
                          "id": "contact-1",
                          "changeKey": "change-1",
                          "displayName": "Jane Doe",
                          "givenName": "Jane",
                          "surname": "Doe",
                          "companyName": "Example",
                          "department": "Engineering",
                          "jobTitle": "Director",
                          "primaryEmailAddress": { "name": "Jane Doe", "address": "jane.primary@example.org" },
                          "secondaryEmailAddress": { "name": "Jane Doe", "address": "jane.secondary@example.org" },
                          "emailAddresses": [ { "name": "Jane Doe", "address": "jane@example.org", "type": "personal" } ],
                          "businessPhones": [ "+1 215 555 0100" ],
                          "mobilePhone": "+1 215 555 0101",
                          "categories": [ "Directory" ],
                          "personalNotes": "managed",
                          "singleValueExtendedProperties": [
                            {
                              "id": "String {c7d37545-2710-4a53-a25d-dbbf5ca1d607} Name contactmesh.sourceId",
                              "value": "directory-user-1"
                            }
                          ]
                        }
                      ],
                      "@odata.nextLink": "https://graph.test/v1.0/users/user@example.org/contacts?$skiptoken=next-page"
                    }
                    """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "value": [] }""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "value": [] }""")
            });
        var client = CreateClient(handler);

        var contacts = await client.ListAsync("user@example.org", CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.Equal("contact-1", contact.Id);
        Assert.Equal("change-1", contact.ChangeKey);
        Assert.Equal("directory-user-1", contact.SourceId);
        Assert.Equal("Jane Doe", contact.DisplayName);
        Assert.Equal("jane.primary@example.org", contact.PrimaryEmailAddress?.Address);
        Assert.Equal("jane.secondary@example.org", contact.SecondaryEmailAddress?.Address);
        var genericEmail = Assert.Single(contact.EmailAddresses);
        Assert.Equal("jane@example.org", genericEmail.Address);
        Assert.Equal("personal", genericEmail.Type);
        Assert.Equal("+1 215 555 0100", Assert.Single(contact.BusinessPhones));
        Assert.Equal("Directory", Assert.Single(contact.Categories));
        Assert.Equal("Bearer graph-token", handler.Requests[0].Authorization);
        Assert.Equal("Bearer graph-token", handler.Requests[1].Authorization);
        Assert.StartsWith(
            "https://graph.test/v1.0/users/user%40example.org/contacts?",
            handler.Requests[0].RequestUri?.ToString(),
            StringComparison.Ordinal);
        Assert.Contains("%24select=", handler.Requests[0].RequestUri?.ToString(), StringComparison.Ordinal);
        Assert.Contains("%24expand=", handler.Requests[0].RequestUri?.ToString(), StringComparison.Ordinal);
        Assert.Contains("%24top=999", handler.Requests[0].RequestUri?.ToString(), StringComparison.Ordinal);
        Assert.Equal(
            "https://graph.test/v1.0/users/user@example.org/contacts?$skiptoken=next-page",
            handler.Requests[1].RequestUri?.ToString());
        Assert.StartsWith(
            "https://graph.test/v1.0/users/user%40example.org/contactFolders?",
            handler.Requests[2].RequestUri?.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_Reads_Contacts_From_Contact_Folders()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "value": [] }""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "value": [
                        { "id": "folder-1", "displayName": "-Directory" }
                      ]
                    }
                    """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "value": [] }""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "value": [
                        {
                          "id": "contact-2",
                          "displayName": "Chin Ha Yi",
                          "primaryEmailAddress": { "name": "Chin Ha Yi", "address": "cyi@example.org" }
                        }
                      ]
                    }
                    """)
            });
        var client = CreateClient(handler);

        var contacts = await client.ListAsync("user@example.org", CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.Equal("folder-1", contact.ContactFolderId);
        Assert.Equal("-Directory", contact.ContactFolderDisplayName);
        Assert.Equal("contact-2", contact.Id);
        Assert.Equal("Chin Ha Yi", contact.DisplayName);
        Assert.Equal("cyi@example.org", contact.PrimaryEmailAddress?.Address);
        var folderContactsUri = handler.Requests[3].RequestUri?.ToString();
        Assert.StartsWith(
            "https://graph.test/v1.0/users/user%40example.org/contactFolders/folder-1/contacts?",
            folderContactsUri,
            StringComparison.Ordinal);
        Assert.Contains("%24select=", folderContactsUri, StringComparison.Ordinal);
        Assert.Contains("%24expand=singleValueExtendedProperties", folderContactsUri, StringComparison.Ordinal);
        Assert.Contains("contactmesh.sourceId", folderContactsUri, StringComparison.Ordinal);
        Assert.Contains("%24top=999", folderContactsUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUpdateDeleteAsync_Uses_Graph_Contact_Endpoints()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created),
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.CreateAsync(
            "user@example.org",
            new MicrosoftGraphContact
            {
                SourceId = "directory-user-1",
                DisplayName = "Jane Doe",
                GivenName = "Jane",
                Surname = "Doe",
                CompanyName = "Example",
                PrimaryEmailAddress = new MicrosoftGraphEmailAddress("jane@example.org", "Jane Doe"),
                BusinessPhones = new[] { "+1 215 555 0100" },
                MobilePhone = "+1 215 555 0101",
                Categories = new[] { "Directory" },
                PersonalNotes = "managed"
            },
            CancellationToken.None);
        await client.UpdateAsync(
            "user@example.org",
            new MicrosoftGraphContact
            {
                Id = "contact-1",
                ChangeKey = "change-1",
                SourceId = "directory-user-1",
                DisplayName = "Jane Doe"
            },
            CancellationToken.None);
        await client.DeleteAsync("user@example.org", "contact-1", null, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://graph.test/v1.0/users/user%40example.org/contacts", handler.Requests[0].RequestUri?.ToString());
        using (var document = JsonDocument.Parse(handler.Requests[0].Body))
        {
            Assert.Equal("Jane Doe", document.RootElement.GetProperty("displayName").GetString());
            Assert.Equal("Example", document.RootElement.GetProperty("companyName").GetString());
            Assert.Equal("jane@example.org", document.RootElement.GetProperty("primaryEmailAddress").GetProperty("address").GetString());
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("secondaryEmailAddress").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("tertiaryEmailAddress").ValueKind);
            Assert.False(document.RootElement.TryGetProperty("emailAddresses", out _));
            Assert.Equal("Directory", document.RootElement.GetProperty("categories")[0].GetString());
            Assert.Equal(
                MicrosoftContactMapper.SourceIdExtendedPropertyId,
                document.RootElement.GetProperty("singleValueExtendedProperties")[0].GetProperty("id").GetString());
            Assert.Equal(
                "directory-user-1",
                document.RootElement.GetProperty("singleValueExtendedProperties")[0].GetProperty("value").GetString());
        }

        Assert.Equal(HttpMethod.Patch, handler.Requests[1].Method);
        Assert.Equal("https://graph.test/v1.0/users/user%40example.org/contacts/contact-1", handler.Requests[1].RequestUri?.ToString());
        using (var document = JsonDocument.Parse(handler.Requests[1].Body))
        {
            Assert.Equal("Jane Doe", document.RootElement.GetProperty("displayName").GetString());
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("companyName").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("department").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("jobTitle").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("primaryEmailAddress").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("secondaryEmailAddress").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("tertiaryEmailAddress").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("mobilePhone").ValueKind);
            Assert.False(document.RootElement.TryGetProperty("id", out _));
            Assert.False(document.RootElement.TryGetProperty("changeKey", out _));
        }

        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
        Assert.Equal("https://graph.test/v1.0/users/user%40example.org/contacts/contact-1", handler.Requests[2].RequestUri?.ToString());
    }

    [Fact]
    public async Task UpdateDeleteAsync_Uses_Contact_Folder_Endpoint_When_Folder_Id_Is_Present()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.UpdateAsync(
            "user@example.org",
            new MicrosoftGraphContact
            {
                Id = "contact-1",
                ContactFolderId = "folder-1",
                DisplayName = "Jane Doe"
            },
            CancellationToken.None);
        await client.DeleteAsync("user@example.org", "contact-1", "folder-1", CancellationToken.None);

        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
        Assert.Equal("https://graph.test/v1.0/users/user%40example.org/contactFolders/folder-1/contacts/contact-1", handler.Requests[0].RequestUri?.ToString());
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Equal("https://graph.test/v1.0/users/user%40example.org/contactFolders/folder-1/contacts/contact-1", handler.Requests[1].RequestUri?.ToString());
    }

    [Fact]
    public async Task ListAsync_Rejects_Empty_Access_Token()
    {
        var client = new MicrosoftGraphContactClient(
            new HttpClient(new RecordingHandler()),
            new FakeGraphAccessTokenProvider(" "),
            new Uri("https://graph.test/"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ListAsync("user@example.org", CancellationToken.None));

        Assert.Equal("Microsoft Graph access token provider returned an empty token.", ex.Message);
    }

    [Fact]
    public async Task ListAsync_Includes_Graph_Error_Details()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                ReasonPhrase = "Not Found",
                Content = JsonContent("""{ "error": { "code": "ErrorItemNotFound" } }""")
            });
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ListAsync("user@example.org", CancellationToken.None));

        Assert.Contains("404 (Not Found)", ex.Message, StringComparison.Ordinal);
        Assert.Contains("GET /v1.0/users/user%40example.org/contacts?", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ErrorItemNotFound", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_Returns_Empty_For_MailboxNotEnabledForRESTAPI()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                ReasonPhrase = "Not Found",
                Content = JsonContent("""{ "error": { "code": "MailboxNotEnabledForRESTAPI", "message": "The mailbox is either inactive, soft-deleted, or is hosted on-premise." } }""")
            });
        var client = CreateClient(handler);

        var contacts = await client.ListAsync("svc-account@example.org", CancellationToken.None);

        Assert.Empty(contacts);
    }

    [Fact]
    public async Task UpdateEmailAddressesBetaAsync_Writes_Typed_EmailAddresses_To_Beta_Endpoint()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.UpdateEmailAddressesBetaAsync(
            "user@example.org",
            "contact-1",
            new[]
            {
                new MicrosoftGraphEmailAddress("jane@example.org", "Jane Doe", "work")
            },
            CancellationToken.None);

        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
        Assert.Equal("https://graph.test/beta/users/user%40example.org/contacts/contact-1", handler.Requests[0].RequestUri?.ToString());
        using var document = JsonDocument.Parse(handler.Requests[0].Body);
        var email = Assert.Single(document.RootElement.GetProperty("emailAddresses").EnumerateArray());
        Assert.Equal("jane@example.org", email.GetProperty("address").GetString());
        Assert.Equal("Jane Doe", email.GetProperty("name").GetString());
        Assert.Equal("work", email.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetContactBetaAsync_Reads_Typed_EmailAddresses_From_Beta_Endpoint()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "id": "contact-1",
                      "displayName": "Jane Doe",
                      "emailAddresses": [
                        { "name": "Jane Doe", "address": "jane@example.org", "type": "work" }
                      ]
                    }
                    """)
            });
        var client = CreateClient(handler);

        var contact = await client.GetContactBetaAsync("user@example.org", "contact-1", CancellationToken.None);

        Assert.Equal("https://graph.test/beta/users/user%40example.org/contacts/contact-1?%24select=id%2CdisplayName%2CemailAddresses", handler.Requests[0].RequestUri?.ToString());
        var email = Assert.Single(contact.EmailAddresses);
        Assert.Equal("jane@example.org", email.Address);
        Assert.Equal("work", email.Type);
    }

    private static MicrosoftGraphContactClient CreateClient(RecordingHandler handler)
    {
        return new MicrosoftGraphContactClient(
            new HttpClient(handler),
            new FakeGraphAccessTokenProvider("graph-token"),
            new Uri("https://graph.test/"));
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed class FakeGraphAccessTokenProvider : IMicrosoftGraphAccessTokenProvider
    {
        private readonly string token;

        public FakeGraphAccessTokenProvider(string token)
        {
            this.token = token;
        }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(this.token);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.ToString(),
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));

            return this.responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri? RequestUri, string? Authorization, string Body);
}
