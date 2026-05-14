using ContactMesh.Core.Models;
using ContactMesh.Core.Rules;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class SyncRuleEngineTests
{
    [Fact]
    public void CreateTargets_Excludes_Suspended_And_Excluded_Users()
    {
        var users = new[]
        {
            User("1", "active@example.org"),
            User("2", "blocked@example.org"),
            User("3", "suspended@example.org", isSuspended: true)
        };

        var engine = new SyncRuleEngine(new ExclusionRule(new[] { "blocked@example.org" }));

        var targets = engine.CreateTargets(users);

        var target = Assert.Single(targets);
        Assert.Equal("1", target.UserId);
        Assert.Equal("active@example.org", target.UserEmail);
    }

    [Fact]
    public void FilterContactsForTarget_Includes_Unlabeled_And_Matching_Label_Contacts()
    {
        var unlabeled = new MeshContact { DisplayName = "Global" };
        var visible = Contact("Sales", "Visible");
        var hidden = Contact("Engineering", "Hidden");
        var target = new SyncTarget
        {
            UserId = "1",
            UserEmail = "person@example.org",
            LabelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sales" }
        };

        var contacts = new SyncRuleEngine().FilterContactsForTarget(new[] { unlabeled, visible, hidden }, target);

        Assert.Contains(unlabeled, contacts);
        Assert.Contains(visible, contacts);
        Assert.DoesNotContain(hidden, contacts);
    }

    [Fact]
    public void FilterGroupsForTarget_Includes_Domain_Visible_Groups_For_All_Targets()
    {
        var group = Group("all@example.org", MeshGroupVisibility.Domain, MeshGroupVisibility.Domain);
        var target = Target("user-1", "person@example.org");

        var decisions = new SyncRuleEngine().FilterGroupsForTarget(new[] { group }, target);

        var decision = Assert.Single(decisions);
        Assert.Equal(group, decision.Group);
        Assert.True(decision.CanSeeMembers);
    }

    [Fact]
    public void FilterGroupsForTarget_Includes_Member_Visible_Groups_Only_For_Members()
    {
        var group = Group("members@example.org", MeshGroupVisibility.Members, MeshGroupVisibility.Members, "person@example.org");
        var memberTarget = Target("user-1", "person@example.org");
        var nonMemberTarget = Target("user-2", "other@example.org");

        var memberDecisions = new SyncRuleEngine().FilterGroupsForTarget(new[] { group }, memberTarget);
        var nonMemberDecisions = new SyncRuleEngine().FilterGroupsForTarget(new[] { group }, nonMemberTarget);

        var memberDecision = Assert.Single(memberDecisions);
        Assert.True(memberDecision.CanSeeMembers);
        Assert.Empty(nonMemberDecisions);
    }

    [Fact]
    public void FilterGroupsForTarget_Can_Show_Group_Without_Showing_Members()
    {
        var group = Group("announcements@example.org", MeshGroupVisibility.Domain, MeshGroupVisibility.Members, "member@example.org");
        var target = Target("user-1", "person@example.org");

        var decisions = new SyncRuleEngine().FilterGroupsForTarget(new[] { group }, target);

        var decision = Assert.Single(decisions);
        Assert.Equal(group, decision.Group);
        Assert.False(decision.CanSeeMembers);
    }

    [Fact]
    public void FilterGroupsForTarget_Hides_Hidden_Groups()
    {
        var group = Group("private@example.org", MeshGroupVisibility.Hidden, MeshGroupVisibility.Hidden, "person@example.org");
        var target = Target("user-1", "person@example.org");

        var decisions = new SyncRuleEngine().FilterGroupsForTarget(new[] { group }, target);

        Assert.Empty(decisions);
    }

    private static MeshUser User(string id, string email, bool isSuspended = false)
    {
        return new MeshUser { Id = id, Email = email, IsSuspended = isSuspended };
    }

    private static MeshContact Contact(string label, string name)
    {
        return new MeshContact
        {
            DisplayName = name,
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { label }
        };
    }

    private static SyncTarget Target(string id, string email)
    {
        return new SyncTarget { UserId = id, UserEmail = email };
    }

    private static MeshGroup Group(
        string email,
        MeshGroupVisibility groupVisibility,
        MeshGroupVisibility memberVisibility,
        params string[] members)
    {
        return new MeshGroup
        {
            Id = email,
            Email = email,
            GroupVisibility = groupVisibility,
            MemberVisibility = memberVisibility,
            Members = members
                .Select(emailAddress => new MeshGroupMember
                {
                    Id = emailAddress,
                    Email = emailAddress,
                    Type = MeshGroupMemberType.User
                })
                .ToList()
        };
    }
}
