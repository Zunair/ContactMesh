using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using ContactMesh.Microsoft365.Contacts;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftStaleContactCleanupTests
{
    [Fact]
    public void Clean_Treats_Graph_Contact_Metadata_As_Managed()
    {
        var contact = new MeshContact
        {
            DisplayName = "Former Employee",
            CompanyName = "Example",
            Emails = new[] { new ContactEmail("former@example.org", "work", true) },
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [MicrosoftContactMapper.ContactIdMetadataKey] = "contact-1",
                [MicrosoftContactMapper.ChangeKeyMetadataKey] = "change-1"
            }
        };
        var engine = new StaleContactCleanupEngine(new StaleContactCleanupOptions
        {
            ManagedEmailDomains = new[] { "example.org" },
            ManagedMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                MicrosoftContactMapper.ContactIdMetadataKey,
                MicrosoftContactMapper.ChangeKeyMetadataKey
            }
        });

        var result = engine.Clean(contact);

        Assert.True(result.ShouldDelete);
        Assert.Equal("Managed contact is stale and has no user-owned details.", result.Reason);
    }
}
