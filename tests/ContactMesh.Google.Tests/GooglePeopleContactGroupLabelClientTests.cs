using System.Net;
using System.Text;
using System.Text.Json;
using ContactMesh.Google.Auth;
using ContactMesh.Google.Contacts;
using Xunit;

namespace ContactMesh.Google.Tests;

public sealed class GooglePeopleContactGroupLabelClientTests
{
    [Fact]
    public async Task ListAsync_Pages_Through_Contact_Groups_And_Maps_Client_Data()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "contactGroups": [
                        {
                          "resourceName": "contactGroups/directory",
                          "name": "Directory",
                          "clientData": [
                            { "key": "contactmesh.appId", "value": "contact-mesh" },
                            { "key": "contactmesh.labelName", "value": "Directory" }
                          ]
                        }
                      ],
                      "nextPageToken": "next-page"
                    }
                    """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "contactGroups": [
                        {
                          "resourceName": "contactGroups/sales",
                          "name": "Sales",
                          "clientData": [
                            { "key": "contactmesh.appId", "value": "contact-mesh" }
                          ]
                        }
                      ]
                    }
                    """)
            });
        var client = CreateClient(handler);

        var labels = await client.ListAsync("user@example.org", CancellationToken.None);

        Assert.Equal(2, labels.Count);
        Assert.Equal("contactGroups/directory", labels[0].ResourceName);
        Assert.Equal("Directory", labels[0].Name);
        Assert.Equal("Directory", labels[0].ClientData["contactmesh.labelName"]);
        Assert.Equal("contactGroups/sales", labels[1].ResourceName);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("Bearer", request.Authorization?.Scheme);
            Assert.Equal("token-for-user@example.org", request.Authorization?.Parameter);
        });
        Assert.Equal(
            "https://people.test/v1/contactGroups?groupFields=clientData%2Cname&pageSize=1000",
            handler.Requests[0].RequestUri?.ToString());
        Assert.Equal(
            "https://people.test/v1/contactGroups?groupFields=clientData%2Cname&pageSize=1000&pageToken=next-page",
            handler.Requests[1].RequestUri?.ToString());
    }

    [Fact]
    public async Task CreateAsync_Posts_Label_Name_And_Client_Data()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.CreateAsync(
            "user@example.org",
            " Directory ",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["contactmesh.labelName"] = "Directory",
                ["contactmesh.appId"] = "contact-mesh"
            },
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://people.test/v1/contactGroups?readGroupFields=clientData%2Cname",
            request.RequestUri?.ToString());

        using var document = JsonDocument.Parse(request.Body);
        var contactGroup = document.RootElement.GetProperty("contactGroup");
        Assert.Equal("Directory", contactGroup.GetProperty("name").GetString());
        Assert.Equal(
            new[] { "contactmesh.appId", "contactmesh.labelName" },
            contactGroup
                .GetProperty("clientData")
                .EnumerateArray()
                .Select(item => item.GetProperty("key").GetString())
                .ToArray());
    }

    [Fact]
    public async Task UpdateAsync_Patches_Resource_Name_With_Update_Mask()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.UpdateAsync(
            "user@example.org",
            "contactGroups/directory",
            "Directory",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["contactmesh.appId"] = "contact-mesh"
            },
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal(
            "https://people.test/v1/contactGroups/directory?readGroupFields=clientData%2Cname&updateGroupFields=clientData%2Cname",
            request.RequestUri?.ToString());

        using var document = JsonDocument.Parse(request.Body);
        Assert.Equal(
            "contactGroups/directory",
            document.RootElement.GetProperty("contactGroup").GetProperty("resourceName").GetString());
    }

    [Fact]
    public async Task DeleteAsync_Deletes_Label_Without_Deleting_Contacts()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.DeleteAsync("user@example.org", "contactGroups/stale", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal(
            "https://people.test/v1/contactGroups/stale?deleteContacts=false",
            request.RequestUri?.ToString());
        Assert.Equal(string.Empty, request.Body);
    }

    private static GooglePeopleContactGroupLabelClient CreateClient(RecordingHandler handler)
    {
        return new GooglePeopleContactGroupLabelClient(
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
                request.Headers.Authorization,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));

            return this.responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        System.Net.Http.Headers.AuthenticationHeaderValue? Authorization,
        string Body);
}
