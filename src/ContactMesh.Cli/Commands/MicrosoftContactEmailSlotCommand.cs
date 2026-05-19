using ContactMesh.Core.Models;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Microsoft365.Contacts;

namespace ContactMesh.Cli.Commands;

public sealed class MicrosoftContactEmailSlotCommand
{
    public const string Name = "m365-contact-email-slot";

    public async Task<int> RunAsync(
        string[] args,
        ContactMeshOptions contactMeshOptions,
        Microsoft365Options options,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var userId = GetValue(args, "--user") ?? GetSingleTargetUser(contactMeshOptions);
        var contactEmail = GetValue(args, "--contact");
        var contactId = GetValue(args, "--contact-id");
        var workEmail = GetValue(args, "--work-email") ?? contactEmail;
        var apply = args.Any(arg => string.Equals(arg, "--apply", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(userId)
            || (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactId))
            || string.IsNullOrWhiteSpace(workEmail))
        {
            await output.WriteLineAsync(
                $"Usage: {Name} [--user <mailbox>] (--contact <email-to-find> | --contact-id <graph-id>) [--work-email <email-to-write>] [--apply]").ConfigureAwait(false);
            await output.WriteLineAsync("--user can be omitted when ContactMesh:Rules:TargetUsers contains exactly one mailbox.").ConfigureAwait(false);
            await output.WriteLineAsync("Without --apply, this command only reads and prints the matching contact.").ConfigureAwait(false);
            return 2;
        }

        using var httpClient = new HttpClient();
        var graphClientFactory = new MicrosoftGraphClientFactory(options);
        var accessTokenProvider = graphClientFactory.CreateAccessTokenProvider(httpClient);
        var contactClient = new MicrosoftGraphContactClient(httpClient, accessTokenProvider);
        var resetter = new MicrosoftContactEmailSlotResetter(contactClient);

        var result = string.IsNullOrWhiteSpace(contactId)
            ? await resetter.ResetAsync(
                userId,
                contactEmail!,
                workEmail,
                apply,
                cancellationToken).ConfigureAwait(false)
            : await resetter.ResetByIdAsync(
                userId,
                contactId,
                workEmail,
                apply,
                cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync($"Mailbox: {userId}").ConfigureAwait(false);
        await output.WriteLineAsync($"Contact lookup email: {contactEmail ?? "(not used)"}").ConfigureAwait(false);
        await output.WriteLineAsync($"Contact lookup id: {contactId ?? "(not used)"}").ConfigureAwait(false);
        await output.WriteLineAsync($"Work email to write: {workEmail}").ConfigureAwait(false);
        await output.WriteLineAsync($"Apply: {apply}").ConfigureAwait(false);
        await output.WriteLineAsync($"Matches: {result.Matches.Count}").ConfigureAwait(false);

        foreach (var contact in result.Matches)
        {
            await WriteContactAsync(output, "Before", contact).ConfigureAwait(false);
        }

        if (result.Matches.Count != 1)
        {
            await output.WriteLineAsync("Refusing to update because the lookup did not match exactly one contact.").ConfigureAwait(false);
            return result.Matches.Count == 0 ? 3 : 4;
        }

        if (!apply)
        {
            await output.WriteLineAsync("Dry read only. Add --apply to clear all email slots, then write one primary work email.").ConfigureAwait(false);
            return 0;
        }

        await output.WriteLineAsync(result.Updated ? "Update sent: cleared all email slots, then wrote one primary work email." : "Update not sent.").ConfigureAwait(false);

        foreach (var contact in result.UpdatedContacts)
        {
            await WriteContactAsync(output, "After", contact).ConfigureAwait(false);
        }

        return 0;
    }

    private static string? GetValue(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string? GetSingleTargetUser(ContactMeshOptions options)
    {
        return options.Rules.TargetUsers.Count == 1
            ? options.Rules.TargetUsers[0]
            : null;
    }

    private static async Task WriteContactAsync(TextWriter output, string label, MicrosoftGraphContact contact)
    {
        await output.WriteLineAsync($"{label}: {contact.DisplayName ?? "(no display name)"} [{contact.Id ?? "(no id)"}]").ConfigureAwait(false);
        await WriteEmailAsync(output, "  primaryEmailAddress", contact.PrimaryEmailAddress).ConfigureAwait(false);
        await WriteEmailAsync(output, "  secondaryEmailAddress", contact.SecondaryEmailAddress).ConfigureAwait(false);
        await WriteEmailAsync(output, "  tertiaryEmailAddress", contact.TertiaryEmailAddress).ConfigureAwait(false);

        if (contact.EmailAddresses.Count == 0)
        {
            await output.WriteLineAsync("  emailAddresses[]: (empty)").ConfigureAwait(false);
            return;
        }

        foreach (var email in contact.EmailAddresses)
        {
            await WriteEmailAsync(output, "  emailAddresses[]", email).ConfigureAwait(false);
        }
    }

    private static Task WriteEmailAsync(TextWriter output, string label, MicrosoftGraphEmailAddress? email)
    {
        var value = email is null || string.IsNullOrWhiteSpace(email.Address)
            ? "(null)"
            : $"{email.Address} type={email.Type ?? "(none)"} name={email.Name ?? "(none)"}";

        return output.WriteLineAsync($"{label}: {value}");
    }
}
