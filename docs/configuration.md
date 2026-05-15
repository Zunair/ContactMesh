# Configuration

ContactMesh uses one central `ContactMesh` section and provider-specific sections.

```json
{
  "ContactMesh": {
    "Provider": "Google",
    "DryRun": true,
    "ManagedEmailDomains": [],
    "Rules": {
      "TargetUsers": [],
      "GlobalUserGroups": [],
      "GlobalExternalContactGroups": [],
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

Keep secrets out of repository files. Use environment variables, user secrets, mounted files, or your hosting platform's secret store.
