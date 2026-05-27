namespace ContactMesh.Microsoft365.Directory;

public sealed record MicrosoftGraphUser
{
    public string? Id { get; init; }
    public string? Mail { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public string? CompanyName { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public IReadOnlyList<string> BusinessPhones { get; init; } = Array.Empty<string>();
    public string? MobilePhone { get; init; }
    public bool? AccountEnabled { get; init; }
    public string? UserType { get; init; }
}
