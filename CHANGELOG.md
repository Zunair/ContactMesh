# Changelog

All notable changes to ContactMesh will be documented in this file.

## Unreleased

- Renamed the solution and modern projects to ContactMesh.
- Reorganized the repository into core, providers, app hosts, tests, samples, docs, and migration tools.
- Added provider-neutral core models, abstractions, merge logic, sync planning, rule shells, and typed options.
- Preserved legacy Google Workspace code under `tools/migration/`.
- Ported legacy group visibility and group mapping behavior into provider-neutral core rules.
- Ported stale managed-contact cleanup so user-owned details are preserved when managed contacts fall out of scope.
- Ported duplicate managed-contact consolidation by primary email.
- Ported blank and managed-email-only contact pruning rules.
