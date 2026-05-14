using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Groups;

public sealed class MicrosoftGroupResolver
{
    public IReadOnlySet<string> ResolveMemberEmails(MeshGroup group)
    {
        return group.Members.Select(member => member.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
