using System.Net;
using System.Text;
using System.Text.Json;
using ContactMesh.Google.Auth;
using ContactMesh.Google.Contacts;
using Xunit;

namespace ContactMesh.Google.Tests;

public sealed class GooglePeopleContactClientTests
{
    [Fact]
    public async Task ListAsync_Pages_Connections_And_Maps_Managed_Source_Id()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "connections": [
                        {
                          "resourceName": "people/c123",
                          "etag": "etag-1",
                          "names": [ { "displayName": "Jane Doe", "givenName": "Jane", "familyName": "Doe" } ],
                          "emailAddresses": [ { "value": "jane@example.org", "type": "work", "metadata": { "primary": true } } ],
                          "phoneNumbers": [ { "value": "+12155550100", "type": "work", "metadata": { "primary": true } } ],
                          "organizations": [ { "name": "Example", "department": "Engineering", "title": "Director" } ],
                          "memberships": [ { "contactGroupMembership": { "contactGroupResourceName": "contactGroups/directory" } } ],
                          "clientData": [ { "key": "contactmesh.sourceId", "value": "directory-user-1" } ]
                        }
                      ],
                      "nextPageToken": "next-page"
                    }
                    """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "connections": [] }""")
            });
        var client = CreateClient(handler);

        var contacts = await client.ListAsync("user@example.org", CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.Equal("people/c123", contact.ResourceName);
        Assert.Equal("etag-1", contact.ETag);
        Assert.Equal("directory-user-1", contact.SourceId);
        Assert.Equal("Jane", contact.GivenName);
        Assert.Equal("jane@example.org", Assert.Single(contact.Emails).Address);
        Assert.Equal("contactGroups/directory", Assert.Single(contact.ContactGroupResourceNames));
        Assert.Equal(
            "https://people.test/v1/people/me/connections?personFields=names%2CemailAddresses%2CphoneNumbers%2Corganizations%2Cmemberships%2Cmetadata%2CclientData&pageSize=1000",
            handler.Requests[0].RequestUri?.ToString());
        Assert.Equal(
            "https://people.test/v1/people/me/connections?personFields=names%2CemailAddresses%2CphoneNumbers%2Corganizations%2Cmemberships%2Cmetadata%2CclientData&pageSize=1000&pageToken=next-page",
            handler.Requests[1].RequestUri?.ToString());
    }

    [Fact]
    public async Task CreateUpdateDeleteAsync_Uses_People_Contact_Endpoints()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.CreateAsync(
            "user@example.org",
            new GooglePersonContact
            {
                SourceId = "directory-user-1",
                DisplayName = "Jane Doe",
                Emails = new[] { new GooglePersonEmail("jane@example.org", "work", true) }
            },
            CancellationToken.None);
        await client.UpdateAsync(
            "user@example.org",
            new GooglePersonContact
            {
                ResourceName = "people/c123",
                ETag = "etag-1",
                SourceId = "directory-user-1",
                DisplayName = "Jane Doe"
            },
            CancellationToken.None);
        await client.DeleteAsync("user@example.org", "people/c123", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.StartsWith("https://people.test/v1/people:createContact?", handler.Requests[0].RequestUri?.ToString(), StringComparison.Ordinal);
        using (var document = JsonDocument.Parse(handler.Requests[0].Body))
        {
            Assert.Equal("Jane Doe", document.RootElement.GetProperty("names")[0].GetProperty("displayName").GetString());
            Assert.Equal("directory-user-1", document.RootElement.GetProperty("clientData")[0].GetProperty("value").GetString());
        }

        Assert.Equal(HttpMethod.Patch, handler.Requests[1].Method);
        Assert.StartsWith("https://people.test/v1/people/c123:updateContact?", handler.Requests[1].RequestUri?.ToString(), StringComparison.Ordinal);
        using (var document = JsonDocument.Parse(handler.Requests[1].Body))
        {
            Assert.Equal("people/c123", document.RootElement.GetProperty("resourceName").GetString());
            Assert.Equal("etag-1", document.RootElement.GetProperty("etag").GetString());
        }

        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
        Assert.Equal("https://people.test/v1/people/c123:deleteContact", handler.Requests[2].RequestUri?.ToString());
    }

    private static GooglePeopleContactClient CreateClient(RecordingHandler handler)
    {
        return new GooglePeopleContactClient(
            new HttpClient(handler),
            new FakeAccessTokenProvider(),
            new Uri("https://people.test/"));
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed class FakeAccessTokenProvider : IGoogleAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult($"token-for-{userId}");
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
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));

            return this.responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri? RequestUri, string Body);
}
