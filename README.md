# ContactMesh

ContactMesh syncs directory users, groups, and shared contacts into each user's personal contacts using policy-based rules. It is designed for organizations that need offline mobile contacts, caller ID, and group-aware contact distribution across Google Workspace and Microsoft 365.

## Status

ContactMesh is now a provider-neutral .NET 10 solution with runnable CLI, Worker, and Web hosts.

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

Prerequisite: install the .NET 10 SDK.

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

## Recommended Deployment

For a new Microsoft 365 organization, deploy the published Worker as the scheduled production entrypoint. The Worker is intentionally boring: it runs one sync pass, writes the same dry-run/applied report as the CLI, and exits. That makes it a good fit for Windows Task Scheduler, job history, audit CSVs, and notification emails.

Recommended host layout:

- Install the .NET 10 runtime on the server, or publish the Worker as self-contained if you do not want to manage a shared runtime.
- Create a dedicated Windows or domain service account for ContactMesh.
- Store the published app under a locked-down app folder such as `C:\ContactMesh\app`.
- Store environment-specific config outside the app folder, such as `C:\ContactMesh\config\appsettings.production.json`.
- Store audit logs in a writable data folder such as `C:\ContactMesh\audit-logs`.
- Grant the service account read/execute access to the app folder, read access to config, and write access to audit/log folders.

Publish the Worker for a framework-dependent deployment:

```powershell
dotnet publish .\src\ContactMesh.Worker\ContactMesh.Worker.csproj -c Release -o C:\ContactMesh\app
```

Then create a scheduled task that runs:

```text
Program/script: C:\ContactMesh\app\ContactMesh.Worker.exe
Arguments:      C:\ContactMesh\config\appsettings.production.json
Start in:       C:\ContactMesh\app
```

Use the Web host as an admin-only settings editor, preferably run under the same service account that will run the scheduled task. This matters when saving encrypted Microsoft 365 client secrets: `cmenc:v1:` values are tied to the local ASP.NET Core Data Protection key ring for that user/machine. If the Web editor runs as a different account, either re-enter the secret under the service account, restore the same key ring, or provide the secret through an environment variable or deployment secret store instead of encrypted JSON.

Use the CLI for smoke tests, diagnostics, and focused commands such as `m365-contact-email-slot`; do not make it the default scheduled production entrypoint for a new deployment when the Worker is available.

First-run checklist:

1. Configure Microsoft 365 app permissions and grant admin consent.
2. Start with `ContactMesh:DryRun` set to `true`.
3. Scope the first runs with `TargetUsers` or `GlobalUserGroups`.
4. Enable audit logs and, for live runs, notification emails.
5. Review the dry-run report and CSVs.
6. Set `DryRun` to `false` only after the planned creates, updates, and deletes look correct.

Docker and always-on hosted worker deployment are still roadmap items. The sample compose file is useful as a starting point, but Windows Task Scheduler plus the published Worker is the recommended production path today.

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
