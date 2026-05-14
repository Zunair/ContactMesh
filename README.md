# ContactMesh

ContactMesh syncs directory users, groups, and shared contacts into each user's personal contacts using policy-based rules. It is designed for organizations that need offline mobile contacts, caller ID, and group-aware contact distribution across Google Workspace and Microsoft 365.

## Status

ContactMesh is being extracted from a working Google Workspace utility into a provider-neutral .NET codebase. The current solution includes the clean core, provider contracts, Google/Microsoft provider shells, CLI/Worker/Web shells, documentation, and starter tests.

The legacy Google implementation is preserved under `tools/migration/` as reference material while the modern provider is wired up.

## Repository Layout

```text
src/ContactMesh.Core/          Provider-neutral models, rules, merge, and sync logic
src/ContactMesh.Google/        Google Workspace provider boundary
src/ContactMesh.Microsoft365/  Microsoft Graph provider boundary
src/ContactMesh.Cli/           Console runner
src/ContactMesh.Worker/        Scheduled sync host
src/ContactMesh.Web/           Future settings UI
tests/                         Unit tests
docs/                          Architecture, setup, sync rules, and roadmap docs
samples/                       Sample configuration and Docker compose files
tools/migration/               Legacy Google code retained for migration
```

## Design Rule

`ContactMesh.Core` must not reference Google Workspace, Microsoft 365, or any provider SDK. Providers translate their APIs into core abstractions:

- `IDirectoryProvider`
- `IContactProvider`
- `IGroupProvider`
- `ISyncStateStore`
- `IContactNormalizer`

## Build

```powershell
dotnet build ContactMesh.sln
dotnet test ContactMesh.sln
```

## Configuration

Start from `samples/google/appsettings.sample.json`. The central config shape is:

```json
{
  "ContactMesh": {
    "Provider": "Google",
    "DryRun": true,
    "Rules": {
      "GlobalUserGroups": [],
      "GlobalExternalContactGroups": [],
      "ExclusionGroups": [],
      "ScopedGroupRoots": []
    }
  }
}
```

Keep `DryRun` enabled until the generated plan has been reviewed.

## Roadmap

1. Clean Google app
2. Extract provider-neutral core
3. Add dry-run, logging, and tests
4. Add config UI
5. Add Microsoft Graph provider
6. Add Docker and hosted worker mode

## Security

Never commit service account keys, OAuth client secrets, tenant IDs, exported contacts, or organization-specific group names. See `SECURITY.md`.
