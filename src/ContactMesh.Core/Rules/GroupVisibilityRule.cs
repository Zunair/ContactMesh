using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules;

public sealed class GroupVisibilityRule
{
    public bool IsVisibleToTarget(MeshContact contact, SyncTarget target)
    {
        return contact.Labels.Count == 0 || contact.Labels.Overlaps(target.LabelNames);
    }

    public bool IsGroupVisibleToTarget(MeshGroup group, SyncTarget target)
    {
        return group.GroupVisibility switch
        {
            MeshGroupVisibility.Domain => true,
            MeshGroupVisibility.Members => HasMember(group, target),
            _ => false
        };
    }

    public bool CanTargetSeeGroupMembers(MeshGroup group, SyncTarget target)
    {
        return group.MemberVisibility switch
        {
            MeshGroupVisibility.Domain => true,
            MeshGroupVisibility.Members => HasMember(group, target),
            _ => false
        };
    }

    public IReadOnlyList<GroupVisibilityDecision> FilterVisibleGroups(IEnumerable<MeshGroup> groups, SyncTarget target)
    {
        return groups
            .Where(group => IsGroupVisibleToTarget(group, target))
            .Select(group => new GroupVisibilityDecision(group, CanTargetSeeGroupMembers(group, target)))
            .ToList();
    }

    private static bool HasMember(MeshGroup group, SyncTarget target)
    {
        return group.Members.Any(member =>
            string.Equals(member.Id, target.UserId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.Email, target.UserEmail, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record GroupVisibilityDecision(MeshGroup Group, bool CanSeeMembers);
