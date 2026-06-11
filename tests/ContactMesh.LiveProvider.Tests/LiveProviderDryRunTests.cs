// File: LiveProviderDryRunTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Hosting;
using Xunit;

namespace ContactMesh.LiveProvider.Tests
{
    public sealed class LiveProviderDryRunTests
    {
        private const string EnabledVariable = "CONTACTMESH_LIVE_PROVIDER_TESTS";
        private const string ConfigPathVariable = "CONTACTMESH_LIVE_PROVIDER_CONFIG";

        [Fact]
        public async Task Configured_Provider_Completes_Scoped_Dry_Run()
        {
            if (!IsEnabled())
            {
                Assert.Skip($"{EnabledVariable}=1 is required for live provider dry-run validation.");
            }

            var configPath = GetRequiredEnvironmentVariable(ConfigPathVariable);
            Assert.True(File.Exists(configPath), $"{ConfigPathVariable} must point to an existing JSON config file.");

            using var app = ContactMeshAppHost.Build(new[] { configPath });
            var options = app.Options;

            Assert.True(options.DryRun, "Live provider tests must run with ContactMesh:DryRun=true.");
            Assert.NotEmpty(options.Rules.TargetUsers);

            var result = await app.Orchestrator.RunAsync(options, TestContext.Current.CancellationToken);

            Assert.True(result.DryRun);
            Assert.NotEqual(0, result.TargetCount);
            Assert.True(result.TargetCount <= options.Rules.TargetUsers.Count);
            Assert.False(result.HasErrors, string.Join(Environment.NewLine, result.Errors));
        }

        private static bool IsEnabled()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable(EnabledVariable),
                "1",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRequiredEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);

            Assert.False(string.IsNullOrWhiteSpace(value), $"{name} must be set when {EnabledVariable}=1.");

            return value;
        }
    }
}
