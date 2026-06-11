// File: MeshContact.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Models
{
    public sealed record MeshContact
    {
        public string? SourceId { get; init; }
        public string? DisplayName { get; init; }
        public string? GivenName { get; init; }
        public string? FamilyName { get; init; }
        public string? CompanyName { get; init; }
        public string? Department { get; init; }
        public string? JobTitle { get; init; }
        public string? Notes { get; init; }
        public IReadOnlyList<ContactEmail> Emails { get; init; } = Array.Empty<ContactEmail>();
        public IReadOnlyList<string> MatchEmails { get; init; } = Array.Empty<string>();
        public IReadOnlyList<ContactPhone> Phones { get; init; } = Array.Empty<ContactPhone>();
        public IReadOnlySet<string> Labels { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record ContactEmail(string Address, string Type = "work", bool IsPrimary = false);

    public sealed record ContactPhone(string Number, string Type = "work", bool IsPrimary = false);
}
