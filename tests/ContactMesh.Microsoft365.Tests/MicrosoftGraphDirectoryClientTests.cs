// File: MicrosoftGraphDirectoryClientTests.cs
// Author: Zunair
// Producer: Copilot

using System.Net;
using System.Text;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Microsoft365.Directory;
using Xunit;

namespace ContactMesh.Microsoft365.Tests
{
    public sealed class MicrosoftGraphDirectoryClientTests
    {
        [Fact]
        public async Task ListUsersAsync_Pages_Users_And_Sends_Bearer_Token()
        {
            var handler = new RecordingHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(
                        """
                        {
                          "value": [
                            {
                              "id": "graph-user-1",
                              "mail": "jane.alias@example.org",
                              "userPrincipalName": "jane@example.org",
                              "displayName": "Jane Doe",
                              "givenName": "Jane",
                              "surname": "Doe",
                              "companyName": "Example",
                              "department": "Engineering",
                              "jobTitle": "Director",
                              "proxyAddresses": [ "SMTP:jane.alias@example.org", "smtp:jane@example.org" ],
                              "businessPhones": [ "+1 215 555 0100" ],
                              "mobilePhone": "+1 215 555 0101",
                              "accountEnabled": true
                            }
                          ],
                          "@odata.nextLink": "https://graph.test/v1.0/users?$skiptoken=next-page"
                        }
                        """)
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{ "value": [] }""")
                });
            var client = new MicrosoftGraphDirectoryClient(
                new HttpClient(handler),
                new FakeGraphAccessTokenProvider("graph-token"),
                new Uri("https://graph.test/"));

            var users = await client.ListUsersAsync(CancellationToken.None);

            var user = Assert.Single(users);
            Assert.Equal("graph-user-1", user.Id);
            Assert.Equal("jane.alias@example.org", user.Mail);
            Assert.Equal(new[] { "SMTP:jane.alias@example.org", "smtp:jane@example.org" }, user.ProxyAddresses);
            Assert.Equal("Jane", user.GivenName);
            Assert.Equal("+1 215 555 0100", Assert.Single(user.BusinessPhones));
            Assert.True(user.AccountEnabled);
            Assert.Equal("Bearer graph-token", handler.Requests[0].Authorization);
            Assert.Equal("Bearer graph-token", handler.Requests[1].Authorization);
            Assert.Equal(
                "https://graph.test/v1.0/users?%24select=id%2Cmail%2CuserPrincipalName%2CdisplayName%2CgivenName%2Csurname%2CcompanyName%2Cdepartment%2CjobTitle%2CproxyAddresses%2CbusinessPhones%2CmobilePhone%2CaccountEnabled%2CuserType&%24top=999",
                handler.Requests[0].RequestUri?.ToString());
            Assert.Equal(
                "https://graph.test/v1.0/users?$skiptoken=next-page",
                handler.Requests[1].RequestUri?.ToString());
        }

        [Fact]
        public async Task ListUsersAsync_Rejects_Empty_Access_Token()
        {
            var client = new MicrosoftGraphDirectoryClient(
                new HttpClient(new RecordingHandler()),
                new FakeGraphAccessTokenProvider(" "),
                new Uri("https://graph.test/"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.ListUsersAsync(CancellationToken.None));

            Assert.Equal("Microsoft Graph access token provider returned an empty token.", ex.Message);
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
