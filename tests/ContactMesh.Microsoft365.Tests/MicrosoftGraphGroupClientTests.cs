// File: MicrosoftGraphGroupClientTests.cs
// Author: Zunair
// Producer: Copilot

using System.Net;
using System.Text;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Microsoft365.Groups;
using Xunit;

namespace ContactMesh.Microsoft365.Tests
{
    public sealed class MicrosoftGraphGroupClientTests
    {
        [Fact]
        public async Task ListGroupsAsync_Pages_Groups_And_Sends_Bearer_Token()
        {
            var handler = new RecordingHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(
                        """
                        {
                          "value": [
                            {
                              "id": "group-1",
                              "mail": "team@example.org",
                              "displayName": "Team",
                              "visibility": "Public",
                              "mailEnabled": true,
                              "securityEnabled": false,
                              "groupTypes": [ "Unified" ]
                            }
                          ],
                          "@odata.nextLink": "https://graph.test/v1.0/groups?$skiptoken=next-page"
                        }
                        """)
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{ "value": [] }""")
                });
            var client = CreateClient(handler);

            var groups = await client.ListGroupsAsync(CancellationToken.None);

            var group = Assert.Single(groups);
            Assert.Equal("group-1", group.Id);
            Assert.Equal("team@example.org", group.Mail);
            Assert.Equal("Team", group.DisplayName);
            Assert.Equal("Public", group.Visibility);
            Assert.True(group.MailEnabled);
            Assert.False(group.SecurityEnabled);
            Assert.Equal("Unified", Assert.Single(group.GroupTypes));
            Assert.Equal("Bearer graph-token", handler.Requests[0].Authorization);
            Assert.Equal("Bearer graph-token", handler.Requests[1].Authorization);
            Assert.Equal(
                "https://graph.test/v1.0/groups?%24select=id%2Cmail%2CdisplayName%2Cvisibility%2CmailEnabled%2CsecurityEnabled%2CgroupTypes&%24top=999",
                handler.Requests[0].RequestUri?.ToString());
            Assert.Equal(
                "https://graph.test/v1.0/groups?$skiptoken=next-page",
                handler.Requests[1].RequestUri?.ToString());
        }

        [Fact]
        public async Task ListGroupMembersAsync_Pages_Transitive_Members()
        {
            var handler = new RecordingHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(
                        """
                        {
                          "value": [
                            {
                              "@odata.type": "#microsoft.graph.user",
                              "id": "user-1",
                              "mail": "jane@example.org",
                              "userPrincipalName": "jane@tenant.example",
                              "proxyAddresses": [ "SMTP:jane.primary@example.org", "smtp:jane@example.org" ],
                              "displayName": "Jane Doe"
                            },
                            {
                              "@odata.type": "#microsoft.graph.orgContact",
                              "id": "contact-1",
                              "mail": "external@example.org",
                              "displayName": "External Person",
                              "givenName": "External",
                              "surname": "Person",
                              "companyName": "Example",
                              "department": "Partners",
                              "jobTitle": "Advisor",
                              "businessPhones": [ "+1 215 555 0100" ],
                              "mobilePhone": "+1 215 555 0101"
                            }
                          ],
                          "@odata.nextLink": "https://graph.test/v1.0/groups/group-1/transitiveMembers?$skiptoken=next-page"
                        }
                        """)
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{ "value": [] }""")
                });
            var client = CreateClient(handler);

            var members = await client.ListGroupMembersAsync("group@example.org", CancellationToken.None);

            Assert.Equal(2, members.Count);
            Assert.Equal("#microsoft.graph.user", members[0].ODataType);
            Assert.Equal("jane@example.org", members[0].Mail);
            Assert.Equal(new[] { "SMTP:jane.primary@example.org", "smtp:jane@example.org" }, members[0].ProxyAddresses);
            Assert.Equal("#microsoft.graph.orgContact", members[1].ODataType);
            Assert.Equal("External", members[1].GivenName);
            Assert.Equal("+1 215 555 0100", Assert.Single(members[1].BusinessPhones));
            Assert.Equal("Bearer graph-token", handler.Requests[0].Authorization);
            Assert.Equal("Bearer graph-token", handler.Requests[1].Authorization);
            Assert.StartsWith(
                "https://graph.test/v1.0/groups/group%40example.org/transitiveMembers?",
                handler.Requests[0].RequestUri?.ToString(),
                StringComparison.Ordinal);
            Assert.Contains("%24select=", handler.Requests[0].RequestUri?.ToString(), StringComparison.Ordinal);
            Assert.Contains("proxyAddresses", Uri.UnescapeDataString(handler.Requests[0].RequestUri?.ToString() ?? string.Empty), StringComparison.Ordinal);
            Assert.Contains("%24top=999", handler.Requests[0].RequestUri?.ToString(), StringComparison.Ordinal);
            Assert.Equal(
                "https://graph.test/v1.0/groups/group-1/transitiveMembers?$skiptoken=next-page",
                handler.Requests[1].RequestUri?.ToString());
        }

        [Fact]
        public async Task ListGroupsAsync_Rejects_Empty_Access_Token()
        {
            var client = new MicrosoftGraphGroupClient(
                new HttpClient(new RecordingHandler()),
                new FakeGraphAccessTokenProvider(" "),
                new Uri("https://graph.test/"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.ListGroupsAsync(CancellationToken.None));

            Assert.Equal("Microsoft Graph access token provider returned an empty token.", ex.Message);
        }

        private static MicrosoftGraphGroupClient CreateClient(RecordingHandler handler)
        {
            return new MicrosoftGraphGroupClient(
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

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                this.Requests.Add(new RecordedRequest(
                    request.RequestUri,
                    request.Headers.Authorization?.ToString()));

                return Task.FromResult(this.responses.Dequeue());
            }
        }

        private sealed record RecordedRequest(Uri? RequestUri, string? Authorization);
    }
}
