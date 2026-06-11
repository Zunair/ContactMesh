// File: GooglePersonContact.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Google.Contacts
{
    public sealed record GooglePersonContact
    {
        public string? ResourceName { get; init; }
        public string? ETag { get; init; }
        public string? SourceId { get; init; }
        public string? DisplayName { get; init; }
        public string? GivenName { get; init; }
        public string? FamilyName { get; init; }
        public string? CompanyName { get; init; }
        public string? Department { get; init; }
        public string? JobTitle { get; init; }
        public IReadOnlyList<GooglePersonEmail> Emails { get; init; } = Array.Empty<GooglePersonEmail>();
        public IReadOnlyList<GooglePersonPhone> Phones { get; init; } = Array.Empty<GooglePersonPhone>();
        public IReadOnlyList<string> ContactGroupResourceNames { get; init; } = Array.Empty<string>();
    }

    public sealed record GooglePersonEmail(string Address, string Type = "work", bool IsPrimary = false);

    public sealed record GooglePersonPhone(string Number, string Type = "work", bool IsPrimary = false);
}
