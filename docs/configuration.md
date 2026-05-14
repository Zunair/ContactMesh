# Configuration

ContactMesh uses one central `ContactMesh` section and provider-specific sections.

```json
{
  "ContactMesh": {
    "Provider": "Google",
    "DryRun": true,
    "Rules": {
      "GlobalUserGroups": [],
      "GlobalExternalContactGroups": [],
      "ExclusionGroups": [],
      "ScopedGroupRoots": [],
      "GroupMappings": []
    }
  }
}
```

Typed options:

- `ContactMeshOptions`
- `GoogleWorkspaceOptions`
- `Microsoft365Options`
- `SyncRuleOptions`

Keep secrets out of repository files. Use environment variables, user secrets, mounted files, or your hosting platform's secret store.
