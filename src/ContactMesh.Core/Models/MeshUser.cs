namespace ContactMesh.Core.Models;

public sealed record MeshUser
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? CompanyName { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public string? OrganizationUnit { get; init; }
    public IReadOnlyList<string> AlternateEmails { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ContactPhone> Phones { get; init; } = Array.Empty<ContactPhone>();
    public bool IsSuspended { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
