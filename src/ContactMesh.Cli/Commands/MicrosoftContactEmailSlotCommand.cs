// File: MicrosoftContactEmailSlotCommand.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Microsoft365.Contacts;

namespace ContactMesh.Cli.Commands
{
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
            var diagnostic = options.ContactDiagnostic;
            var userId = FirstConfigured(GetValue(args, "--user"), diagnostic.User) ?? GetSingleTargetUser(contactMeshOptions);
            var contactEmails = GetValues(args, "--contact");
            if (contactEmails.Count == 0)
            {
                contactEmails = diagnostic.Contacts;
            }

            var contactIds = GetValues(args, "--contact-id");
            if (contactIds.Count == 0)
            {
                contactIds = diagnostic.ContactIds;
            }

            var workEmail = FirstConfigured(GetValue(args, "--work-email"), diagnostic.WorkEmail);
            var apply = args.Any(arg => string.Equals(arg, "--apply", StringComparison.OrdinalIgnoreCase)) || diagnostic.Apply;
            var betaEmailType = GetValue(args, "--beta-email-type");

            if (string.IsNullOrWhiteSpace(userId)
                || (contactEmails.Count == 0 && contactIds.Count == 0)
                || (contactIds.Count > 0 && string.IsNullOrWhiteSpace(workEmail)))
            {
                await output.WriteLineAsync(
                    $"Usage: {Name} [--user <mailbox>] (--contact <email-to-find> | --contact-id <graph-id>) [--work-email <email-to-write>] [--apply] [--beta-email-type <work|personal|other|main|unknown>]").ConfigureAwait(false);
                await output.WriteLineAsync("--user can be omitted when ContactMesh:Rules:TargetUsers contains exactly one mailbox.").ConfigureAwait(false);
                await output.WriteLineAsync("Command arguments can also be supplied with Microsoft365:ContactDiagnostic config.").ConfigureAwait(false);
                await output.WriteLineAsync("--work-email is required for --contact-id lookups, because the email cannot be inferred from the id.").ConfigureAwait(false);
                await output.WriteLineAsync("Without --apply, this command only reads and prints the matching contact.").ConfigureAwait(false);
                return 2;
            }

            using var httpClient = new HttpClient();
            var graphClientFactory = new MicrosoftGraphClientFactory(options);
            var accessTokenProvider = graphClientFactory.CreateAccessTokenProvider(httpClient);
            var contactClient = new MicrosoftGraphContactClient(httpClient, accessTokenProvider);
            var resetter = new MicrosoftContactEmailSlotResetter(contactClient);

            var exitCode = 0;

            foreach (var contactEmail in contactEmails)
            {
                var result = await resetter.ResetAsync(
                    userId,
                    contactEmail,
                    FirstConfigured(workEmail, contactEmail)!,
                    apply,
                    cancellationToken).ConfigureAwait(false);

                var resultCode = await WriteResultAsync(
                    output,
                    userId,
                    contactEmail,
                    null,
                    FirstConfigured(workEmail, contactEmail)!,
                    apply,
                    result).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(betaEmailType))
                {
                    resultCode = Math.Max(
                        resultCode,
                        await ApplyBetaEmailTypeAsync(
                            output,
                            contactClient,
                            userId,
                            result,
                            FirstConfigured(workEmail, contactEmail)!,
                            betaEmailType,
                            cancellationToken).ConfigureAwait(false));
                }

                exitCode = Math.Max(exitCode, resultCode);
            }

            foreach (var contactId in contactIds)
            {
                var result = await resetter.ResetByIdAsync(
                    userId,
                    contactId,
                    workEmail!,
                    apply,
                    cancellationToken).ConfigureAwait(false);

                var resultCode = await WriteResultAsync(
                    output,
                    userId,
                    null,
                    contactId,
                    workEmail!,
                    apply,
                    result).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(betaEmailType))
                {
                    resultCode = Math.Max(
                        resultCode,
                        await ApplyBetaEmailTypeAsync(
                            output,
                            contactClient,
                            userId,
                            result,
                            workEmail!,
                            betaEmailType,
                            cancellationToken).ConfigureAwait(false));
                }

                exitCode = Math.Max(exitCode, resultCode);
            }

            return exitCode;
        }

        private static async Task<int> WriteResultAsync(
            TextWriter output,
            string userId,
            string? contactEmail,
            string? contactId,
            string workEmail,
            bool apply,
            MicrosoftContactEmailSlotResetResult result)
        {
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

        private static async Task<int> ApplyBetaEmailTypeAsync(
            TextWriter output,
            MicrosoftGraphContactClient contactClient,
            string userId,
            MicrosoftContactEmailSlotResetResult result,
            string workEmail,
            string betaEmailType,
            CancellationToken cancellationToken)
        {
            if (result.Matches.Count != 1)
            {
                await output.WriteLineAsync("Skipping beta email type write because the lookup did not match exactly one contact.").ConfigureAwait(false);
                return result.Matches.Count == 0 ? 3 : 4;
            }

            var contact = result.Matches[0];
            if (string.IsNullOrWhiteSpace(contact.Id))
            {
                await output.WriteLineAsync("Skipping beta email type write because the matched contact has no Graph id.").ConfigureAwait(false);
                return 5;
            }

            await contactClient.UpdateEmailAddressesBetaAsync(
                userId,
                contact.Id,
                new[]
                {
                    new MicrosoftGraphEmailAddress(workEmail, contact.DisplayName, betaEmailType)
                },
                cancellationToken).ConfigureAwait(false);

            await output.WriteLineAsync($"Beta emailAddresses[] write sent with type={betaEmailType}.").ConfigureAwait(false);
            var betaContact = await contactClient.GetContactBetaAsync(userId, contact.Id, cancellationToken).ConfigureAwait(false);
            await WriteContactAsync(output, "Beta after", betaContact).ConfigureAwait(false);
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

        private static IReadOnlyList<string> GetValues(IReadOnlyList<string> args, string name)
        {
            var values = new List<string>();
            for (var i = 0; i < args.Count - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    values.Add(args[i + 1]);
                }
            }

            return values;
        }

        private static string? FirstConfigured(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
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
}
