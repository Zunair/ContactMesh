# Security

This project can read and write contacts across a Google Workspace tenant. Treat configuration and credentials as highly sensitive.

## Required practices

- Use a dedicated service account.
- Grant only the scopes required for the enabled features.
- Store service-account JSON outside source control.
- Use dry-run mode before any production run.
- Keep audit logs for every create/update/delete.
- Do not publish tenant names, OU names, group names, or user email addresses.

## Reporting vulnerabilities

Open a private security advisory or contact the maintainer directly. Do not publish credentials or tenant-specific data in issues.
