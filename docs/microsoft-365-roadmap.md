# Microsoft 365 Roadmap

The Microsoft 365 provider is intentionally scaffolded but not production-ready.

Provider work:

1. Microsoft Graph client-credentials token acquisition through `MicrosoftGraphClientFactory`.
2. Read Graph users and map them to `MeshUser`.
3. Map Graph groups and nested memberships to `MeshGroup`.
4. Map Outlook contacts to `MeshContact`.
5. Implement batched contact writes.
6. Add tenant-safe dry-run reporting.
7. Add integration tests with mocked Graph responses.

The core project should not change to support Microsoft 365 unless the provider-neutral contract is incomplete.
