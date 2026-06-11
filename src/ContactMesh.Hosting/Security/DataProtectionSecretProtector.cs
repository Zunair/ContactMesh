// File: DataProtectionSecretProtector.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Security;
using Microsoft.AspNetCore.DataProtection;

namespace ContactMesh.Hosting.Security
{
    public sealed class DataProtectionSecretProtector : ISecretProtector
    {
        public const string Purpose = "ContactMesh.ConfigurationSecrets.v1";

        private readonly IDataProtector protector;

        public DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
        {
            ArgumentNullException.ThrowIfNull(dataProtectionProvider);
            this.protector = dataProtectionProvider.CreateProtector(Purpose);
        }

        public string Protect(string plaintext)
        {
            return this.protector.Protect(plaintext);
        }

        public string Unprotect(string protectedValue)
        {
            return this.protector.Unprotect(protectedValue);
        }
    }
}
