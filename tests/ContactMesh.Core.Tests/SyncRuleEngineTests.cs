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
}
