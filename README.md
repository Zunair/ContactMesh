# ContactMesh

ContactMesh syncs directory users, groups, and shared contacts into each user's personal contacts using policy-based rules. It is designed for organizations that need offline mobile contacts, caller ID, and group-aware contact distribution across Google Workspace and Microsoft 365.

## Status

ContactMesh is now a provider-neutral .NET 8 solution with runnable CLI, Worker, and Web hosts.

- `ContactMesh.Core` owns provider-neutral models, rules, merge logic, sync planning, execution, and dry-run reporting.
- `ContactMesh.Google` includes delegated Google Workspace auth plus People API contact and contact-group label clients.
- `ContactMesh.Microsoft365` includes Microsoft Graph auth, user, group, transitive membership, contact, and contact write clients.
- `ContactMesh.Hosting` binds shared configuration and wires the selected provider into the sync orchestrator.
- `ContactMesh.Cli` and `ContactMesh.Worker` run a sync pass and print the dry-run/applied operation report.
- `ContactMesh.Web` renders a server-side settings overview at `/` and `/settings`.

The legacy Google implementation is preserved under `tools/migration/` as reference material. Production Docker/hosted worker mode is still pending; `samples/docker-compose.sample.yml` is only a starting point.

## Repository Layout

```text
src/ContactMesh.Core/          Provider-neutral models, rules, merge, and sync logic
src/ContactMesh.Google/        Google Workspace auth, directory/group shells, People API contacts
src/ContactMesh.Microsoft365/  Microsoft Graph auth, users, groups, memberships, contacts
src/ContactMesh.Hosting/       Shared config binding and provider host wiring
src/ContactMesh.Cli/           Console runner
src/ContactMesh.Worker/        Worker-style sync runner
src/ContactMesh.Web/           Settings overview UI
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

Prerequisite: install the .NET 8 SDK.

```powershell
dotnet build ContactMesh.sln -m:1
dotnet test ContactMesh.sln -m:1 --no-build
```

Live provider validation is opt-in and secret-free by default. See `docs/live-provider-tests.md` for the environment variables and dry-run guardrails.

## Configuration

The hosts load the first `.json` argument as the config file. If no JSON file is passed, they look for `appsettings.json` in the current directory. Environment variables and command-line values override JSON configuration.

Start from one of the samples:

```powershell
Copy-Item .\samples\google\appsettings.sample.json .\appsettings.local.json
# or
Copy-Item .\samples\microsoft365\appsettings.sample.json .\appsettings.local.json
```

The central config shape is:

```json
{
  "ContactMesh": {
    "Provider": "Google",
    "DryRun": true,
    "ManagedEmailDomains": [ "example.org" ],
    "Rules": {
      "GlobalUserGroups": [],
      "GlobalExternalContactGroups": [],
      "ExclusionGroups": [],
      "ScopedGroupRoots": [],
      "GroupMappings": [],
      "IncludedOrganizationUnits": [ "/" ],
      "ExcludedOrganizationUnits": []
    }
  }
}
```

Provider-specific sections are `GoogleWorkspace` and `Microsoft365`. Keep credentials out of the repository and provide secrets with user secrets, environment variables, mounted files, or your deployment secret store.

Common overrides:

```powershell
$env:ContactMesh__DryRun = "true"
$env:Microsoft365__ClientSecret = "<secret>"
dotnet run --no-build --project .\src\ContactMesh.Cli\ContactMesh.Cli.csproj -- .\appsettings.local.json --ContactMesh:Provider=Microsoft365
```

Keep `DryRun` enabled until the generated plan has been reviewed. Setting `ContactMesh:DryRun` to `false` allows provider writes.

## Run

Run commands assume `dotnet build ContactMesh.sln -m:1` has completed.

Run a no-provider smoke pass:

```powershell
dotnet run --no-build --project .\src\ContactMesh.Cli\ContactMesh.Cli.csproj
```

Run a dry-run sync through the CLI:

```powershell
dotnet run --no-build --project .\src\ContactMesh.Cli\ContactMesh.Cli.csproj -- .\appsettings.local.json
```

Run the Worker host. It currently runs the same sync job once; hosted scheduling/containerization is pending.

```powershell
dotnet run --no-build --project .\src\ContactMesh.Worker\ContactMesh.Worker.csproj -- .\appsettings.local.json
```

Run the Web settings overview:

```powershell
dotnet run --no-build --project .\src\ContactMesh.Web\ContactMesh.Web.csproj -- .\appsettings.local.json --urls http://localhost:5050
```

Open `http://localhost:5050/` or `http://localhost:5050/settings`.

## Provider Setup

Google Workspace:

1. Enable Admin SDK Directory API, Groups Settings API, and People API.
2. Create a service account with domain-wide delegation.
3. Authorize the contacts scope shown in `samples/google/appsettings.sample.json`.
4. Set `GoogleWorkspace:ServiceAccountFile` to a credential file stored outside the repository.
5. Set `GoogleWorkspace:AdminUserEmail` to the delegated admin account.

Microsoft 365:

1. Register an Entra ID application for ContactMesh.
2. Grant the Graph application permissions needed for users, groups, memberships, and mailbox contacts.
3. Configure `Microsoft365:TenantId`, `Microsoft365:ClientId`, and `Microsoft365:ClientSecret`.
4. Keep the default `https://graph.microsoft.com/.default` scope unless your tenant strategy requires otherwise.

## Roadmap

- Complete Docker/hosted worker mode.
- Keep expanding provider behavior without adding provider references to `ContactMesh.Core`.

## Security

Never commit service account keys, OAuth client secrets, tenant IDs, exported contacts, or organization-specific group names. See `SECURITY.md`.
