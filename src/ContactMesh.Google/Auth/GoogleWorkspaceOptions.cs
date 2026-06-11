// File: GoogleWorkspaceOptions.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Google.Auth
{
    public sealed record GoogleWorkspaceOptions
    {
        public const string SectionName = "GoogleWorkspace";
        public const string PeopleContactsScope = "https://www.googleapis.com/auth/contacts";

        public static readonly IReadOnlyList<string> DefaultPeopleApiScopes = new[]
        {
            PeopleContactsScope
        };

        public string ServiceAccountFile { get; init; } = "client_secret_svc.json";
        public string? AdminUserEmail { get; init; }
        public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
    }
}
