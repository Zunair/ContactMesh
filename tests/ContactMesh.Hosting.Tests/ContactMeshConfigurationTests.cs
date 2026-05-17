using ContactMesh.Core.Models;
using ContactMesh.Google.Auth;
using ContactMesh.Hosting;
using ContactMesh.Microsoft365.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ContactMesh.Hosting.Tests;

public sealed class ContactMeshConfigurationTests
{
    [Fact]
    public void AddContactMeshOptionsBindsAllHostOptionSections()
    {
        var values = new Dictionary<string, string?>
        {
            ["ContactMesh:Provider"] = "Google",
            ["ContactMesh:DryRun"] = "false",
            ["ContactMesh:DisableDeletes"] = "true",
            ["ContactMesh:ManagedEmailDomains:0"] = "example.org",
            ["ContactMesh:Rules:TargetUsers:0"] = "target@example.org",
            ["ContactMesh:Rules:MainContactsGroupEmail"] = "company-directory@example.org",
            ["ContactMesh:Rules:MainContactsGroupLabel"] = "-Directory",
            ["ContactMesh:Rules:GroupContactPrefix"] = "#",
            ["ContactMesh:Rules:GlobalUserGroups:0"] = "all-users",
            ["ContactMesh:Rules:GlobalExternalContactGroups:0"] = "external-users",
            ["ContactMesh:Rules:GroupsToSyncByGroup:0"] = "contact-labels@example.org",
            ["ContactMesh:Rules:ExclusionGroups:0"] = "blocked",
            ["ContactMesh:Rules:ScopedGroupRoots:0"] = "engineering",
            ["ContactMesh:Rules:GroupMappings:0:From"] = "source-group",
            ["ContactMesh:Rules:GroupMappings:0:To"] = "target-group",
            ["ContactMesh:Rules:IncludedOrganizationUnits:0"] = "/",
            ["ContactMesh:Rules:ExcludedOrganizationUnits:0"] = "/Service Accounts=Ignore",
            ["GoogleWorkspace:ServiceAccountFile"] = "service-account.json",
            ["GoogleWorkspace:AdminUserEmail"] = "admin@example.org",
            ["GoogleWorkspace:Scopes:0"] = GoogleWorkspaceOptions.PeopleContactsScope,
            ["Microsoft365:TenantId"] = "tenant-id",
            ["Microsoft365:ClientId"] = "client-id",
            ["Microsoft365:ClientSecret"] = "client-secret",
            ["Microsoft365:Scopes:0"] = "https://graph.microsoft.com/.default"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection()
            .AddContactMeshOptions(configuration)
            .BuildServiceProvider();

        var contactMesh = services.GetRequiredService<IOptions<ContactMeshOptions>>().Value;
        var rules = services.GetRequiredService<IOptions<SyncRuleOptions>>().Value;
        var googleWorkspace = services.GetRequiredService<IOptions<GoogleWorkspaceOptions>>().Value;
        var microsoft365 = services.GetRequiredService<IOptions<Microsoft365Options>>().Value;

        Assert.Equal("Google", contactMesh.Provider);
        Assert.False(contactMesh.DryRun);
        Assert.True(contactMesh.DisableDeletes);
        Assert.Equal("example.org", Assert.Single(contactMesh.ManagedEmailDomains));
        Assert.Equal("target@example.org", Assert.Single(contactMesh.Rules.TargetUsers));
        Assert.Equal("company-directory@example.org", contactMesh.Rules.MainContactsGroupEmail);
        Assert.Equal("-Directory", contactMesh.Rules.MainContactsGroupLabel);
        Assert.Equal("#", contactMesh.Rules.GroupContactPrefix);
        Assert.Equal("all-users", Assert.Single(contactMesh.Rules.GlobalUserGroups));
        Assert.Equal("contact-labels@example.org", Assert.Single(contactMesh.Rules.GroupsToSyncByGroup));
        Assert.Equal("source-group", Assert.Single(contactMesh.Rules.GroupMappings).From);
        Assert.Equal("target-group", Assert.Single(contactMesh.Rules.GroupMappings).To);

        Assert.Equal("all-users", Assert.Single(rules.GlobalUserGroups));
        Assert.Equal("company-directory@example.org", rules.MainContactsGroupEmail);
        Assert.Equal("-Directory", rules.MainContactsGroupLabel);
        Assert.Equal("#", rules.GroupContactPrefix);
        Assert.Equal("external-users", Assert.Single(rules.GlobalExternalContactGroups));
        Assert.Equal("contact-labels@example.org", Assert.Single(rules.GroupsToSyncByGroup));
        Assert.Equal("blocked", Assert.Single(rules.ExclusionGroups));
        Assert.Equal("engineering", Assert.Single(rules.ScopedGroupRoots));
        Assert.Equal("/", Assert.Single(rules.IncludedOrganizationUnits));
        Assert.Equal("/Service Accounts=Ignore", Assert.Single(rules.ExcludedOrganizationUnits));

        Assert.Equal("service-account.json", googleWorkspace.ServiceAccountFile);
        Assert.Equal("admin@example.org", googleWorkspace.AdminUserEmail);
        Assert.Equal(GoogleWorkspaceOptions.PeopleContactsScope, Assert.Single(googleWorkspace.Scopes));

        Assert.Equal("tenant-id", microsoft365.TenantId);
        Assert.Equal("client-id", microsoft365.ClientId);
        Assert.Equal("client-secret", microsoft365.ClientSecret);
        Assert.Equal("https://graph.microsoft.com/.default", Assert.Single(microsoft365.Scopes));
    }

    [Fact]
    public void ResolveConfigPathUsesFirstJsonArgumentOrDefault()
    {
        Assert.Equal("custom.json", ContactMeshConfiguration.ResolveConfigPath(new[] { "--dry-run", "custom.json" }));
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var child = Path.Combine(root, "src", "ContactMesh.Web");
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(root, "ContactMesh.sln"), string.Empty);
        File.WriteAllText(Path.Combine(root, "appsettings.local.json"), "{}");

        try
        {
            Directory.SetCurrentDirectory(child);

            Assert.Equal(
                Path.Combine(root, "appsettings.local.json"),
                ContactMeshConfiguration.ResolveConfigPath(new[] { "--dry-run" }));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AddContactMeshConfigFileLetsCommandLineOverrideJson()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "ContactMesh": {
                "Provider": "Google",
                "DryRun": true
              }
            }
            """);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddContactMeshConfigFile(
                    configPath,
                    new[] { "--ContactMesh:Provider=Microsoft365", "--ContactMesh:DryRun=false" })
                .Build();

            Assert.Equal("Microsoft365", configuration["ContactMesh:Provider"]);
            Assert.Equal("false", configuration["ContactMesh:DryRun"]);
        }
        finally
        {
            File.Delete(configPath);
        }
    }
}
