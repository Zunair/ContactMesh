// File: GoogleDelegatedAccessTokenProviderTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Google.Auth;
using Xunit;

namespace ContactMesh.Google.Tests
{
    public sealed class GoogleDelegatedAccessTokenProviderTests
    {
        [Fact]
        public async Task GetAccessTokenAsync_Uses_Target_User_As_Delegated_Subject()
        {
            var tokenSource = new FakeDelegatedAccessTokenSource("live-token");
            var provider = new GoogleDelegatedAccessTokenProvider(
                new GoogleWorkspaceOptions
                {
                    ServiceAccountFile = " service-account.json ",
                    Scopes = new[] { " scope.b ", "scope.a", "scope.a", " " }
                },
                tokenSource);

            var token = await provider.GetAccessTokenAsync(" user@example.org ", CancellationToken.None);

            Assert.Equal("live-token", token);
            var request = Assert.Single(tokenSource.Requests);
            Assert.Equal("service-account.json", request.ServiceAccountFile);
            Assert.Equal("user@example.org", request.SubjectUserEmail);
            Assert.Equal(new[] { "scope.b", "scope.a" }, request.Scopes);
        }

        [Fact]
        public async Task GetAccessTokenAsync_Defaults_To_People_Contacts_Scope()
        {
            var tokenSource = new FakeDelegatedAccessTokenSource("live-token");
            var provider = new GoogleDelegatedAccessTokenProvider(
                new GoogleWorkspaceOptions
                {
                    ServiceAccountFile = "service-account.json"
                },
                tokenSource);

            await provider.GetAccessTokenAsync("user@example.org", CancellationToken.None);

            Assert.Equal(
                GoogleWorkspaceOptions.DefaultPeopleApiScopes,
                Assert.Single(tokenSource.Requests).Scopes);
        }

        [Fact]
        public async Task GetAccessTokenAsync_Rejects_Empty_Service_Account_File()
        {
            var provider = new GoogleDelegatedAccessTokenProvider(
                new GoogleWorkspaceOptions
                {
                    ServiceAccountFile = " "
                },
                new FakeDelegatedAccessTokenSource("live-token"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetAccessTokenAsync("user@example.org", CancellationToken.None));

            Assert.Equal("GoogleWorkspace:ServiceAccountFile must be configured.", ex.Message);
        }

        [Fact]
        public async Task GetAccessTokenAsync_Rejects_Empty_Token_Source_Response()
        {
            var provider = new GoogleDelegatedAccessTokenProvider(
                new GoogleWorkspaceOptions
                {
                    ServiceAccountFile = "service-account.json"
                },
                new FakeDelegatedAccessTokenSource(" "));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetAccessTokenAsync("user@example.org", CancellationToken.None));

            Assert.Equal("Google delegated access token source returned an empty token.", ex.Message);
        }

        private sealed class FakeDelegatedAccessTokenSource : IGoogleDelegatedAccessTokenSource
        {
            private readonly string token;

            public FakeDelegatedAccessTokenSource(string token)
            {
                this.token = token;
            }

            public List<GoogleDelegatedCredentialRequest> Requests { get; } = new();

            public Task<string> GetAccessTokenAsync(
                GoogleDelegatedCredentialRequest request,
                CancellationToken cancellationToken)
            {
                this.Requests.Add(request);

                return Task.FromResult(this.token);
            }
        }
    }
}
