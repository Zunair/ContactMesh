namespace ContactMesh.Microsoft365.Contacts;

public sealed class MicrosoftContactEmailSlotResetter
{
    private readonly IMicrosoftGraphContactClient client;

    public MicrosoftContactEmailSlotResetter(IMicrosoftGraphContactClient client)
    {
        this.client = client;
    }

    public async Task<MicrosoftContactEmailSlotResetResult> ResetAsync(
        string userId,
        string contactEmail,
        string workEmail,
        bool apply,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(workEmail);

        var before = await this.client.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        var matches = before
            .Where(contact => HasEmail(contact, contactEmail))
            .ToList();

        return await this.ResetMatchesAsync(
            userId,
            matches,
            workEmail,
            apply,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<MicrosoftContactEmailSlotResetResult> ResetByIdAsync(
        string userId,
        string contactId,
        string workEmail,
        bool apply,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workEmail);

        var before = await this.client.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        var matches = before
            .Where(contact => string.Equals(contact.Id, contactId.Trim(), StringComparison.Ordinal))
            .ToList();

        return await this.ResetMatchesAsync(
            userId,
            matches,
            workEmail,
            apply,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<MicrosoftContactEmailSlotResetResult> ResetMatchesAsync(
        string userId,
        IReadOnlyList<MicrosoftGraphContact> matches,
        string workEmail,
        bool apply,
        CancellationToken cancellationToken)
    {
        if (matches.Count != 1)
        {
            return new MicrosoftContactEmailSlotResetResult(matches, Array.Empty<MicrosoftGraphContact>(), apply, false);
        }

        if (!apply)
        {
            return new MicrosoftContactEmailSlotResetResult(matches, Array.Empty<MicrosoftGraphContact>(), apply, false);
        }

        var contact = matches[0];
        var cleared = SetEmailSlots(contact, null);
        await this.client.UpdateAsync(userId, cleared, cancellationToken).ConfigureAwait(false);

        var updated = SetEmailSlots(contact, new MicrosoftGraphEmailAddress(workEmail.Trim(), contact.DisplayName, "work"));
        await this.client.UpdateAsync(userId, updated, cancellationToken).ConfigureAwait(false);

        var after = await this.client.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        var updatedContacts = after
            .Where(current => string.Equals(current.Id, contact.Id, StringComparison.Ordinal))
            .ToList();

        return new MicrosoftContactEmailSlotResetResult(matches, updatedContacts, apply, true);
    }

    private static MicrosoftGraphContact SetEmailSlots(
        MicrosoftGraphContact contact,
        MicrosoftGraphEmailAddress? primaryEmailAddress)
    {
        return contact with
        {
            PrimaryEmailAddress = primaryEmailAddress,
            SecondaryEmailAddress = null,
            TertiaryEmailAddress = null,
            EmailAddresses = Array.Empty<MicrosoftGraphEmailAddress>()
        };
    }

    private static bool HasEmail(MicrosoftGraphContact contact, string email)
    {
        return AllEmails(contact)
            .Any(address => string.Equals(address, email.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> AllEmails(MicrosoftGraphContact contact)
    {
        foreach (var email in new[]
        {
            contact.PrimaryEmailAddress,
            contact.SecondaryEmailAddress,
            contact.TertiaryEmailAddress
        })
        {
            if (!string.IsNullOrWhiteSpace(email?.Address))
            {
                yield return email.Address.Trim();
            }
        }

        foreach (var email in contact.EmailAddresses)
        {
            if (!string.IsNullOrWhiteSpace(email.Address))
            {
                yield return email.Address.Trim();
            }
        }
    }
}

public sealed record MicrosoftContactEmailSlotResetResult(
    IReadOnlyList<MicrosoftGraphContact> Matches,
    IReadOnlyList<MicrosoftGraphContact> UpdatedContacts,
    bool Apply,
    bool Updated);
