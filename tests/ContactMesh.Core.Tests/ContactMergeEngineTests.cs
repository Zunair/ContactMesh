// File: ContactMergeEngineTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Merge;
using ContactMesh.Core.Models;
using Xunit;

namespace ContactMesh.Core.Tests
{
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
        public void Merge_Clears_Managed_Organization_Fields_When_Source_Is_Blank()
        {
            var source = new MeshContact
            {
                SourceId = "user-1",
                CompanyName = null,
                Department = " ",
                JobTitle = null
            };
            var existing = new MeshContact
            {
                SourceId = "user-1",
                CompanyName = "Old Company",
                Department = "Old Department",
                JobTitle = "Old Title",
                Notes = "User-owned note"
            };

            var merged = new ContactMergeEngine().Merge(source, existing);

            Assert.Null(merged.CompanyName);
            Assert.Null(merged.Department);
            Assert.Null(merged.JobTitle);
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
        public void Merge_Deduplicates_Source_Phones_With_Same_Normalized_Number_And_Type()
        {
            // Two source entries with the same digits and same type collapse to one.
            var source = new MeshContact
            {
                Phones = new[]
                {
                    new ContactPhone("+1 (215) 555-0100", "work", true),
                    new ContactPhone("215-555-0100", "work")        // same digits, same type
                }
            };

            var merged = new ContactMergeEngine().Merge(source, new MeshContact());

            Assert.Single(merged.Phones);
            Assert.Equal("215-555-0100", merged.Phones[0].Number);
        }

        [Fact]
        public void Merge_Deduplicates_Source_Phones_With_Us_Country_Code_And_Uses_Hyphenated_Format()
        {
            var source = new MeshContact
            {
                Phones = new[]
                {
                    new ContactPhone("+12675073489", "work", true),
                    new ContactPhone("12675073489", "work"),
                    new ContactPhone("2675073489", "work"),
                    new ContactPhone("267-507-3489", "work")
                }
            };

            var merged = new ContactMergeEngine().Merge(source, new MeshContact());

            var phone = Assert.Single(merged.Phones);
            Assert.Equal("267-507-3489", phone.Number);
            Assert.Equal("work", phone.Type);
        }

        [Fact]
        public void Merge_Keeps_Both_Phones_When_Same_Number_Has_Different_Types()
        {
            // Directory contacts sometimes list the same physical number as both work and mobile.
            // Both entries must be preserved so no data is lost.
            var source = new MeshContact
            {
                Phones = new[]
                {
                    new ContactPhone("+12155550100", "work"),
                    new ContactPhone("+12155550100", "mobile")      // same digits, different type
                }
            };

            var merged = new ContactMergeEngine().Merge(source, new MeshContact());

            Assert.Equal(2, merged.Phones.Count);
            Assert.Contains(merged.Phones, p => p.Type == "work");
            Assert.Contains(merged.Phones, p => p.Type == "mobile");
        }

        [Fact]
        public void Merge_ForceDeduplicatePhones_Collapses_Same_Number_With_Different_Types()
        {
            var source = new MeshContact
            {
                Phones = new[]
                {
                    new ContactPhone("+12155550100", "work"),
                    new ContactPhone("+12155550100", "mobile")
                }
            };
            var existing = new MeshContact
            {
                Phones = new[]
                {
                    new ContactPhone("+1 (215) 555-0100", "work"),
                    new ContactPhone("+1 (215) 555-0100", "mobile")
                }
            };

            var options = new ContactMergeOptions { ForceDeduplicatePhones = true };
            var merged = new ContactMergeEngine(options: options).Merge(source, existing);

            var phone = Assert.Single(merged.Phones);
            Assert.Equal("215-555-0100", phone.Number);
            Assert.Equal("work", phone.Type);
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

        [Fact]
        public void Merge_ForceResetLabels_Replaces_All_Labels_Including_UserOwned()
        {
            var source = new MeshContact
            {
                Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
            };

            var existing = new MeshContact
            {
                Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sales", "Personal", "OldStaleLabel" }
            };

            var options = new ContactMergeOptions { ForceResetLabels = true };
            var merged = new ContactMergeEngine(options: options).Merge(source, existing);

            Assert.Single(merged.Labels);
            Assert.Contains("Directory", merged.Labels);
            Assert.DoesNotContain("Sales", merged.Labels);
            Assert.DoesNotContain("Personal", merged.Labels);
            Assert.DoesNotContain("OldStaleLabel", merged.Labels);
        }

        [Fact]
        public void Merge_Preserves_Stored_Phone_Number_Format_When_Normalized_Match()
        {
            // When a provider (e.g. Microsoft Graph) rewrites number formatting on storage,
            // the merge should keep the stored string so the next comparison sees no change.
            var source = new MeshContact
            {
                Phones = new[] { new ContactPhone("+12155550100", "work") }
            };

            var existing = new MeshContact
            {
                // Provider reformatted "+12155550100" to "+1 (215) 555-0100" when it was stored.
                Phones = new[] { new ContactPhone("+1 (215) 555-0100", "work") }
            };

            var merged = new ContactMergeEngine().Merge(source, existing);

            var phone = Assert.Single(merged.Phones);
            Assert.Equal("+1 (215) 555-0100", phone.Number);  // stored format, not source format
            Assert.Equal("work", phone.Type);
        }

        [Fact]
        public void Merge_Uses_Source_Phone_Number_When_No_Stored_Match()
        {
            // If the phone is new (not in existing), use the source format as-is.
            var source = new MeshContact
            {
                Phones = new[] { new ContactPhone("+12155550100", "work") }
            };

            var merged = new ContactMergeEngine().Merge(source, new MeshContact());

            var phone = Assert.Single(merged.Phones);
            Assert.Equal("215-555-0100", phone.Number);
        }

        [Fact]
        public void Merge_Uses_Source_Phone_When_Type_Differs_From_Stored()
        {
            // If the directory changes a phone's type (e.g. work to mobile), treat it as a new
            // phone (no stored match) so the update is written through to the provider.
            var source = new MeshContact
            {
                Phones = new[] { new ContactPhone("+12155550100", "mobile") }
            };

            var existing = new MeshContact
            {
                Phones = new[] { new ContactPhone("+1 (215) 555-0100", "work") }  // same number, different type
            };

            var merged = new ContactMergeEngine().Merge(source, existing);

            var phone = Assert.Single(merged.Phones);
            Assert.Equal("215-555-0100", phone.Number);
            Assert.Equal("mobile", phone.Type);
        }
    }
}
