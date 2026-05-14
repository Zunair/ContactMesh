using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Contacts;

public static class MicrosoftContactMapper
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
