# Configuration

ContactMesh uses one central `ContactMesh` section and provider-specific sections.

```json
{
  "ContactMesh": {
    "Provider": "Google",
    "DryRun": true,
    "DisableDeletes": false,
    "ForceResetLabels": false,
    "ForceDeduplicatePhones": false,
    "ForceNormalizeEmailTypes": false,
    "ManagedEmailDomains": [],
    "Rules": {
      "TargetUsers": [],
      "MainContactsGroupEmails": [],
      "MainContactsGroupLabel": "-Directory",
      "GroupContactPrefix": "+",
      "GlobalUserGroups": [],
      "GlobalExternalContactGroups": [],
      "GroupsToSyncByGroup": [],
      "ExclusionGroups": [],
      "ScopedGroupRoots": [],
      "GroupMappings": [],
      "IncludedOrganizationUnits": [],
      "ExcludedOrganizationUnits": []
    }
  }
}
```

Typed options:

- `ContactMeshOptions`
- `GoogleWorkspaceOptions`
- `Microsoft365Options`
- `SyncRuleOptions`

The CLI, Worker, and Web hosts bind these sections through the .NET configuration host. JSON files,
environment variables, command-line configuration, user secrets, and hosted secret stores can all feed
the same typed options. Environment variables and command-line values override JSON file values.
`SyncRuleOptions` is bound from `ContactMesh:Rules`.

Set `ContactMesh:DisableDeletes` to `true` to keep delete operations visible in dry-run and run reports while preventing live provider delete writes. Create and update writes still run when `DryRun` is `false`.

Set `ContactMesh:ForceResetLabels` to `true` to completely replace all labels on existing managed contacts rather than reconciling. Use this once after a sync bug has written stale labels, then set it back to `false`. When `false` (the default), only declared managed labels are replaced and user-owned labels are preserved.

Set `ContactMesh:ForceDeduplicatePhones` to `true` for a one-time legacy cleanup when managed contacts have the same phone number written into multiple phone fields, such as both mobile and work. Review the dry-run, run once with writes enabled, then set it back to `false` so future syncs keep the provider's current phone fields.

Set `ContactMesh:ForceNormalizeEmailTypes` to `true` for a one-time legacy cleanup when organization email addresses show under Other email. Review the dry-run, run once with writes enabled, then set it back to `false`.

For focused Microsoft 365 contact diagnostics, configure `Microsoft365:ContactDiagnostic` and run `m365-contact-email-slot`. `User` is the target mailbox whose Contacts folder should be inspected, `Contacts` are contact email addresses to find, `ContactIds` are Graph contact ids to inspect directly, `WorkEmail` is required when updating by id, and `Apply` controls whether the command performs the two-step email-slot reset. Leave `Apply` as `false` for a report-only dry run.
The command also accepts `--beta-email-type <type>` for testing Microsoft Graph beta typed email writes against one matched contact. Keep this as a diagnostic path unless the beta API behavior is intentionally adopted.

`GroupsToSyncByGroup` is the label-container setting. Put one or more container group emails or ids there; each direct subgroup becomes a managed group contact and the subgroup display name becomes the contact label for members of that subgroup, including nested members. Group-derived contact labels use `GroupContactPrefix`, so `MHP Locations` becomes `+MHP Locations` with the default prefix; `MainContactsGroupLabel` is left as configured. Ordinary visible groups affect who can see contacts, but they do not create labels.

Keep secrets out of repository files. Use environment variables, user secrets, mounted files, or your hosting platform's secret store.

When the Web settings page saves `Microsoft365:ClientSecret`, it writes the value in protected form with a `cmenc:v1:` prefix. The hosts decrypt that value after configuration binding, so CLI, Worker, Web test email, and Graph auth still receive the normal plaintext secret at runtime. Plaintext secrets from existing JSON files, environment variables, and command-line overrides remain supported; they are encrypted the next time the Web settings page saves the JSON file.

The protected value is intended for the local app/user key ring. If the JSON config is moved to another machine, container, Windows user, or service account without the same ASP.NET Core Data Protection key ring, ContactMesh cannot decrypt it. Re-enter the client secret from the Web settings page in that environment, or restore the original key ring.

## Run audit logs and email notifications

`ContactMesh:AuditLog` writes a per-run detail and summary CSV to disk for every sync run. `ContactMesh:Notifications` sends a success or failure summary email (Microsoft 365 only) after each non-dry-run. See [audit-and-notifications.md](audit-and-notifications.md) for the full schema and options reference.
