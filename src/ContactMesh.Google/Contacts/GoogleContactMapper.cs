using ContactMesh.Core.Models;

namespace ContactMesh.Google.Contacts;

public static class GoogleContactMapper
{
    public static MeshContact ToMeshContact(string sourceId, string displayName, string email)
    {
        return new MeshContact
        {
            SourceId = sourceId,
            DisplayName = displayName,
            Emails = new[] { new ContactEmail(email, "work", true) }
        };
    }
}
