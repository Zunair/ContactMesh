// File: ISecretProtector.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Security
{
    public interface ISecretProtector
    {
        string Protect(string plaintext);

        string Unprotect(string protectedValue);
    }
}
