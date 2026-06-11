// File: SettingsPageRendererTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;
using ContactMesh.Core.Security;
using ContactMesh.Google.Auth;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Web.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace ContactMesh.Web.Tests
{
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
                    ForceDeduplicatePhones = true,
                    ForceNormalizeEmailTypes = true,
                    ManagedEmailDomains = new[] { "example.org" },
                    Notifications = new()
                    {
                        Enabled = true,
                        From = "sender@example.org",
                        SuccessTo = new[] { "success@example.org" },
                        FailureTo = new[] { "failure@example.org" },
                        SubjectPrefix = "[Mesh]",
                        AttachCsvOnFailure = true,
                        MaxAttachmentBytes = 1024
                    },
                    Rules = new SyncRuleOptions
                    {
                        MainContactsGroupEmails = new[] { "company-directory@example.org", "contractors@example.org" },
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
                    Scopes = new[] { "https://graph.microsoft.com/.default" },
                    GroupTypes = new[] { "Microsoft365", "Distribution" }
                },
                "settings.json",
                null);

            Assert.Contains("<title>ContactMesh Settings</title>", html);
            Assert.Contains("<form method=\"post\" action=\"/settings\">", html);
            Assert.Contains("<select name=\"ContactMesh.Provider\">", html);
            Assert.Contains("<option value=\"Google\" selected>Google</option>", html);
            Assert.Contains("<option value=\"Microsoft365\">Microsoft365</option>", html);
            Assert.Contains("checked", html);
            Assert.Contains("Deletes off", html);
            Assert.Contains("name=\"ContactMesh.DisableDeletes\"", html);
            Assert.Contains("name=\"ContactMesh.ForceDeduplicatePhones\"", html);
            Assert.Contains("name=\"ContactMesh.ForceNormalizeEmailTypes\"", html);
            Assert.Contains("example.org", html);
            Assert.Contains("name=\"ContactMesh.Notifications.From\"", html);
            Assert.Contains("sender@example.org", html);
            Assert.Contains("success@example.org", html);
            Assert.Contains("failure@example.org", html);
            Assert.Contains("Application permission Mail.Send", html);
            Assert.Contains("formaction=\"/settings/test-email\"", html);
            Assert.Contains("target@example.org", html);
            Assert.Contains("company-directory@example.org", html);
            Assert.Contains("contractors@example.org", html);
            Assert.Contains("name=\"ContactMesh.Rules.MainContactsGroupEmails\"", html);
            Assert.Contains("-Directory", html);
            Assert.Contains("#", html);
            Assert.Contains("all-users", html);
            Assert.Contains("contact-labels@example.org", html);
            Assert.DoesNotContain("Scoped group roots", html);
            Assert.DoesNotContain("department-root", html);
            Assert.Contains("source-group", html);
            Assert.Contains("target-group", html);
            Assert.Contains("service-account.json", html);
            Assert.Contains("Configured", html);
            Assert.Contains("name=\"ContactMesh.Rules.TargetUsers\"", html);
            Assert.Contains("name=\"Microsoft365.ClientSecret\"", html);
            Assert.Contains("<details class=\"checkbox-dropdown\">", html);
            Assert.Contains("value=\"Microsoft365\" checked", html);
            Assert.Contains("value=\"Distribution\" checked", html);
            Assert.Contains("value=\"MailEnabledSecurity\"", html);
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
            Assert.Contains("<option value=\"Scaffolded\" selected>Scaffolded</option>", html);
        }

        [Fact]
        public void RenderPreservesUnknownProviderInDropdown()
        {
            var html = SettingsPageRenderer.Render(
                new ContactMeshOptions
                {
                    Provider = "CustomProvider"
                },
                new GoogleWorkspaceOptions(),
                new Microsoft365Options(),
                "appsettings.json",
                null);

            Assert.Contains("<select name=\"ContactMesh.Provider\">", html);
            Assert.Contains("<option value=\"CustomProvider\" selected>CustomProvider</option>", html);
        }

        [Fact]
        public void RenderPreservesCustomGroupTypeInChecklist()
        {
            var html = SettingsPageRenderer.Render(
                new ContactMeshOptions(),
                new GoogleWorkspaceOptions(),
                new Microsoft365Options
                {
                    GroupTypes = new[] { "CustomType" }
                },
                "appsettings.json",
                null);

            Assert.Contains("CustomType", html);
            Assert.Contains("value=\"CustomType\" checked", html);
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
            Assert.Contains("Use once to clean legacy duplicates", html);
            Assert.Contains("no longer show the organization address under Other email", html);
            Assert.Contains("Sends one test message to all configured success and failure recipients", html);
            Assert.Contains("Use Application permissions, not Delegated permissions", html);
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
                ["ContactMesh.ForceDeduplicatePhones"] = "true",
                ["ContactMesh.ForceNormalizeEmailTypes"] = "true",
                ["ContactMesh.ManagedEmailDomains"] = "example.org",
                ["ContactMesh.Notifications.Enabled"] = "true",
                ["ContactMesh.Notifications.From"] = "sender@example.org",
                ["ContactMesh.Notifications.SuccessTo"] = "success@example.org",
                ["ContactMesh.Notifications.FailureTo"] = "failure@example.org",
                ["ContactMesh.Notifications.SubjectPrefix"] = "[Mesh]",
                ["ContactMesh.Notifications.AttachCsvOnFailure"] = "true",
                ["ContactMesh.Notifications.MaxAttachmentBytes"] = "2048",
                ["ContactMesh.Rules.MainContactsGroupEmails"] = "company-directory@example.org\ncontractors@example.org",
                ["ContactMesh.Rules.MainContactsGroupLabel"] = "-Directory",
                ["ContactMesh.Rules.GroupContactPrefix"] = "#",
                ["ContactMesh.Rules.TargetUsers"] = "target@example.org",
                ["ContactMesh.Rules.GroupsToSyncByGroup"] = "contact-labels@example.org",
                ["ContactMesh.Rules.GroupMappings"] = "source@example.org -> target@example.org",
                ["GoogleWorkspace.Scopes"] = GoogleWorkspaceOptions.PeopleContactsScope,
                ["Microsoft365.TenantId"] = "tenant-id",
                ["Microsoft365.ClientId"] = "client-id",
                ["Microsoft365.ClientSecret"] = "",
                ["Microsoft365.Scopes"] = Microsoft365Options.DefaultGraphScope,
                ["Microsoft365.GroupTypes"] = new[] { "Microsoft365", "Distribution" }
            });
            var configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

            try
            {
                var settings = SettingsFormModel.FromForm(
                    form,
                    new ContactMeshOptions
                    {
                        Rules = new SyncRuleOptions
                        {
                            ScopedGroupRoots = new[] { "department-root" }
                        }
                    },
                    new GoogleWorkspaceOptions(),
                    new Microsoft365Options { ClientSecret = "existing-secret" });

                await settings.SaveAsync(configPath, new FakeSecretProtector(), TestContext.Current.CancellationToken);
                var json = await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken);

                Assert.Contains("\"Provider\": \"Microsoft365\"", json);
                Assert.Contains("\"DisableDeletes\": true", json);
                Assert.Contains("\"ForceDeduplicatePhones\": true", json);
                Assert.Contains("\"ForceNormalizeEmailTypes\": true", json);
                Assert.Contains("\"From\": \"sender@example.org\"", json);
                Assert.Contains("\"success@example.org\"", json);
                Assert.Contains("\"failure@example.org\"", json);
                Assert.Contains("\"SubjectPrefix\": \"[Mesh]\"", json);
                Assert.Contains("\"MaxAttachmentBytes\": 2048", json);
                Assert.Contains("\"MainContactsGroupEmails\": [", json);
                Assert.Contains("\"company-directory@example.org\"", json);
                Assert.Contains("\"contractors@example.org\"", json);
                Assert.Contains("\"MainContactsGroupLabel\": \"-Directory\"", json);
                Assert.Contains("\"GroupContactPrefix\": \"#\"", json);
                Assert.Contains("\"ScopedGroupRoots\": [", json);
                Assert.Contains("\"department-root\"", json);
                Assert.Contains("\"target@example.org\"", json);
                Assert.Contains("\"contact-labels@example.org\"", json);
                Assert.Contains("\"From\": \"source@example.org\"", json);
                Assert.Contains("\"GroupTypes\": [", json);
                Assert.Contains("\"Microsoft365\"", json);
                Assert.Contains("\"Distribution\"", json);
                Assert.DoesNotContain("existing-secret", json);
                Assert.Contains("\"ClientSecret\": \"cmenc:v1:protected:6578697374696E672D736563726574\"", json);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public async Task SettingsFormModelSavesNewSecretEncrypted()
        {
            var form = new FormCollection(new Dictionary<string, StringValues>
            {
                ["Microsoft365.ClientSecret"] = "new-secret"
            });
            var configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

            try
            {
                var settings = SettingsFormModel.FromForm(
                    form,
                    new ContactMeshOptions(),
                    new GoogleWorkspaceOptions(),
                    new Microsoft365Options());

                await settings.SaveAsync(configPath, new FakeSecretProtector(), TestContext.Current.CancellationToken);
                var json = await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken);

                Assert.DoesNotContain("new-secret", json);
                Assert.Contains("\"ClientSecret\": \"cmenc:v1:protected:6E65772D736563726574\"", json);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void RenderDoesNotLeakProtectedSecretPayload()
        {
            var html = SettingsPageRenderer.Render(
                new ContactMeshOptions(),
                new GoogleWorkspaceOptions(),
                new Microsoft365Options
                {
                    ClientSecret = "cmenc:v1:protected:super-secret"
                },
                "appsettings.json",
                null);

            Assert.Contains("Configured", html);
            Assert.DoesNotContain("cmenc:v1", html);
            Assert.DoesNotContain("super-secret", html);
        }

        private sealed class FakeSecretProtector : ISecretProtector
        {
            public string Protect(string plaintext)
            {
                return "protected:" + Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(plaintext));
            }

            public string Unprotect(string protectedValue)
            {
                const string prefix = "protected:";
                if (!protectedValue.StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Invalid test payload.");
                }

                return System.Text.Encoding.UTF8.GetString(Convert.FromHexString(protectedValue[prefix.Length..]));
            }
        }
    }
}
