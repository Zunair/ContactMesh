// File: ContactMeshConfigurationTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;
using ContactMesh.Core.Security;
using ContactMesh.Google.Auth;
using ContactMesh.Hosting;
using ContactMesh.Hosting.Security;
using ContactMesh.Microsoft365.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ContactMesh.Hosting.Tests
{
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
                ["ContactMesh:ForceDeduplicatePhones"] = "true",
                ["ContactMesh:ForceNormalizeEmailTypes"] = "true",
                ["ContactMesh:ManagedEmailDomains:0"] = "example.org",
                ["ContactMesh:Notifications:Enabled"] = "true",
                ["ContactMesh:Notifications:From"] = "sender@example.org",
                ["ContactMesh:Notifications:SuccessTo:0"] = "success@example.org",
                ["ContactMesh:Notifications:FailureTo:0"] = "failure@example.org",
                ["ContactMesh:Notifications:SubjectPrefix"] = "[Mesh]",
                ["ContactMesh:Notifications:AttachCsvOnFailure"] = "true",
                ["ContactMesh:Notifications:MaxAttachmentBytes"] = "2048",
                ["ContactMesh:Rules:TargetUsers:0"] = "target@example.org",
                ["ContactMesh:Rules:MainContactsGroupEmails:0"] = "company-directory@example.org",
                ["ContactMesh:Rules:MainContactsGroupEmails:1"] = "contractors@example.org",
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
                ["Microsoft365:Scopes:0"] = "https://graph.microsoft.com/.default",
                ["Microsoft365:ContactDiagnostic:User"] = "target@example.org",
                ["Microsoft365:ContactDiagnostic:Contacts:0"] = "contact@example.org",
                ["Microsoft365:ContactDiagnostic:ContactIds:0"] = "contact-id",
                ["Microsoft365:ContactDiagnostic:WorkEmail"] = "work@example.org",
                ["Microsoft365:ContactDiagnostic:Apply"] = "true"
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
            Assert.True(contactMesh.ForceDeduplicatePhones);
            Assert.True(contactMesh.ForceNormalizeEmailTypes);
            Assert.Equal("example.org", Assert.Single(contactMesh.ManagedEmailDomains));
            Assert.True(contactMesh.Notifications.Enabled);
            Assert.Equal("sender@example.org", contactMesh.Notifications.From);
            Assert.Equal("success@example.org", Assert.Single(contactMesh.Notifications.SuccessTo));
            Assert.Equal("failure@example.org", Assert.Single(contactMesh.Notifications.FailureTo));
            Assert.Equal("[Mesh]", contactMesh.Notifications.SubjectPrefix);
            Assert.True(contactMesh.Notifications.AttachCsvOnFailure);
            Assert.Equal(2048, contactMesh.Notifications.MaxAttachmentBytes);
            Assert.Equal("target@example.org", Assert.Single(contactMesh.Rules.TargetUsers));
            Assert.Equal(new[] { "company-directory@example.org", "contractors@example.org" }, contactMesh.Rules.MainContactsGroupEmails);
            Assert.Equal("-Directory", contactMesh.Rules.MainContactsGroupLabel);
            Assert.Equal("#", contactMesh.Rules.GroupContactPrefix);
            Assert.Equal("all-users", Assert.Single(contactMesh.Rules.GlobalUserGroups));
            Assert.Equal("contact-labels@example.org", Assert.Single(contactMesh.Rules.GroupsToSyncByGroup));
            Assert.Equal("source-group", Assert.Single(contactMesh.Rules.GroupMappings).From);
            Assert.Equal("target-group", Assert.Single(contactMesh.Rules.GroupMappings).To);

            Assert.Equal("all-users", Assert.Single(rules.GlobalUserGroups));
            Assert.Equal(new[] { "company-directory@example.org", "contractors@example.org" }, rules.MainContactsGroupEmails);
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
            Assert.Equal("target@example.org", microsoft365.ContactDiagnostic.User);
            Assert.Equal("contact@example.org", Assert.Single(microsoft365.ContactDiagnostic.Contacts));
            Assert.Equal("contact-id", Assert.Single(microsoft365.ContactDiagnostic.ContactIds));
            Assert.Equal("work@example.org", microsoft365.ContactDiagnostic.WorkEmail);
            Assert.True(microsoft365.ContactDiagnostic.Apply);
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

        [Fact]
        public void AddContactMeshOptions_Decrypts_Protected_ClientSecret()
        {
            var values = new Dictionary<string, string?>
            {
                ["Microsoft365:ClientSecret"] = "cmenc:v1:protected:client-secret"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<ISecretProtector, FakeSecretProtector>();
            using var serviceProvider = services
                .AddContactMeshOptions(configuration)
                .BuildServiceProvider();

            var microsoft365 = serviceProvider.GetRequiredService<IOptions<Microsoft365Options>>().Value;

            Assert.Equal("client-secret", microsoft365.ClientSecret);
        }

        [Fact]
        public void AddContactMeshOptions_Leaves_Plaintext_ClientSecret_Unchanged()
        {
            var values = new Dictionary<string, string?>
            {
                ["Microsoft365:ClientSecret"] = "client-secret"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            using var serviceProvider = new ServiceCollection()
                .AddContactMeshOptions(configuration)
                .BuildServiceProvider();

            var microsoft365 = serviceProvider.GetRequiredService<IOptions<Microsoft365Options>>().Value;

            Assert.Equal("client-secret", microsoft365.ClientSecret);
        }

        [Fact]
        public void AddContactMeshOptions_Reports_Clear_Error_For_Invalid_Protected_ClientSecret()
        {
            var values = new Dictionary<string, string?>
            {
                ["Microsoft365:ClientSecret"] = "cmenc:v1:not-valid"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<ISecretProtector, ThrowingSecretProtector>();
            using var serviceProvider = services
                .AddContactMeshOptions(configuration)
                .BuildServiceProvider();

            var exception = Assert.Throws<InvalidOperationException>(
                () => serviceProvider.GetRequiredService<IOptions<Microsoft365Options>>().Value);

            Assert.Contains("encrypted for another user, machine, or Data Protection key ring", exception.Message);
        }

        [Fact]
        public void DataProtectionSecretProtector_RoundTrips_Secret()
        {
            var keyDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                var dataProtectionProvider = DataProtectionProvider.Create(new DirectoryInfo(keyDirectory));
                var protector = new DataProtectionSecretProtector(dataProtectionProvider);

                var protectedValue = protector.Protect("client-secret");
                var plaintext = protector.Unprotect(protectedValue);

                Assert.NotEqual("client-secret", protectedValue);
                Assert.Equal("client-secret", plaintext);
            }
            finally
            {
                if (Directory.Exists(keyDirectory))
                {
                    Directory.Delete(keyDirectory, recursive: true);
                }
            }
        }

        private sealed class FakeSecretProtector : ISecretProtector
        {
            public string Protect(string plaintext)
            {
                return "protected:" + plaintext;
            }

            public string Unprotect(string protectedValue)
            {
                const string prefix = "protected:";
                if (!protectedValue.StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Invalid test payload.");
                }

                return protectedValue[prefix.Length..];
            }
        }

        private sealed class ThrowingSecretProtector : ISecretProtector
        {
            public string Protect(string plaintext)
            {
                return plaintext;
            }

            public string Unprotect(string protectedValue)
            {
                throw new InvalidOperationException("Cannot decrypt.");
            }
        }
    }
}
