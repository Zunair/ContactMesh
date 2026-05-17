using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class GroupContactFactoryTests
{
    [Fact]
    public void CreateGroupContact_Maps_Group_To_Managed_Work_Contact()
    {
        var group = new MeshGroup
        {
            Id = "group-1",
            Email = "support@example.org",
            DisplayName = "Support Team"
        };

        var contact = new GroupContactFactory().CreateGroupContact(group, new[] { "Directory", "Support" });

        Assert.Equal("group:group-1", contact.SourceId);
        Assert.Equal("+Support-Team", contact.DisplayName);
        Assert.Equal("+Support-Team", contact.GivenName);
        Assert.Equal("Group", contact.FamilyName);
        Assert.Equal("support@example.org", Assert.Single(contact.Emails).Address);
        Assert.Contains("Directory", contact.Labels);
        Assert.Contains("Support", contact.Labels);
        Assert.Equal("group-1", contact.Metadata["groupId"]);
        Assert.Equal("support@example.org", contact.Metadata["groupEmail"]);
        Assert.Equal("group", contact.Metadata["sourceType"]);
    }

    [Fact]
    public void CreateGroupContact_Falls_Back_To_Email_For_DisplayName()
    {
        var group = new MeshGroup
        {
            Id = "group-1",
            Email = "support@example.org"
        };

        var contact = new GroupContactFactory().CreateGroupContact(group);

        Assert.Equal("+support@example.org", contact.DisplayName);
        Assert.Equal("+support@example.org", contact.GivenName);
    }

    [Fact]
    public void CreateGroupContact_Uses_Configured_Prefix_For_DisplayName()
    {
        var group = new MeshGroup
        {
            Id = "group-1",
            Email = "support@example.org",
            DisplayName = "Support Team"
        };

        var contact = new GroupContactFactory().CreateGroupContact(group, prefix: "#");

        Assert.Equal("#Support-Team", contact.DisplayName);
        Assert.Equal("#Support-Team", contact.GivenName);
    }

    [Fact]
    public void CreateGroupContact_Does_Not_Double_Apply_Prefix()
    {
        var group = new MeshGroup
        {
            Id = "group-1",
            Email = "support@example.org",
            DisplayName = "+Support-Team"
        };

        var contact = new GroupContactFactory().CreateGroupContact(group);

        Assert.Equal("+Support-Team", contact.DisplayName);
    }

    [Fact]
    public void CreateGroupContact_Hyphenates_Whitespace_In_DisplayName()
    {
        var group = new MeshGroup
        {
            Id = "group-1",
            Email = "833c@example.org",
            DisplayName = "833 Chestnut Street (833C)"
        };

        var contact = new GroupContactFactory().CreateGroupContact(group);

        Assert.Equal("+833-Chestnut-Street-(833C)", contact.DisplayName);
        Assert.Equal("Group", contact.FamilyName);
    }

    [Fact]
    public void CreateGroupContact_Deduplicates_Labels_Case_Insensitively()
    {
        var group = new MeshGroup
        {
            Id = "group-1",
            Email = "support@example.org"
        };

        var contact = new GroupContactFactory().CreateGroupContact(group, new[] { "Directory", "directory", "" });

        Assert.Single(contact.Labels);
        Assert.Contains("Directory", contact.Labels);
    }
}
