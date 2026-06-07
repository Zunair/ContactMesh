namespace ContactMesh.Microsoft365.Contacts;

public sealed record MicrosoftGraphContact
{
    public string? Id { get; init; }
    public string? ChangeKey { get; init; }
    public string? ContactFolderId { get; init; }
    public string? ContactFolderDisplayName { get; init; }
    public string? SourceId { get; init; }
    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public string? CompanyName { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public MicrosoftGraphEmailAddress? PrimaryEmailAddress { get; init; }
    public MicrosoftGraphEmailAddress? SecondaryEmailAddress { get; init; }
    public MicrosoftGraphEmailAddress? TertiaryEmailAddress { get; init; }
    public IReadOnlyList<MicrosoftGraphEmailAddress> EmailAddresses { get; init; } = Array.Empty<MicrosoftGraphEmailAddress>();
    public IReadOnlyList<string> BusinessPhones { get; init; } = Array.Empty<string>();
    public string? MobilePhone { get; init; }
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public string? PersonalNotes { get; init; }
}

public sealed record MicrosoftGraphEmailAddress(string Address, string? Name = null, string? Type = null);
