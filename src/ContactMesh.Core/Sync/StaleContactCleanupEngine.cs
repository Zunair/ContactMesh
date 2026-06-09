using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class StaleContactCleanupEngine
{
    private const int MaxDetailLength = 120;

    private readonly StaleContactCleanupOptions options;

    public StaleContactCleanupEngine(StaleContactCleanupOptions? options = null)
    {
        this.options = options ?? new StaleContactCleanupOptions();
    }

    public StaleContactCleanupResult Clean(MeshContact contact)
    {
        var cleaned = contact with
        {
            SourceId = null,
            Notes = IsManagedPhoneNote(contact.Notes) ? null : contact.Notes,
            Emails = contact.Emails.Where(email => !IsManagedEmail(email)).ToList(),
            Phones = contact.Phones.Where(phone => !IsManagedPhone(phone)).ToList(),
            Labels = this.options.ForceResetLabels
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : contact.Labels
                    .Where(label => !this.options.ManagedLabels.Contains(label))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
            Metadata = contact.Metadata
                .Where(item => this.options.OperationalMetadataKeys.Contains(item.Key)
                               || !this.options.ManagedMetadataKeys.Contains(item.Key))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
        };

        return HasUserOwnedData(cleaned)
            ? new StaleContactCleanupResult
            {
                ShouldDelete = false,
                Contact = cleaned,
                Reason = $"Managed contact is stale; preserving user-owned details ({DescribeUserOwnedData(cleaned)}) and removing managed fields."
            }
            : new StaleContactCleanupResult
            {
                ShouldDelete = true,
                Contact = contact,
                Reason = "Managed contact is stale and has no user-owned details."
            };
    }

    public bool HasManagedEmail(MeshContact contact)
    {
        return contact.Emails.Any(IsManagedEmail);
    }

    public bool HasManagedMarker(MeshContact contact)
    {
        return this.HasManagedEmail(contact)
            || contact.Labels.Any(label => this.options.ManagedLabels.Contains(label))
            || contact.Metadata.Keys.Any(key => this.options.ManagedMarkerMetadataKeys.Contains(key));
    }

    private bool IsManagedEmail(ContactEmail email)
    {
        return this.options.ManagedEmailDomains.Any(domain =>
            email.Address.EndsWith(NormalizeDomain(domain), StringComparison.OrdinalIgnoreCase));
    }

    private bool IsManagedPhone(ContactPhone phone)
    {
        return this.options.ManagedPhoneTypes.Contains(phone.Type);
    }

    private bool IsManagedPhoneNote(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return false;
        }

        var lines = notes
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2 || !int.TryParse(lines[0], out var declaredCount) || declaredCount != lines.Length - 1)
        {
            return false;
        }

        return lines
            .Skip(1)
            .All(line =>
            {
                var separator = line.IndexOf(':', StringComparison.Ordinal);
                if (separator <= 0 || separator == line.Length - 1)
                {
                    return false;
                }

                var type = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();

                return value.Length > 0 && this.options.ManagedPhoneTypes.Contains(type);
            });
    }

    private bool HasUserOwnedData(MeshContact contact)
    {
        return contact.Emails.Count > 0
            || contact.Phones.Count > 0
            || contact.Labels.Count > 0
            || contact.Metadata.Any(item => !this.options.OperationalMetadataKeys.Contains(item.Key))
            || !string.IsNullOrWhiteSpace(contact.Notes);
    }

    private string DescribeUserOwnedData(MeshContact contact)
    {
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(contact.Notes))
        {
            details.Add($"notes={FormatValue(contact.Notes)}");
        }

        if (contact.Emails.Count > 0)
        {
            details.Add($"emails={FormatList(contact.Emails.Select(email => email.Address))}");
        }

        if (contact.Phones.Count > 0)
        {
            details.Add($"phones={FormatList(contact.Phones.Select(phone => phone.Number))}");
        }

        if (contact.Labels.Count > 0)
        {
            details.Add($"labels={FormatList(contact.Labels.Order(StringComparer.OrdinalIgnoreCase))}");
        }

        var userMetadata = contact.Metadata
            .Where(item => !this.options.OperationalMetadataKeys.Contains(item.Key))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Key}={item.Value}")
            .ToList();

        if (userMetadata.Count > 0)
        {
            details.Add($"metadata={FormatList(userMetadata)}");
        }

        return string.Join(", ", details);
    }

    private static string FormatList(IEnumerable<string> values)
    {
        return $"[{string.Join("; ", values.Select(FormatValue))}]";
    }

    private static string FormatValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(blank)";
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();

        return normalized.Length <= MaxDetailLength
            ? normalized
            : $"{normalized[..MaxDetailLength]}...";
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.StartsWith('@') ? domain : $"@{domain}";
    }
}
