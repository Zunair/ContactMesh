// File: ContactEmailPolicyOptions.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Sync
{
    public sealed record ContactEmailPolicyOptions
    {
        public IReadOnlyList<string> ManagedEmailDomains { get; init; } = Array.Empty<string>();
        public bool ForceNormalizeEmailTypes { get; init; }
    }
}
