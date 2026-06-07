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
            User("1", "active@example.org", "/Staff"),
            User("2", "blocked@example.org", "/Staff"),
            User("3", "suspended@example.org", "/Staff", isSuspended: true),
            User("4", "external@example.org", "/Staff", isExternal: true)
        };

        var engine = new SyncRuleEngine(new ExclusionRule(new[] { "blocked@example.org" }));

        var targets = engine.CreateTargets(users);

        var target = Assert.Single(targets);
        Assert.Equal("1", target.UserId);
        Assert.Equal("active@example.org", target.UserEmail);
    }

    [Fact]
    public void CreateSourceEligibleUsers_Includes_Suspended_But_Excludes_External_Users()
    {
        var users = new[]
        {
            User("1", "active@example.org", "/Staff"),
            User("2", "suspended@example.org", "/Staff", isSuspended: true),
            User("3", "external@example.org", "/Staff", isExternal: true)
        };

        var engine = new SyncRuleEngine();

        var sourceUsers = engine.CreateSourceEligibleUsers(users);

        Assert.Equal(new[] { "1", "2" }, sourceUsers.Select(user => user.Id));
    }

    [Fact]
    public void CreateTargets_Uses_OrganizationUnit_Eligibility()
    {
        var users = new[]
        {
            User("1", "included@example.org", "/Staff"),
            User("2", "excluded@example.org", "/Service Accounts"),
            User("3", "outside@example.org", "/Archive")
        };

        var engine = new SyncRuleEngine(
            organizationUnitRule: new OrganizationUnitRule(
                includedOrganizationUnits: new[] { "/Staff", "/Service Accounts" },
                excludedOrganizationUnits: new[] { "/Service Accounts=Ignore" }));

        var targets = engine.CreateTargets(users);

        var target = Assert.Single(targets);
        Assert.Equal("included@example.org", target.UserEmail);
    }

    [Fact]
    public void CreateTargets_Uses_TargetUserScope_By_Id_Or_Email()
    {
        var users = new[]
        {
            User("1", "first@example.org", "/Staff"),
            User("2", "second@example.org", "/Staff"),
            User("3", "third@example.org", "/Staff")
        };

        var engine = new SyncRuleEngine(targetUsers: new[] { "1", "second@example.org", " " });

        var targets = engine.CreateTargets(users);

        Assert.Equal(new[] { "1", "2" }, targets.Select(target => target.UserId));
    }

    [Fact]
    public void CreateTargets_Uses_TargetUserScope_By_Alternate_Email()
    {
        var users = new[]
        {
            new MeshUser
            {
                Id = "1",
                Email = "primary@example.org",
                AlternateEmails = new[] { "alias@example.org" }
            },
            User("2", "other@example.org", "/Staff")
        };

        var engine = new SyncRuleEngine(targetUsers: new[] { "alias@example.org" });

        var target = Assert.Single(engine.CreateTargets(users));

        Assert.Equal("1", target.UserId);
        Assert.Equal("primary@example.org", target.UserEmail);
        Assert.Contains("alias@example.org", target.AlternateEmails);
    }

    [Fact]
    public void CreateTargets_Excludes_By_Alternate_Email()
    {
        var users = new[]
        {
            new MeshUser
            {
                Id = "1",
                Email = "primary@example.org",
                AlternateEmails = new[] { "blocked-alias@example.org" }
            },
            User("2", "active@example.org", "/Staff")
        };

        var engine = new SyncRuleEngine(new ExclusionRule(new[] { "blocked-alias@example.org" }));

        var target = Assert.Single(engine.CreateTargets(users));

        Assert.Equal("2", target.UserId);
    }

    [Fact]
    public void CreateEligibleUsers_Does_Not_Apply_TargetUserScope()
    {
        var users = new[]
        {
            User("1", "first@example.org", "/Staff"),
            User("2", "second@example.org", "/Staff")
        };

        var engine = new SyncRuleEngine(targetUsers: new[] { "1" });

        var eligibleUsers = engine.CreateEligibleUsers(users);

        Assert.Equal(new[] { "1", "2" }, eligibleUsers.Select(user => user.Id));
    }

    [Fact]
    public void OrganizationUnitRule_Includes_Users_When_No_Inclusion_List_Is_Configured()
    {
        var evaluation = new OrganizationUnitRule().Evaluate(User("1", "person@example.org", "/Any"));

        Assert.True(evaluation.IsIncluded);
    }

    [Fact]
    public void OrganizationUnitRule_Matches_Any_Included_OrganizationUnit_Prefix()
    {
        var rule = new OrganizationUnitRule(includedOrganizationUnits: new[] { "/Staff", "/Volunteers" });

        var evaluation = rule.Evaluate(User("1", "person@example.org", "/Volunteers/Board"));

        Assert.True(evaluation.IsIncluded);
    }

    [Fact]
    public void OrganizationUnitRule_Excludes_OrganizationUnit_Prefixes()
    {
        var rule = new OrganizationUnitRule(excludedOrganizationUnits: new[] { "/Transitioning=Error" });

        var evaluation = rule.Evaluate(User("1", "person@example.org", "/Transitioning/Temp"));

        Assert.False(evaluation.IsIncluded);
        Assert.False(evaluation.IsIgnored);
    }

    [Fact]
    public void OrganizationUnitRule_Marks_Ignore_Exclusions()
    {
        var rule = new OrganizationUnitRule(excludedOrganizationUnits: new[] { "/Service Accounts=Ignore" });

        var evaluation = rule.Evaluate(User("1", "person@example.org", "/Service Accounts/App"));

        Assert.False(evaluation.IsIncluded);
        Assert.True(evaluation.IsIgnored);
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
    public void FilterGroupsForTarget_Uses_Target_And_Member_Alternate_Emails()
    {
        var group = new MeshGroup
        {
            Id = "members@example.org",
            Email = "members@example.org",
            GroupVisibility = MeshGroupVisibility.Members,
            MemberVisibility = MeshGroupVisibility.Members,
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "user-1",
                    Email = "old@example.org",
                    AlternateEmails = new[] { "primary@example.org" },
                    Type = MeshGroupMemberType.User
                }
            }
        };
        var target = new SyncTarget
        {
            UserId = "user-1",
            UserEmail = "primary@example.org",
            AlternateEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "old@example.org" }
        };

        var decision = Assert.Single(new SyncRuleEngine().FilterGroupsForTarget(new[] { group }, target));

        Assert.True(decision.CanSeeMembers);
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

    private static MeshUser User(
        string id,
        string email,
        string organizationUnit = "/",
        bool isSuspended = false,
        bool isExternal = false)
    {
        return new MeshUser
        {
            Id = id,
            Email = email,
            OrganizationUnit = organizationUnit,
            IsSuspended = isSuspended,
            IsExternal = isExternal
        };
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
