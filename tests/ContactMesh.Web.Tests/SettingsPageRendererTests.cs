using ContactMesh.Core.Models;
using ContactMesh.Google.Auth;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Web.Settings;
using Xunit;

namespace ContactMesh.Web.Tests;

public sealed class SettingsPageRendererTests
{
    [Fact]
    public void RenderShowsBoundContactMeshSettings()
    {
        var html = SettingsPageRenderer.Render(
            new ContactMeshOptions
            {
                Provider = "Google",
                DryRun = true,
                ManagedEmailDomains = new[] { "example.org" },
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "target@example.org" },
                    GlobalUserGroups = new[] { "all-users" },
                    GlobalExternalContactGroups = new[] { "external" },
                    ExclusionGroups = new[] { "blocked" },
                    ScopedGroupRoots = new[] { "department-root" },
                    IncludedOrganizationUnits = new[] { "/" },
                    ExcludedOrganizationUnits = new[] { "/Service Accounts=Ignore" },
                    GroupMappings = new[] { new GroupMapping("source-group", "target-group") }
                }
            },
            new GoogleWorkspaceOptions
            {
                ServiceAccountFile = "service-account.json",
                AdminUserEmail = "admin@example.org",
                Scopes = new[] { GoogleWorkspaceOptions.PeopleContactsScope }
            },
            new Microsoft365Options
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "super-secret",
                Scopes = new[] { "https://graph.microsoft.com/.default" }
            },
            "settings.json");

        Assert.Contains("<title>ContactMesh Settings</title>", html);
        Assert.Contains("value=\"Google\"", html);
        Assert.Contains("checked", html);
        Assert.Contains("example.org", html);
        Assert.Contains("target@example.org", html);
        Assert.Contains("all-users", html);
        Assert.Contains("source-group", html);
        Assert.Contains("target-group", html);
        Assert.Contains("service-account.json", html);
        Assert.Contains("Configured", html);
        Assert.DoesNotContain("super-secret", html);
    }

    [Fact]
    public void RenderEncodesConfiguredValues()
    {
        var html = SettingsPageRenderer.Render(
            new ContactMeshOptions
            {
                Provider = "<Google>",
                Rules = new SyncRuleOptions
                {
                    ExclusionGroups = new[] { "<blocked>" }
                }
            },
            new GoogleWorkspaceOptions(),
            new Microsoft365Options(),
            "<appsettings>.json");

        Assert.Contains("&lt;Google&gt;", html);
        Assert.Contains("&lt;blocked&gt;", html);
        Assert.Contains("&lt;appsettings&gt;.json", html);
        Assert.DoesNotContain("<Google>", html);
    }

    [Fact]
    public void RenderShowsEmptyRuleGroups()
    {
        var html = SettingsPageRenderer.Render(
            new ContactMeshOptions(),
            new GoogleWorkspaceOptions(),
            new Microsoft365Options(),
            "appsettings.json");

        Assert.Contains("Managed domains", html);
        Assert.Contains("None", html);
        Assert.Contains("Not set", html);
    }
}
