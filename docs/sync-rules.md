# Sync Rules

Sync rules decide which users receive which contacts.

Current rule options:

- `GlobalUserGroups`: user groups that receive global contacts.
- `GlobalExternalContactGroups`: shared external contact groups.
- `ExclusionGroups`: users or groups that should not receive managed contacts.
- `ScopedGroupRoots`: group trees used for scoped, group-aware contact visibility.

Rule processing belongs in `ContactMesh.Core`. Provider projects only resolve provider-specific group memberships and map them into `MeshGroup`.
