// File: GoogleDirectoryMapper.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Google.Directory
{
    public static class GoogleDirectoryMapper
    {
        public static MeshUser ToMeshUser(
            string id,
            string email,
            string? displayName = null,
            string? givenName = null,
            string? familyName = null,
            string? organizationUnit = null)
        {
            return new MeshUser
            {
                Id = id,
                Email = email,
                DisplayName = displayName,
                GivenName = givenName,
                FamilyName = familyName,
                OrganizationUnit = organizationUnit
            };
        }
    }
}
