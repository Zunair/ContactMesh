using ContactMesh.Microsoft365.Groups;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftGroupProviderTests
{
    [Fact]
    public async Task GetGroupsAsync_Returns_Empty_When_Client_Is_Not_Configured()
    {
        var provider = new MicrosoftGroupProvider();

        var groups = await provider.GetGroupsAsync(CancellationToken.None);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task GetGroupsAsync_Reads_Groups_And_Transitive_Members_From_Client()
    {
        var provider = new MicrosoftGroupProvider(new FakeGraphGroupClient(
            new[]
            {
                new MicrosoftGraphGroup
                {
                    Id = "group-1",
                    Mail = "team@example.org",
                    DisplayName = "Team",
                    Visibility = "Private"
                },
                new MicrosoftGraphGroup
                {
                    Id = "missing-mail"
                }
            },
            new Dictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>>(StringComparer.OrdinalIgnoreCase)
            {
                ["group-1"] = new[]
                {
                    new MicrosoftGraphGroupMember
                    {
                        Id = "user-1",
                        ODataType = "#microsoft.graph.user",
                        Mail = "jane@example.org"
                    }
                }
            }));

        var groups = await provider.GetGroupsAsync(CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.Equal("group-1", group.Id);
        Assert.Equal("team@example.org", group.Email);
        Assert.Equal("jane@example.org", Assert.Single(group.Members).Email);
    }

    [Fact]
    public async Task GetGroupContactsAsync_Returns_Only_OrgContact_Members_With_Email()
    {
        var provider = new MicrosoftGroupProvider(new FakeGraphGroupClient(
            Array.Empty<MicrosoftGraphGroup>(),
            new Dictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>>(StringComparer.OrdinalIgnoreCase)
            {
                ["group-1"] = new[]
                {
                    new MicrosoftGraphGroupMember
                    {
                        Id = "contact-1",
                        ODataType = "#microsoft.graph.orgContact",
                        Mail = "external@example.org",
                        DisplayName = "External Person"
                    },
                    new MicrosoftGraphGroupMember
                    {
                        Id = "user-1",
                        ODataType = "#microsoft.graph.user",
                        Mail = "jane@example.org"
                    },
                    new MicrosoftGraphGroupMember
                    {
                        Id = "missing-mail",
                        ODataType = "#microsoft.graph.orgContact"
                    }
                }
            }));

        var contacts = await provider.GetGroupContactsAsync("group-1", CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.Equal("orgContact:contact-1", contact.SourceId);
        Assert.Equal("External Person", contact.DisplayName);
        Assert.Equal("external@example.org", Assert.Single(contact.Emails).Address);
    }

    private sealed class FakeGraphGroupClient : IMicrosoftGraphGroupClient
    {
        private readonly IReadOnlyList<MicrosoftGraphGroup> groups;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>> membersByGroupId;

        public FakeGraphGroupClient(
            IReadOnlyList<MicrosoftGraphGroup> groups,
            IReadOnlyDictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>> membersByGroupId)
        {
            this.groups = groups;
            this.membersByGroupId = membersByGroupId;
        }

        public Task<IReadOnlyList<MicrosoftGraphGroup>> ListGroupsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(this.groups);
        }

        public Task<IReadOnlyList<MicrosoftGraphGroupMember>> ListGroupMembersAsync(
            string groupId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                this.membersByGroupId.TryGetValue(groupId, out var members)
                    ? members
                    : Array.Empty<MicrosoftGraphGroupMember>());
        }
    }
}
