// File: CoreBoundaryTests.cs
// Author: Zunair
// Producer: Copilot

using Xunit;

namespace ContactMesh.Core.Tests
{
    public sealed class CoreBoundaryTests
    {
        [Fact]
        public void Core_Project_Does_Not_Reference_Provider_Projects_Or_Sdks()
        {
            var root = FindRepositoryRoot();
            var projectFile = Path.Combine(root, "src", "ContactMesh.Core", "ContactMesh.Core.csproj");
            var projectXml = File.ReadAllText(projectFile);

            Assert.DoesNotContain("ContactMesh.Google", projectXml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ContactMesh.Microsoft365", projectXml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Google.Apis", projectXml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Microsoft.Graph", projectXml, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ContactMesh.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find ContactMesh.sln.");
        }
    }
}
