// File: ProtectedSecret.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Security
{
    public static class ProtectedSecret
    {
        public const string Prefix = "cmenc:v1:";

        public static bool IsProtected(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.StartsWith(Prefix, StringComparison.Ordinal);
        }

        public static string? ProtectIfNeeded(string? value, ISecretProtector secretProtector)
        {
            ArgumentNullException.ThrowIfNull(secretProtector);

            if (string.IsNullOrWhiteSpace(value) || IsProtected(value))
            {
                return value;
            }

            return Prefix + secretProtector.Protect(value);
        }

        public static string? UnprotectIfNeeded(string? value, ISecretProtector secretProtector)
        {
            ArgumentNullException.ThrowIfNull(secretProtector);

            if (!IsProtected(value))
            {
                return value;
            }

            try
            {
                return secretProtector.Unprotect(value![Prefix.Length..]);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "The configured secret could not be decrypted. It may have been encrypted for another user, machine, or Data Protection key ring. Re-enter the secret in the Web settings UI or restore the original key ring.",
                    ex);
            }
        }
    }
}
