using ContactMesh.Core.Merge;
using ContactMesh.Core.Models;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class DuplicateContactDetectorTests
{
    [Fact]
    public void FindDuplicates_Groups_Contacts_With_Matching_Email()
    {
        var first = new MeshContact { DisplayName = "Jane", Emails = new[] { new ContactEmail("Jane@example.org") } };
        var second = new MeshContact { DisplayName = "Jane Alt", Emails = new[] { new ContactEmail("jane@example.org") } };
        var third = new MeshContact { DisplayName = "Other", Emails = new[] { new ContactEmail("other@example.org") } };

        var duplicates = new DuplicateContactDetector().FindDuplicates(new[] { first, second, third });

        var duplicateGroup = Assert.Single(duplicates);
        Assert.Contains(first, duplicateGroup);
        Assert.Contains(second, duplicateGroup);
        Assert.DoesNotContain(third, duplicateGroup);
    }

    [Fact]
    public void FindDuplicates_Groups_Contacts_With_Matching_Normalized_Phone()
    {
        var first = new MeshContact { DisplayName = "Jane", Phones = new[] { new ContactPhone("+1 (215) 555-0100") } };
        var second = new MeshContact { DisplayName = "Jane Alt", Phones = new[] { new ContactPhone("215-555-0100") } };

        var duplicates = new DuplicateContactDetector().FindDuplicates(new[] { first, second });

        var duplicateGroup = Assert.Single(duplicates);
        Assert.Contains(first, duplicateGroup);
        Assert.Contains(second, duplicateGroup);
    }
}
