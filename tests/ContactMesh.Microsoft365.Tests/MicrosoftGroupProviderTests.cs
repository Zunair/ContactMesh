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

    [Fact]
    public async Task GetGroupsAsync_Returns_All_Types_When_GroupTypes_Not_Configured()
    {
        var allTypeGroups = new[]
        {
            new MicrosoftGraphGroup { Id = "m365", Mail = "m365@example.org", GroupTypes = new[] { "Unified" }, MailEnabled = true },
            new MicrosoftGraphGroup { Id = "mail-sec", Mail = "mailsec@example.org", MailEnabled = true, SecurityEnabled = true },
            new MicrosoftGraphGroup { Id = "dist", Mail = "dist@example.org", MailEnabled = true, SecurityEnabled = false }
        };

        var provider = new MicrosoftGroupProvider(new FakeGraphGroupClient(allTypeGroups, new Dictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>>(StringComparer.OrdinalIgnoreCase)));

        var groups = await provider.GetGroupsAsync(CancellationToken.None);

        Assert.Equal(3, groups.Count);
    }

    [Theory]
    [InlineData("Microsoft365", "m365")]
    [InlineData("MailEnabledSecurity", "mail-sec")]
    [InlineData("Distribution", "dist")]
    public async Task GetGroupsAsync_Filters_To_Configured_GroupType(string allowedType, string expectedGroupId)
    {
        var allTypeGroups = new[]
        {
            new MicrosoftGraphGroup { Id = "m365", Mail = "m365@example.org", GroupTypes = new[] { "Unified" }, MailEnabled = true },
            new MicrosoftGraphGroup { Id = "mail-sec", Mail = "mailsec@example.org", MailEnabled = true, SecurityEnabled = true },
            new MicrosoftGraphGroup { Id = "dist", Mail = "dist@example.org", MailEnabled = true, SecurityEnabled = false }
        };

        var provider = new MicrosoftGroupProvider(
            new FakeGraphGroupClient(allTypeGroups, new Dictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>>(StringComparer.OrdinalIgnoreCase)),
            new[] { allowedType });

        var groups = await provider.GetGroupsAsync(CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.Equal(expectedGroupId, group.Id);
    }

    [Fact]
    public async Task GetGroupsAsync_Filters_To_Multiple_Configured_GroupTypes()
    {
        var allTypeGroups = new[]
        {
            new MicrosoftGraphGroup { Id = "m365", Mail = "m365@example.org", GroupTypes = new[] { "Unified" }, MailEnabled = true },
            new MicrosoftGraphGroup { Id = "mail-sec", Mail = "mailsec@example.org", MailEnabled = true, SecurityEnabled = true },
            new MicrosoftGraphGroup { Id = "dist", Mail = "dist@example.org", MailEnabled = true, SecurityEnabled = false }
        };

        var provider = new MicrosoftGroupProvider(
            new FakeGraphGroupClient(allTypeGroups, new Dictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>>(StringComparer.OrdinalIgnoreCase)),
            new[] { "Microsoft365", "Distribution" });

        var groups = await provider.GetGroupsAsync(CancellationToken.None);

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Id == "m365");
        Assert.Contains(groups, g => g.Id == "dist");
    }

    [Fact]
    public async Task GetGroupsAsync_GroupType_Matching_Is_Case_Insensitive()
    {
        var allTypeGroups = new[]
        {
            new MicrosoftGraphGroup { Id = "m365", Mail = "m365@example.org", GroupTypes = new[] { "Unified" }, MailEnabled = true }
        };

        var provider = new MicrosoftGroupProvider(
            new FakeGraphGroupClient(allTypeGroups, new Dictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>>(StringComparer.OrdinalIgnoreCase)),
            new[] { "microsoft365" });

        var groups = await provider.GetGroupsAsync(CancellationToken.None);

        Assert.Single(groups);
    }

    [Fact]
    public async Task GetGroupsAsync_Security_GroupType_Matches_MailEnabledSecurity()
    {
        var allTypeGroups = new[]
        {
            new MicrosoftGraphGroup { Id = "mail-sec", Mail = "mailsec@example.org", MailEnabled = true, SecurityEnabled = true },
            new MicrosoftGraphGroup { Id = "dist", Mail = "dist@example.org", MailEnabled = true, SecurityEnabled = false }
        };

        var provider = new MicrosoftGroupProvider(
            new FakeGraphGroupClient(allTypeGroups, new Dictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>>(StringComparer.OrdinalIgnoreCase)),
            new[] { "Security" });

        var groups = await provider.GetGroupsAsync(CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.Equal("mail-sec", group.Id);
    }

    [Fact]
    public async Task GetGroupsAsync_Security_And_Distribution_GroupTypes_Match_Both()
    {
        var allTypeGroups = new[]
        {
            new MicrosoftGraphGroup { Id = "m365", Mail = "m365@example.org", GroupTypes = new[] { "Unified" }, MailEnabled = true },
            new MicrosoftGraphGroup { Id = "mail-sec", Mail = "mailsec@example.org", MailEnabled = true, SecurityEnabled = true },
            new MicrosoftGraphGroup { Id = "dist", Mail = "dist@example.org", MailEnabled = true, SecurityEnabled = false }
        };

        var provider = new MicrosoftGroupProvider(
            new FakeGraphGroupClient(allTypeGroups, new Dictionary<string, IReadOnlyList<MicrosoftGraphGroupMember>>(StringComparer.OrdinalIgnoreCase)),
            new[] { "Distribution", "Security" });

        var groups = await provider.GetGroupsAsync(CancellationToken.None);

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Id == "mail-sec");
        Assert.Contains(groups, g => g.Id == "dist");
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

