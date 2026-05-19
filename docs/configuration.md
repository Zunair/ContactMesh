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
      "MainContactsGroupEmail": "",
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

`GroupsToSyncByGroup` is the label-container setting. Put one or more container group emails or ids there; each direct subgroup becomes a managed group contact and the subgroup display name becomes the contact label for members of that subgroup, including nested members. Ordinary visible groups affect who can see contacts, but they do not create labels.

Keep secrets out of repository files. Use environment variables, user secrets, mounted files, or your hosting platform's secret store.
