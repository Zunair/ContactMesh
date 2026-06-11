# ContactMesh Session Bootstrap

Before changing code in this repository:

1. Read `PROJECT_STATE.json`.
2. Treat it as the compact handoff ledger for completed, in-progress, pending, and blocked work.
3. Keep `ContactMesh.Core` provider-neutral. It must not reference Google, Microsoft, Graph, Workspace, or provider SDKs.
4. After every completed functional, docs, or test slice, update `PROJECT_STATE.json` before the final response or commit.
5. Prefer small commits that match one completed slice.

Standard verification:

```powershell
dotnet build ContactMesh.sln -m:1
dotnet test ContactMesh.sln -m:1 --no-build
```

Generated `bin` and `obj` folders are ignored build output. Do not spend session time cleaning them unless a task specifically requires removing build artifacts.
