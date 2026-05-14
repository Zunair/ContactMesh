using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class GroupContactFactory
{
    public MeshContact CreateGroupContact(MeshGroup group, IEnumerable<string>? labels = null)
    {
        var displayName = string.IsNullOrWhiteSpace(group.DisplayName) ? group.Email : group.DisplayName;

        return new MeshContact
        {
            SourceId = $"group:{group.Id}",
            DisplayName = displayName,
            GivenName = displayName,
            FamilyName = "Group",
            Emails = new[] { new ContactEmail(group.Email, "work", true) },
            Labels = (labels ?? Array.Empty<string>())
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["groupId"] = group.Id,
                ["groupEmail"] = group.Email,
                ["sourceType"] = "group"
            }
        };
    }
}
