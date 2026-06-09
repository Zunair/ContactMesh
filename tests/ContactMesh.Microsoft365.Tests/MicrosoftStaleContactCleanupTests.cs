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

    [Fact]
    public void HasManagedMarker_Treats_Managed_Folder_Metadata_As_Marker()
    {
        var contact = new MeshContact
        {
            DisplayName = "Legacy Folder Contact",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [MicrosoftContactMapper.ContactIdMetadataKey] = "contact-1",
                [MicrosoftContactMapper.ContactFolderIdMetadataKey] = "folder-1",
                [MicrosoftContactMapper.ManagedFolderLabelMetadataKey] = "-Directory"
            }
        };
        var engine = new StaleContactCleanupEngine(new StaleContactCleanupOptions
        {
            ManagedMarkerMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                MicrosoftContactMapper.ManagedFolderLabelMetadataKey
            },
            OperationalMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                MicrosoftContactMapper.ContactIdMetadataKey,
                MicrosoftContactMapper.ContactFolderIdMetadataKey,
                MicrosoftContactMapper.ManagedFolderLabelMetadataKey
            }
        });

        Assert.True(engine.HasManagedMarker(contact));
        Assert.True(engine.Clean(contact).ShouldDelete);
    }
}
