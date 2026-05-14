using ContactMesh.Core.Models;

namespace ContactMesh.Google.Directory;

public static class GoogleDirectoryMapper
{
    public static MeshUser ToMeshUser(string id, string email, string? displayName = null)
    {
        return new MeshUser
        {
            Id = id,
            Email = email,
            DisplayName = displayName
        };
    }
}
