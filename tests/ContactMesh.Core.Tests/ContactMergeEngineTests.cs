using ContactMesh.Core.Merge;
using ContactMesh.Core.Models;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class ContactMergeEngineTests
{
    [Fact]
    public void Merge_Preserves_UserOwned_Email_Phone_And_Notes()
    {
        var source = new MeshContact
        {
            SourceId = "user-1",
            DisplayName = "Jane Doe",
            Notes = "Managed note should not overwrite user notes.",
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) },
            Phones = new[] { new ContactPhone("215-555-0100", "work", true) }
        };

        var existing = new MeshContact
        {
            SourceId = "user-1",
            DisplayName = "Jane Old",
            Notes = "User-owned note",
            Emails = new[]
            {
                new ContactEmail("jane@example.org", "work", true),
                new ContactEmail("jane.personal@example.net", "home")
            },
            Phones = new[]
            {
                new ContactPhone("+1 (215) 555-0100", "work", true),
                new ContactPhone("267-555-2222", "mobile")
            }
        };

        var merged = new ContactMergeEngine().Merge(source, existing);

        Assert.Contains(merged.Emails, e => e.Address == "jane.personal@example.net");
        Assert.Contains(merged.Phones, p => p.Number == "267-555-2222");
        Assert.Equal("Jane Doe", merged.DisplayName);
        Assert.Equal("User-owned note", merged.Notes);
    }

    [Fact]
    public void Merge_Deduplicates_Source_Emails_Case_Insensitively()
    {
        var source = new MeshContact
        {
            Emails = new[]
            {
                new ContactEmail("Jane@example.org", "work", true),
                new ContactEmail("jane@example.org", "work")
            }
        };

        var merged = new ContactMergeEngine().Merge(source, new MeshContact());

        Assert.Single(merged.Emails);
        Assert.Equal("Jane@example.org", merged.Emails[0].Address);
    }

    [Fact]
    public void Merge_Deduplicates_Source_Phones_By_Normalized_Digits()
    {
        var source = new MeshContact
        {
            Phones = new[]
            {
                new ContactPhone("+1 (215) 555-0100", "work", true),
                new ContactPhone("215-555-0100", "mobile")
            }
        };

        var merged = new ContactMergeEngine().Merge(source, new MeshContact());

        Assert.Single(merged.Phones);
        Assert.Equal("+1 (215) 555-0100", merged.Phones[0].Number);
    }

    [Fact]
    public void Merge_Source_Metadata_Overwrites_Existing_Metadata()
    {
        var source = new MeshContact
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["etag"] = "source-etag",
                ["source"] = "directory"
            }
        };

        var existing = new MeshContact
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["etag"] = "existing-etag",
                ["user"] = "kept"
            }
        };

        var merged = new ContactMergeEngine().Merge(source, existing);

        Assert.Equal("source-etag", merged.Metadata["etag"]);
        Assert.Equal("directory", merged.Metadata["source"]);
        Assert.Equal("kept", merged.Metadata["user"]);
    }

    [Fact]
    public void Merge_Combines_Source_And_Existing_Labels_Case_Insensitively()
    {
        var source = new MeshContact
        {
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory", "Sales" }
        };

        var existing = new MeshContact
        {
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "directory", "Personal" }
        };

        var merged = new ContactMergeEngine().Merge(source, existing);

        Assert.Equal(3, merged.Labels.Count);
        Assert.Contains("Directory", merged.Labels);
        Assert.Contains("Sales", merged.Labels);
        Assert.Contains("Personal", merged.Labels);
    }

    [Fact]
    public void Merge_Removes_Stale_Managed_Labels_But_Preserves_UserOwned_Labels()
    {
        var source = new MeshContact
        {
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
        };

        var existing = new MeshContact
        {
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "directory", "Sales", "Personal" }
        };

        var options = new ContactMergeOptions
        {
            ManagedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory", "Sales" }
        };

        var merged = new ContactMergeEngine(options: options).Merge(source, existing);

        Assert.Equal(2, merged.Labels.Count);
        Assert.Contains("Directory", merged.Labels);
        Assert.Contains("Personal", merged.Labels);
        Assert.DoesNotContain("Sales", merged.Labels);
    }

    [Fact]
    public void Merge_Keeps_Label_Union_When_No_Managed_Label_Set_Configured()
    {
        var source = new MeshContact
        {
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
        };

        var existing = new MeshContact
        {
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sales", "Personal" }
        };

        var merged = new ContactMergeEngine().Merge(source, existing);

        Assert.Equal(3, merged.Labels.Count);
        Assert.Contains("Directory", merged.Labels);
        Assert.Contains("Sales", merged.Labels);
        Assert.Contains("Personal", merged.Labels);
    }
}
