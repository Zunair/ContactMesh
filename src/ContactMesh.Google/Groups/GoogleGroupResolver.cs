// File: GoogleGroupResolver.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Google.Groups
{
    public sealed class GoogleGroupResolver
    {
        public IReadOnlySet<string> ResolveMemberEmails(MeshGroup group)
        {
            return group.Members.Select(member => member.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
