# Live Provider Tests

The live-provider test project is an opt-in dry-run harness for validating a configured provider against real Google Workspace or Microsoft 365 APIs. It is skipped by default by returning before any host or provider is created.

Run the normal suite without live credentials:

```powershell
dotnet test ContactMesh.sln -m:1 --no-build
```

Run the live dry-run harness only from a machine or CI job with secret injection:

```powershell
$env:CONTACTMESH_LIVE_PROVIDER_TESTS = "1"
$env:CONTACTMESH_LIVE_PROVIDER_CONFIG = "C:\secure\contactmesh.live.json"
dotnet test .\tests\ContactMesh.LiveProvider.Tests\ContactMesh.LiveProvider.Tests.csproj --no-build
```

The config file must stay outside the repository or be ignored local config. The test refuses to run unless `ContactMesh:DryRun` is `true` and `ContactMesh:Rules:TargetUsers` contains at least one target, so validation stays scoped to known test accounts.

Recommended live-test config posture:

- Use the same provider sections as the CLI and Worker hosts.
- Provide secrets through environment variables, user secrets, mounted files, or a CI secret store.
- Keep target users limited to dedicated test mailboxes or accounts.
- Review the dry-run operation report separately before ever setting `ContactMesh:DryRun` to `false` in manual runs.

The harness fails on provider errors because those errors usually indicate missing permissions, disabled mailboxes, expired credentials, or API-shape regressions. Tenant-specific partial availability, such as Microsoft 365 users without mailbox-enabled contact stores, should be handled by narrowing `TargetUsers` to accounts intended for validation.
