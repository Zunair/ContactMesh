# Run audit logs and email notifications

ContactMesh writes a per-run audit log to disk and (optionally) sends a success or
failure summary email after each sync. Both features apply to runs invoked from the
CLI and the Worker; the Web host can reuse the same pipeline when wired up.

## Configuration

Both features bind from the application configuration under `ContactMesh`.

```jsonc
"ContactMesh": {
  // ...other settings...
  "AuditLog": {
    "Enabled": true,
    "Directory": "logs/audit",
    "IncludeNoChange": false,
    "IncludeDryRunPlannedAsWrites": false
  },
  "Notifications": {
    "Enabled": true,
    "From": "contactmesh-noreply@example.org",
    "SuccessTo": [ "directory-ops@example.org" ],
    "FailureTo": [ "directory-ops@example.org", "oncall@example.org" ],
    "SubjectPrefix": "[ContactMesh]",
    "AttachCsvOnFailure": true,
    "MaxAttachmentBytes": 3145728
  }
}
```

### `AuditLog`

| Property | Default | Notes |
|---|---|---|
| `Enabled` | `true` | When `false`, no audit files are written. |
| `Directory` | `logs/audit` | Resolved relative to the current working directory when not rooted. Created automatically. |
| `IncludeNoChange` | `false` | When `true`, per-target no-change rows are included in the detail CSV. |
| `IncludeDryRunPlannedAsWrites` | `false` | Reserved. Detail rows are labelled `Planned` for dry-run and `Written` for live writes regardless. |

### `Notifications`

| Property | Default | Notes |
|---|---|---|
| `Enabled` | `true` | When `false`, no email is sent. |
| `From` | `""` | Sender mailbox. Required to send. For Microsoft 365 the application must have `Mail.Send` granted for this user. |
| `SuccessTo` | `[]` | Recipients for successful (non-failed) runs. Empty list skips success mail. |
| `FailureTo` | `[]` | Recipients for failed runs. Empty list skips failure mail. |
| `SubjectPrefix` | `[ContactMesh]` | Prepended to all subjects. |
| `AttachCsvOnFailure` | `true` | When `true`, attaches both the summary and detail CSVs on failure mails. |
| `MaxAttachmentBytes` | `3145728` (3 MiB) | Each attachment is truncated to this size with a trailing notice if exceeded. |

> **Dry-run runs never send email.** The audit CSV is still written so you can review the plan locally.

## Output files

For every run the writer creates two files in `AuditLog:Directory`:

```
{provider}-{yyyyMMdd-HHmmss}-{runId}-detail.csv
{provider}-{yyyyMMdd-HHmmss}-{runId}-summary.csv
```

The `runId` is generated per run (`{yyyyMMdd-HHmmss}-{guid-fragment}`, 24 chars total).
Both files are UTF-8 with a BOM so they open cleanly in Excel.

### Detail CSV columns

`Timestamp, Provider, RunId, DryRun, TargetUserId, Operation, Status, SourceId, DisplayName, PrimaryEmail, Labels, LabelsRemoved, ChangedFields, SourceRule, Reason`

* `Status` is `NoChange`, `Planned` (dry-run) or `Written` (live).
* Warnings and errors emitted by a target are appended as additional rows where the level appears in `Operation` and `Status`, with the message in `Reason`.

### Summary CSV columns

`Provider, RunId, HostKind, ConfigPath, StartedAt, CompletedAt, DurationSeconds, DryRun, Outcome, TargetCount, CreateCount, UpdateCount, DeleteCount, NoChangeCount, WriteCount, WarningCount, ErrorCount, FailureMessage, Warnings, Errors, DetailCsvPath`

`Outcome` is `Success` or `Failure`. A run is considered a failure when an
unhandled exception escaped the orchestrator or `HasErrors` is true on the result.

## Email behaviour

* Mail is sent through the existing Microsoft Graph token provider (`POST /users/{From}/sendMail`).
* Subjects look like `[ContactMesh] Microsoft365 sync Success — 5C/3U/0D over 12 targets`.
* The body includes provider, run id, host kind, configuration path, the rendered run report, artifact paths, and (on failure) the exception details.
* For non-Microsoft 365 providers, the dispatcher logs `No notification sender configured.` to the run output and skips sending. Email for Google Workspace runs is not implemented in this slice.

## Operations

* The audit directory should be retained per the policy applicable to PII / directory data. Consider rotating or shipping the CSVs to a SIEM if you have a centralised audit requirement.
* `MaxAttachmentBytes` keeps inbound mailbox limits in check. Recipients see a truncation notice in any over-sized attachment.
* If email fails to send, the failure is written to the run output but does **not** fail the overall sync run — the audit CSVs are the source of truth.
