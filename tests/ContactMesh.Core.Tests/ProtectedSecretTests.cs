// File: ProtectedSecretTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Security;
using Xunit;

namespace ContactMesh.Core.Tests
{
    public sealed class ProtectedSecretTests
    {
        [Fact]
        public void IsProtected_Identifies_Prefixed_Value()
        {
            Assert.True(ProtectedSecret.IsProtected("cmenc:v1:payload"));
            Assert.False(ProtectedSecret.IsProtected("plain-secret"));
            Assert.False(ProtectedSecret.IsProtected(string.Empty));
        }

        [Fact]
        public void ProtectIfNeeded_Does_Not_Double_Protect()
        {
            var protector = new FakeSecretProtector();
            var protectedValue = ProtectedSecret.ProtectIfNeeded("plain-secret", protector);
            var protectedAgain = ProtectedSecret.ProtectIfNeeded(protectedValue, protector);

            Assert.Equal("cmenc:v1:protected:plain-secret", protectedValue);
            Assert.Equal(protectedValue, protectedAgain);
            Assert.Equal(1, protector.ProtectCount);
        }

        [Fact]
        public void UnprotectIfNeeded_Leaves_Plaintext_Untouched_And_Unwraps_Protected_Values()
        {
            var protector = new FakeSecretProtector();

            Assert.Equal("plain-secret", ProtectedSecret.UnprotectIfNeeded("plain-secret", protector));
            Assert.Equal("plain-secret", ProtectedSecret.UnprotectIfNeeded("cmenc:v1:protected:plain-secret", protector));
        }

        private sealed class FakeSecretProtector : ISecretProtector
        {
            public int ProtectCount { get; private set; }

            public string Protect(string plaintext)
            {
                this.ProtectCount++;
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
    }
}
