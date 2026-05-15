# Sync Rules

Sync rules decide which users receive which contacts.

Current rule options:

- `GlobalUserGroups`: user groups that receive global contacts.
- `TargetUsers`: optional user IDs or email addresses that limit which users receive managed contacts.
- `MainContactsGroupEmail`: optional group ID or email whose user members become directory contacts instead of every eligible tenant user.
- `MainContactsGroupLabel`: label applied to those directory contacts.
- `GlobalExternalContactGroups`: shared external contact groups.
- `ExclusionGroups`: users or groups that should not receive managed contacts.
- `ScopedGroupRoots`: group trees used for scoped, group-aware contact visibility.
- `GroupMappings`: source-to-target group mappings used to merge one group's members into another contact label.
- `IncludedOrganizationUnits`: organization unit prefixes that are allowed to receive managed contacts.
- `ExcludedOrganizationUnits`: organization unit prefixes that are blocked; append `=Ignore` to suppress noisy reporting.

Rule processing belongs in `ContactMesh.Core`. Provider projects only resolve provider-specific group memberships and map them into `MeshGroup`.

`TargetUsers` limits sync recipients only. Directory users that still pass the suspended, exclusion, and organization unit rules can remain source contacts for the scoped targets.

`MainContactsGroupEmail` limits directory source contacts only. Sync recipients are still chosen by target, exclusion, and organization unit rules. Nested groups are honored when provider group data includes the child group or transitive user membership.

Visible groups can be shaped into managed contacts with a `group:` source ID. This keeps distribution-list contacts distinct from directory users while still letting the same sync planner create, update, or remove them.

Group visibility uses provider-neutral values:

- `Domain`: every target can see the group or its members.
- `Members`: only members can see the group or its members.
- `Hidden`: no target can see it through sync rules.
