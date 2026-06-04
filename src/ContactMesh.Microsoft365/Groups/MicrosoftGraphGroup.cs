namespace ContactMesh.Microsoft365.Groups;

public enum MicrosoftGroupType
{
    Microsoft365,
    MailEnabledSecurity,
    Distribution
}

public sealed record MicrosoftGraphGroup
{
    public string? Id { get; init; }
    public string? Mail { get; init; }
    public string? DisplayName { get; init; }
    public string? Visibility { get; init; }
    public bool? MailEnabled { get; init; }
    public bool? SecurityEnabled { get; init; }
    public IReadOnlyList<string> GroupTypes { get; init; } = Array.Empty<string>();
}

public sealed record MicrosoftGraphGroupMember
{
    public string? Id { get; init; }
    public string? ODataType { get; init; }
    public string? Mail { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public string? CompanyName { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public IReadOnlyList<string> ProxyAddresses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BusinessPhones { get; init; } = Array.Empty<string>();
    public string? MobilePhone { get; init; }
}
