// File: MicrosoftGraphTokenRequest.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Microsoft365.Auth
{
    public sealed record MicrosoftGraphTokenRequest(
        string TenantId,
        string ClientId,
        string ClientSecret,
        IReadOnlyList<string> Scopes);
}
