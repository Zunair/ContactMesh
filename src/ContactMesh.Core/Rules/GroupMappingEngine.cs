using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules;

public sealed class GroupMappingEngine
{
    public IReadOnlyList<MeshGroup> ApplyMappings(IEnumerable<MeshGroup> groups, IEnumerable<GroupMapping> mappings)
    {
        var mappedGroups = groups.ToList();

        foreach (var mapping in mappings)
        {
            var source = mappedGroups.FirstOrDefault(group => MatchesGroup(group, mapping.From));
            var targetIndex = mappedGroups.FindIndex(group => MatchesGroup(group, mapping.To));

            if (source is null || targetIndex < 0)
            {
                continue;
            }

            var target = mappedGroups[targetIndex];
            var mergedMembers = target.Members
                .Concat(source.Members)
                .GroupBy(MemberKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            mappedGroups[targetIndex] = target with { Members = mergedMembers };
            mappedGroups.RemoveAll(group => MatchesGroup(group, mapping.From));
        }

        return mappedGroups;
    }

    private static bool MatchesGroup(MeshGroup group, string idOrEmail)
    {
        return string.Equals(group.Id, idOrEmail, StringComparison.OrdinalIgnoreCase)
            || string.Equals(group.Email, idOrEmail, StringComparison.OrdinalIgnoreCase);
    }

    private static string MemberKey(MeshGroupMember member)
    {
        return string.IsNullOrWhiteSpace(member.Email) ? member.Id : member.Email;
    }
}
