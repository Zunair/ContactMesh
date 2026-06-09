# ContactMesh

ContactMesh syncs directory users, groups, and shared contacts into each user's personal contacts using policy-based rules. It is designed for organizations that need offline mobile contacts, caller ID, and group-aware contact distribution across Google Workspace and Microsoft 365.

## Status

ContactMesh is now a provider-neutral .NET 8 solution with runnable CLI, Worker, and Web hosts.

- `ContactMesh.Core` owns provider-neutral models, rules, merge logic, sync planning, execution, and dry-run reporting.
- `ContactMesh.Google` includes delegated Google Workspace auth plus People API contact and contact-group label clients.
- `ContactMesh.Microsoft365` includes Microsoft Graph auth, user, group, transitive membership, contact, and contact write clients.
- `ContactMesh.Hosting` binds shared configuration, decrypts protected local config secrets, and wires the selected provider into the sync orchestrator.
- `ContactMesh.Cli` and `ContactMesh.Worker` run a sync pass and print the dry-run/applied operation report.
- `ContactMesh.Web` renders a server-side settings editor at `/` and `/settings`.

The legacy Google implementation is preserved under `tools/migration/` as reference material. Production Docker/hosted worker mode is still pending; `samples/docker-compose.sample.yml` is only a starting point.

Provider maturity: Microsoft 365 is the primary path and is mostly implemented with broad unit coverage plus live dry-run validation. Google Workspace has useful provider pieces and migration reference code, but it has not been brought to the same tested/production-ready level yet.

## Repository Layout

```text
src/ContactMesh.Core/          Provider-neutral models, rules, merge, and sync logic
src/ContactMesh.Google/        Google Workspace auth, directory/group shells, People API contacts
src/ContactMesh.Microsoft365/  Microsoft Graph auth, users, groups, memberships, contacts
src/ContactMesh.Hosting/       Shared config binding, secret protection, and provider host wiring
src/ContactMesh.Cli/           Console runner
src/ContactMesh.Worker/        Worker-style sync runner
src/ContactMesh.Web/           Settings editor UI
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
    "DisableDeletes": false,
    "ForceResetLabels": false,
    "ForceDeduplicatePhones": false,
    "ForceNormalizeEmailTypes": false,
    "ManagedEmailDomains": [ "example.org" ],
    "AuditLog": {
      "Enabled": false,
      "Directory": "audit-logs",
      "IncludeNoChange": false,
      "IncludeDryRunPlannedAsWrites": false
    },
    "Notifications": {
      "Enabled": false,
      "From": "",
      "SuccessTo": [],
      "FailureTo": [],
      "SubjectPrefix": "[ContactMesh]",
      "AttachCsvOnFailure": true,
      "MaxAttachmentBytes": 5242880
    },
    "Rules": {
      "GlobalUserGroups": [],
      "MainContactsGroupEmails": [ "company-directory@example.org" ],
      "MainContactsGroupLabel": "-Directory",
      "GroupContactPrefix": "+",
      "GlobalExternalContactGroups": [],
      "GroupsToSyncByGroup": [],
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

When the Web settings page saves `Microsoft365:ClientSecret`, it writes a protected value with a `cmenc:v1:` prefix. CLI, Worker, and Web decrypt that value during configuration binding, so Microsoft Graph auth still receives the normal plaintext secret at runtime. Existing plaintext JSON secrets and environment-variable overrides continue to work; plaintext JSON is encrypted the next time the Web editor saves the file.

Protected Web-saved secrets are tied to the local ASP.NET Core Data Protection key ring for the account running the app. For scheduled tasks or services, run the Web editor and the CLI/Worker under the same dedicated service account, or re-enter the secret from the Web editor in the account/environment that will run the sync.

Use `GroupsToSyncByGroup` for contact labels: each configured container group's direct subgroups become managed group contacts, and subgroup display names become labels for their members. Regular visible groups control visibility and contact inclusion but do not create labels.

Common overrides:

```powershell
$env:ContactMesh__DryRun = "true"
$env:Microsoft365__ClientSecret = "<secret>"
dotnet run --no-build --project .\src\ContactMesh.Cli\ContactMesh.Cli.csproj -- .\appsettings.local.json --ContactMesh:Provider=Microsoft365
```

Keep `DryRun` enabled until the generated plan has been reviewed. Setting `ContactMesh:DryRun` to `false` allows provider writes. Set `ContactMesh:DisableDeletes` to `true` when you want live runs to create and update contacts but skip all planned contact delete writes.

Set `ContactMesh:AuditLog:Enabled` to `true` to write per-run detail and summary CSV files. Set `ContactMesh:Notifications:Enabled` to `true` to send live-run success/failure email through Microsoft Graph sendMail. Failure notifications can attach the audit CSVs; see `docs/audit-and-notifications.md`.

Set `ContactMesh:ForceResetLabels` to `true` for a one-time cleanup when old managed labels need to be replaced completely. Review the dry-run, run once with writes enabled, then set it back to `false`.

For old managed contacts that have the same phone number in multiple fields, set `ContactMesh:ForceDeduplicatePhones` to `true`, review the dry-run, run the cleanup once with writes enabled, then set it back to `false`.

For old managed contacts whose organization email appears under Other email, set `ContactMesh:ForceNormalizeEmailTypes` to `true`, review the dry-run, run once with writes enabled, then set it back to `false`.

For focused Microsoft 365 contact diagnostics, configure `Microsoft365:ContactDiagnostic` or pass CLI arguments to `m365-contact-email-slot`:

```powershell
dotnet run --project .\src\ContactMesh.Cli -- m365-contact-email-slot --user zfayaz@example.org --contact zfadmin@example.org
```

Add `--apply` only when you want the diagnostic command to clear the contact's primary/secondary/tertiary email slots and write back one primary work email.
For beta-only email type testing, add `--beta-email-type work`; this calls `/beta` with a typed `emailAddresses[]` payload and then reads the beta projection back.

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

Run the Web settings editor:

```powershell
dotnet run --no-build --project .\src\ContactMesh.Web\ContactMesh.Web.csproj -- .\appsettings.local.json --urls http://localhost:5050
```

Open `http://localhost:5050/` or `http://localhost:5050/settings`. The Web editor saves the selected JSON config file and preserves masked secrets; Microsoft 365 client secrets are encrypted on save.

## Provider Setup

Google Workspace:

1. Enable Admin SDK Directory API, Groups Settings API, and People API.
2. Create a service account with domain-wide delegation.
3. Authorize the contacts scope shown in `samples/google/appsettings.sample.json`.
4. Set `GoogleWorkspace:ServiceAccountFile` to a credential file stored outside the repository.
5. Set `GoogleWorkspace:AdminUserEmail` to the delegated admin account.

Microsoft 365:

1. Register an Entra ID application for ContactMesh.
2. Grant the recommended Microsoft Graph application permissions below, then grant admin consent.
3. Configure `Microsoft365:TenantId`, `Microsoft365:ClientId`, and `Microsoft365:ClientSecret`.
4. Keep the default `https://graph.microsoft.com/.default` scope unless your tenant strategy requires otherwise.

Recommended app registration API permissions:

| API / permissions name | Type | Description | Admin consent required |
| --- | --- | --- | --- |
| Microsoft Graph / `Contacts.ReadWrite` | Application | Read and write contacts in all mailboxes | Yes |
| Microsoft Graph / `Directory.Read.All` | Application | Read directory data | Yes |
| Microsoft Graph / `GroupMember.Read.All` | Application | Read all group memberships | Yes |
| Microsoft Graph / `Mail.Send` | Application | Send mail as any user | Yes |
| Microsoft Graph / `Member.Read.Hidden` | Application | Read all hidden memberships | Yes |
| Microsoft Graph / `OrgContact.Read.All` | Application | Read organizational contacts | Yes |
| Microsoft Graph / `User.Read.All` | Application | Read all users' full profiles | Yes |

## Roadmap

- Complete Docker/hosted worker mode.
- Keep expanding provider behavior without adding provider references to `ContactMesh.Core`.

## Security

Never commit service account keys, OAuth client secrets, tenant IDs, exported contacts, or organization-specific group names. See `SECURITY.md`.
