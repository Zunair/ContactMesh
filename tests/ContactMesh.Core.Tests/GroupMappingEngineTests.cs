using ContactMesh.Core.Models;
using ContactMesh.Core.Rules;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class GroupMappingEngineTests
{
    [Fact]
    public void ApplyMappings_Merges_Source_Members_Into_Target_Group()
    {
        var source = Group("source@example.org", Member("a@example.org"), Member("b@example.org"));
        var target = Group("target@example.org", Member("b@example.org"), Member("c@example.org"));

        var mappedGroups = new GroupMappingEngine().ApplyMappings(
            new[] { source, target },
            new[] { new GroupMapping("source@example.org", "target@example.org") });

        var mappedGroup = Assert.Single(mappedGroups);
        Assert.Equal("target@example.org", mappedGroup.Email);
        Assert.Equal(3, mappedGroup.Members.Count);
        Assert.Contains(mappedGroup.Members, member => member.Email == "a@example.org");
        Assert.Contains(mappedGroup.Members, member => member.Email == "b@example.org");
        Assert.Contains(mappedGroup.Members, member => member.Email == "c@example.org");
    }

    [Fact]
    public void ApplyMappings_Can_Match_Groups_By_Id()
    {
        var source = Group("source@example.org", Member("a@example.org")) with { Id = "source-id" };
        var target = Group("target@example.org") with { Id = "target-id" };

        var mappedGroups = new GroupMappingEngine().ApplyMappings(
            new[] { source, target },
            new[] { new GroupMapping("source-id", "target-id") });

        var mappedGroup = Assert.Single(mappedGroups);
        Assert.Equal("target@example.org", mappedGroup.Email);
        Assert.Equal("a@example.org", Assert.Single(mappedGroup.Members).Email);
    }

    [Fact]
    public void ApplyMappings_Leaves_Groups_Unchanged_When_Mapping_Cannot_Be_Resolved()
    {
        var group = Group("target@example.org", Member("a@example.org"));

        var mappedGroups = new GroupMappingEngine().ApplyMappings(
            new[] { group },
            new[] { new GroupMapping("missing@example.org", "target@example.org") });

        var mappedGroup = Assert.Single(mappedGroups);
        Assert.Equal(group, mappedGroup);
    }

    private static MeshGroup Group(string email, params MeshGroupMember[] members)
    {
        return new MeshGroup
        {
            Id = email,
            Email = email,
            DisplayName = email,
            Members = members
        };
    }

    private static MeshGroupMember Member(string email)
    {
        return new MeshGroupMember
        {
            Id = email,
            Email = email,
            Type = MeshGroupMemberType.User
        };
    }
}
