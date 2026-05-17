using ContactMesh.Core.Models;
using ContactMesh.Google.Auth;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Web.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
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
                DisableDeletes = true,
                ManagedEmailDomains = new[] { "example.org" },
                Rules = new SyncRuleOptions
                {
                    MainContactsGroupEmail = "company-directory@example.org",
                    MainContactsGroupLabel = "-Directory",
                    GroupContactPrefix = "#",
                    TargetUsers = new[] { "target@example.org" },
                    GlobalUserGroups = new[] { "all-users" },
                    GlobalExternalContactGroups = new[] { "external" },
                    GroupsToSyncByGroup = new[] { "contact-labels@example.org" },
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
            "settings.json",
            null);

        Assert.Contains("<title>ContactMesh Settings</title>", html);
        Assert.Contains("<form method=\"post\" action=\"/settings\">", html);
        Assert.Contains("value=\"Google\"", html);
        Assert.Contains("checked", html);
        Assert.Contains("Deletes off", html);
        Assert.Contains("name=\"ContactMesh.DisableDeletes\"", html);
        Assert.Contains("example.org", html);
        Assert.Contains("target@example.org", html);
        Assert.Contains("company-directory@example.org", html);
        Assert.Contains("-Directory", html);
        Assert.Contains("#", html);
        Assert.Contains("all-users", html);
        Assert.Contains("contact-labels@example.org", html);
        Assert.Contains("source-group", html);
        Assert.Contains("target-group", html);
        Assert.Contains("service-account.json", html);
        Assert.Contains("Configured", html);
        Assert.Contains("name=\"ContactMesh.Rules.TargetUsers\"", html);
        Assert.Contains("name=\"Microsoft365.ClientSecret\"", html);
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
            "appsettings.json",
            null);

        Assert.Contains("&lt;Google&gt;", html);
        Assert.Contains("&lt;blocked&gt;", html);
        Assert.DoesNotContain("<Google>", html);
    }

    [Fact]
    public void RenderShowsEmptyRuleGroups()
    {
        var html = SettingsPageRenderer.Render(
            new ContactMeshOptions(),
            new GoogleWorkspaceOptions(),
            new Microsoft365Options(),
            "appsettings.json",
            null);

        Assert.Contains("Managed domains", html);
        Assert.Contains("Will be created on save", html);
    }

    [Fact]
    public void RenderShowsSettingDescriptionsForLiveProviderValidation()
    {
        var html = SettingsPageRenderer.Render(
            new ContactMeshOptions(),
            new GoogleWorkspaceOptions(),
            new Microsoft365Options(),
            "appsettings.json",
            "Saved");

        Assert.Contains("Saved", html);
        Assert.Contains("Keep this on for live-provider validation", html);
        Assert.Contains("live runs skip delete writes to providers", html);
        Assert.Contains("Optional user IDs or email addresses that limit who receives managed contacts", html);
        Assert.Contains("append =Ignore to reduce expected noise", html);
        Assert.Contains("Secret value is masked here", html);
        Assert.Contains("Keep the file outside the repository", html);
    }

    [Fact]
    public async Task SettingsFormModelSavesEditableSettingsAndPreservesBlankSecret()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["ContactMesh.Provider"] = "Microsoft365",
            ["ContactMesh.DryRun"] = "true",
            ["ContactMesh.DisableDeletes"] = "true",
            ["ContactMesh.ManagedEmailDomains"] = "example.org",
            ["ContactMesh.Rules.MainContactsGroupEmail"] = "company-directory@example.org",
            ["ContactMesh.Rules.MainContactsGroupLabel"] = "-Directory",
            ["ContactMesh.Rules.GroupContactPrefix"] = "#",
            ["ContactMesh.Rules.TargetUsers"] = "target@example.org",
            ["ContactMesh.Rules.GroupsToSyncByGroup"] = "contact-labels@example.org",
            ["ContactMesh.Rules.GroupMappings"] = "source@example.org -> target@example.org",
            ["GoogleWorkspace.Scopes"] = GoogleWorkspaceOptions.PeopleContactsScope,
            ["Microsoft365.TenantId"] = "tenant-id",
            ["Microsoft365.ClientId"] = "client-id",
            ["Microsoft365.ClientSecret"] = "",
            ["Microsoft365.Scopes"] = Microsoft365Options.DefaultGraphScope
        });
        var configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            var settings = SettingsFormModel.FromForm(
                form,
                new GoogleWorkspaceOptions(),
                new Microsoft365Options { ClientSecret = "existing-secret" });

            await settings.SaveAsync(configPath, TestContext.Current.CancellationToken);
            var json = await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken);

            Assert.Contains("\"Provider\": \"Microsoft365\"", json);
            Assert.Contains("\"DisableDeletes\": true", json);
            Assert.Contains("\"MainContactsGroupEmail\": \"company-directory@example.org\"", json);
            Assert.Contains("\"MainContactsGroupLabel\": \"-Directory\"", json);
            Assert.Contains("\"GroupContactPrefix\": \"#\"", json);
            Assert.Contains("\"target@example.org\"", json);
            Assert.Contains("\"contact-labels@example.org\"", json);
            Assert.Contains("\"From\": \"source@example.org\"", json);
            Assert.Contains("\"ClientSecret\": \"existing-secret\"", json);
        }
        finally
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
    }
}
