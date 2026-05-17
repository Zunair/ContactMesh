# Sync Rules

Sync rules decide which users receive which contacts.

Current rule options:

- `GlobalUserGroups`: user groups that receive global contacts.
- `TargetUsers`: optional user IDs or email addresses that limit which users receive managed contacts.
- `MainContactsGroupEmail`: optional group ID or email whose user members become directory contacts instead of every eligible tenant user.
- `MainContactsGroupLabel`: compatibility label applied to directory contacts from the main contacts group. Prefer label container groups for new label rules.
- `GroupContactPrefix`: prefix added to managed group contact display names; defaults to `+`.
- `GlobalExternalContactGroups`: shared external contact groups.
- `GroupsToSyncByGroup`: label container groups whose direct group members become managed group-email contacts and contact labels, matching the legacy `groupsToSyncByGroup` behavior.
- `ExclusionGroups`: users or groups that should not receive managed contacts.
- `ScopedGroupRoots`: group trees used for scoped, group-aware contact visibility.
- `GroupMappings`: source-to-target group mappings used to merge one group's members into another contact label.
- `IncludedOrganizationUnits`: organization unit prefixes that are allowed to receive managed contacts.
- `ExcludedOrganizationUnits`: organization unit prefixes that are blocked; append `=Ignore` to suppress noisy reporting.

Rule processing belongs in `ContactMesh.Core`. Provider projects only resolve provider-specific group memberships and map them into `MeshGroup`.

`TargetUsers` limits sync recipients only. Directory users that still pass the suspended, exclusion, and organization unit rules can remain source contacts for the scoped targets.

`MainContactsGroupEmail` limits directory source contacts only. Sync recipients are still chosen by target, exclusion, and organization unit rules. Nested groups are honored when provider group data includes the child group or transitive user membership.

Visible groups can be shaped into managed contacts with a `group:` source ID. This keeps distribution-list contacts distinct from directory users while still letting the same sync planner create, update, or remove them. Visible groups do not create contact labels by themselves.
Managed group contacts use `GroupContactPrefix` in their display names and hyphenate whitespace, so `2709 North Broad Street (2709B)` becomes `+2709-North-Broad-Street-(2709B)` by default.

`GroupsToSyncByGroup` expands each configured container group by its direct group members. Those member groups become managed contacts with their group email address, even when the member group is only present as a group member record. Directory contacts that belong to one of those member groups, including through nested group membership, receive that member group's label. Labels use the member group's display name when present, then fall back to email or id. This ports the legacy pattern where a container such as `Labels` can contain `Location`, and users in nested branch-location groups receive the `Location` contact label.

Group visibility uses provider-neutral values:

- `Domain`: every target can see the group or its members.
- `Members`: only members can see the group or its members.
- `Hidden`: no target can see it through sync rules.
