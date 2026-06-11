// File: MicrosoftClientCredentialsTokenSourceTests.cs
// Author: Zunair
// Producer: Copilot

using System.Net;
using System.Text;
using ContactMesh.Microsoft365.Auth;
using Xunit;

namespace ContactMesh.Microsoft365.Tests
{
    public sealed class MicrosoftClientCredentialsTokenSourceTests
    {
        [Fact]
        public async Task GetAccessTokenAsync_Posts_Client_Credentials_Form_To_Tenant_Token_Endpoint()
        {
            var handler = new RecordingHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{ "access_token": "graph-token" }""")
                });
            var tokenSource = new MicrosoftClientCredentialsTokenSource(
                new HttpClient(handler),
                new Uri("https://login.test/"));

            var token = await tokenSource.GetAccessTokenAsync(
                new MicrosoftGraphTokenRequest(
                    "tenant-id",
                    "client-id",
                    "client-secret",
                    new[] { "scope.a", "scope.b" }),
                CancellationToken.None);

            Assert.Equal("graph-token", token);
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://login.test/tenant-id/oauth2/v2.0/token", request.RequestUri?.ToString());
            Assert.Contains("client_id=client-id", request.Body);
            Assert.Contains("client_secret=client-secret", request.Body);
            Assert.Contains("scope=scope.a+scope.b", request.Body);
            Assert.Contains("grant_type=client_credentials", request.Body);
        }

        [Fact]
        public async Task GetAccessTokenAsync_Returns_Empty_String_When_Response_Has_No_Access_Token()
        {
            var handler = new RecordingHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{}""")
                });
            var tokenSource = new MicrosoftClientCredentialsTokenSource(
                new HttpClient(handler),
                new Uri("https://login.test/"));

            var token = await tokenSource.GetAccessTokenAsync(
                new MicrosoftGraphTokenRequest("tenant-id", "client-id", "client-secret", Microsoft365Options.DefaultGraphScopes),
                CancellationToken.None);

            Assert.Equal(string.Empty, token);
        }

        private static StringContent JsonContent(string json)
        {
            return new StringContent(json, Encoding.UTF8, "application/json");
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
}
