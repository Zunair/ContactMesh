using ContactMesh.Microsoft365.Directory;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftDirectoryMapperTests
{
    [Fact]
    public void ToMeshUser_Maps_Identity_Email_And_DisplayName()
    {
        var user = MicrosoftDirectoryMapper.ToMeshUser("graph-user-1", "jane@example.org", "Jane Doe", "Jane", "Doe", "/Staff");

        Assert.Equal("graph-user-1", user.Id);
        Assert.Equal("jane@example.org", user.Email);
        Assert.Equal("Jane Doe", user.DisplayName);
        Assert.Equal("Jane", user.GivenName);
        Assert.Equal("Doe", user.FamilyName);
        Assert.Equal("/Staff", user.OrganizationUnit);
    }

    [Fact]
    public void ToMeshUser_Maps_Graph_User_Profile_And_Status()
    {
        var user = MicrosoftDirectoryMapper.ToMeshUser(new MicrosoftGraphUser
        {
            Id = " graph-user-1 ",
            UserPrincipalName = " jane@example.org ",
            DisplayName = "Jane Doe",
            GivenName = "Jane",
            Surname = "Doe",
            CompanyName = "Example",
            Department = "Engineering",
            JobTitle = "Director",
            BusinessPhones = new[] { " +1 215 555 0100 ", " " },
            MobilePhone = " +1 215 555 0101 ",
            AccountEnabled = false
        });

        Assert.Equal("graph-user-1", user.Id);
        Assert.Equal("jane@example.org", user.Email);
        Assert.Equal("Jane Doe", user.DisplayName);
        Assert.Equal("Jane", user.GivenName);
        Assert.Equal("Doe", user.FamilyName);
        Assert.Equal("Example", user.CompanyName);
        Assert.Equal("Engineering", user.Department);
        Assert.Equal("Director", user.JobTitle);
        Assert.True(user.IsSuspended);
        Assert.Collection(
            user.Phones,
            phone =>
            {
                Assert.Equal("+1 215 555 0100", phone.Number);
                Assert.Equal("work", phone.Type);
            },
            phone =>
            {
                Assert.Equal("+1 215 555 0101", phone.Number);
                Assert.Equal("mobile", phone.Type);
            });
    }

    [Fact]
    public void ToMeshUser_Prefers_Mail_Over_UserPrincipalName()
    {
        var user = MicrosoftDirectoryMapper.ToMeshUser(new MicrosoftGraphUser
        {
            Id = "graph-user-1",
            Mail = "jane.alias@example.org",
            UserPrincipalName = "jane@example.org"
        });

        Assert.Equal("jane.alias@example.org", user.Email);
    }

    [Fact]
    public void ToMeshUser_Prefers_Primary_Proxy_And_Warns_On_Mismatch()
    {
        var user = MicrosoftDirectoryMapper.ToMeshUser(new MicrosoftGraphUser
        {
            Id = "graph-user-1",
            DisplayName = "Jane Doe",
            Mail = "jane.mail@example.org",
            UserPrincipalName = "jane.upn@example.org",
            ProxyAddresses = new[] { "smtp:jane.mail@example.org", "SMTP:jane.primary@example.org" }
        });

        Assert.Equal("jane.primary@example.org", user.Email);
        Assert.Contains("jane.mail@example.org", user.AlternateEmails);
        Assert.Contains("jane.upn@example.org", user.AlternateEmails);
        Assert.Contains("mismatched email identity values", Assert.Single(user.Warnings));
    }

    [Fact]
    public void ToMeshUser_Can_Carry_Guest_Ext_Upn_Warning_For_Core_Filtering()
    {
        var user = MicrosoftDirectoryMapper.ToMeshUser(new MicrosoftGraphUser
        {
            Id = "guest-1",
            DisplayName = "External Guest",
            Mail = "external@partner.example.net",
            UserPrincipalName = "external_partner.example.net#EXT#@tenant.onmicrosoft.com",
            ProxyAddresses = new[] { "SMTP:external@partner.example.net" },
            AccountEnabled = true,
            UserType = "Guest"
        });

        Assert.True(user.IsSuspended);
        Assert.Equal("external@partner.example.net", user.Email);
        Assert.Contains("#EXT#", Assert.Single(user.AlternateEmails), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mismatched email identity values", Assert.Single(user.Warnings));
    }

    [Theory]
    [InlineData("Guest")]
    [InlineData("guest")]
    [InlineData("ExternalMember")]
    public void ToMeshUser_Marks_Non_Member_UserType_As_Suspended(string userType)
    {
        var user = MicrosoftDirectoryMapper.ToMeshUser(new MicrosoftGraphUser
        {
            Id = "guest-1",
            UserPrincipalName = "guest_example.org#EXT#@contoso.onmicrosoft.com",
            AccountEnabled = true,
            UserType = userType
        });

        Assert.True(user.IsSuspended);
    }

    [Fact]
    public void ToMeshUser_Does_Not_Mark_Member_UserType_As_Suspended()
    {
        var user = MicrosoftDirectoryMapper.ToMeshUser(new MicrosoftGraphUser
        {
            Id = "member-1",
            UserPrincipalName = "member@example.org",
            AccountEnabled = true,
            UserType = "Member"
        });

        Assert.False(user.IsSuspended);
    }
}
