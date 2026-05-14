# Google Workspace Setup

The Google provider is the first production provider to migrate from the legacy code in `tools/migration/`.

Required Google Workspace setup:

1. Create a Google Cloud project.
2. Enable Admin SDK Directory API, Groups Settings API, and People API.
3. Create a service account.
4. Enable domain-wide delegation for the service account.
5. Authorize the required scopes in Google Workspace Admin Console.
6. Store the service account credential outside the repository.
7. Configure `samples/google/appsettings.sample.json` for local dry-runs.

Start with `DryRun: true` and a limited target group before applying changes to real user contacts.
