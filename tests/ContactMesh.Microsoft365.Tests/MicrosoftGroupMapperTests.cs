// File: MicrosoftGroupMapperTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;
using ContactMesh.Microsoft365.Groups;
using Xunit;

namespace ContactMesh.Microsoft365.Tests
{
    public sealed class MicrosoftGroupMapperTests
    {
        [Fact]
        public void ToMeshGroup_Maps_Graph_Group_Visibility_And_Members()
        {
            var group = MicrosoftGroupMapper.ToMeshGroup(
                new MicrosoftGraphGroup
                {
                    Id = "group-1",
                    Mail = "team@example.org",
                    DisplayName = "Team",
                    Visibility = "Public"
                },
                new[]
                {
                    new MicrosoftGraphGroupMember
                    {
                        Id = "user-1",
                        ODataType = "#microsoft.graph.user",
                        Mail = "jane@example.org",
                        ProxyAddresses = new[] { "SMTP:jane.primary@example.org", "smtp:jane@example.org" },
                        DisplayName = "Jane Doe"
                    },
                    new MicrosoftGraphGroupMember
                    {
                        Id = "nested-group",
                        ODataType = "#microsoft.graph.group",
                        Mail = "nested@example.org"
                    },
                    new MicrosoftGraphGroupMember
                    {
                        Id = "contact-1",
                        ODataType = "#microsoft.graph.orgContact",
                        Mail = "external@example.org"
                    },
                    new MicrosoftGraphGroupMember
                    {
                        Id = "missing-email",
                        ODataType = "#microsoft.graph.user"
                    }
                });

            Assert.Equal("group-1", group.Id);
            Assert.Equal("team@example.org", group.Email);
            Assert.Equal("Team", group.DisplayName);
            Assert.Equal(MeshGroupVisibility.Domain, group.GroupVisibility);
            Assert.Equal(MeshGroupVisibility.Domain, group.MemberVisibility);
            Assert.Equal(3, group.Members.Count);
            var userMember = Assert.Single(group.Members, member => member.Type == MeshGroupMemberType.User);
            Assert.Equal("jane.primary@example.org", userMember.Email);
            Assert.Contains("jane@example.org", userMember.AlternateEmails);
            Assert.Contains("mismatched email identity values", Assert.Single(userMember.Warnings));
            Assert.Contains(group.Members, member => member.Type == MeshGroupMemberType.Group && member.Email == "nested@example.org");
            Assert.Contains(group.Members, member => member.Type == MeshGroupMemberType.Contact && member.Email == "external@example.org");
        }

        [Theory]
        [InlineData("Public", MeshGroupVisibility.Domain, MeshGroupVisibility.Domain)]
        [InlineData("Private", MeshGroupVisibility.Members, MeshGroupVisibility.Members)]
        [InlineData("HiddenMembership", MeshGroupVisibility.Members, MeshGroupVisibility.Hidden)]
        [InlineData(null, MeshGroupVisibility.Hidden, MeshGroupVisibility.Hidden)]
        public void Visibility_Mapping_Is_Conservative(
            string? graphVisibility,
            MeshGroupVisibility expectedGroupVisibility,
            MeshGroupVisibility expectedMemberVisibility)
        {
            Assert.Equal(expectedGroupVisibility, MicrosoftGroupMapper.ToGroupVisibility(graphVisibility));
            Assert.Equal(expectedMemberVisibility, MicrosoftGroupMapper.ToMemberVisibility(graphVisibility));
        }

        [Fact]
        public void ToMeshContact_Maps_Graph_OrgContact_To_Managed_Contact()
        {
            var contact = MicrosoftGroupMapper.ToMeshContact(new MicrosoftGraphGroupMember
            {
                Id = "contact-1",
                ODataType = "#microsoft.graph.orgContact",
                Mail = "external@example.org",
                DisplayName = "External Person",
                GivenName = "External",
                Surname = "Person",
                CompanyName = "Example",
                Department = "Partners",
                JobTitle = "Advisor",
                BusinessPhones = new[] { "+1 215 555 0100" },
                MobilePhone = "+1 215 555 0101"
            });

            Assert.Equal("orgContact:contact-1", contact.SourceId);
            Assert.Equal("External Person", contact.DisplayName);
            Assert.Equal("External", contact.GivenName);
            Assert.Equal("Person", contact.FamilyName);
            Assert.Equal("Example", contact.CompanyName);
            Assert.Equal("Partners", contact.Department);
            Assert.Equal("Advisor", contact.JobTitle);
            Assert.Equal("external@example.org", Assert.Single(contact.Emails).Address);
            Assert.Contains(new ContactPhone("+1 215 555 0100", "work"), contact.Phones);
            Assert.Contains(new ContactPhone("+1 215 555 0101", "mobile"), contact.Phones);
            Assert.Equal("contact-1", contact.Metadata[MicrosoftGroupMapper.OrgContactIdMetadataKey]);
        }

        [Theory]
        [InlineData(true, false, new[] { "Unified" }, MicrosoftGroupType.Microsoft365)]
        [InlineData(true, true, new string[0], MicrosoftGroupType.MailEnabledSecurity)]
        [InlineData(true, false, new string[0], MicrosoftGroupType.Distribution)]
        public void GetGroupType_Classifies_Known_Group_Types(
            bool mailEnabled,
            bool securityEnabled,
            string[] groupTypes,
            MicrosoftGroupType expectedType)
        {
            var group = new MicrosoftGraphGroup
            {
                MailEnabled = mailEnabled,
                SecurityEnabled = securityEnabled,
                GroupTypes = groupTypes
            };

            Assert.Equal(expectedType, MicrosoftGroupMapper.GetGroupType(group));
        }

        [Fact]
        public void GetGroupType_Returns_Null_For_Non_Mail_Enabled_Security_Group()
        {
            var group = new MicrosoftGraphGroup
            {
                MailEnabled = false,
                SecurityEnabled = true,
                GroupTypes = Array.Empty<string>()
            };

            Assert.Null(MicrosoftGroupMapper.GetGroupType(group));
        }
    }
}
