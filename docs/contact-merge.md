# Contact Merge

ContactMesh treats provider-managed contact fields and user-owned contact fields differently.

Managed fields come from directory users, shared contacts, and sync rules. User-owned fields are existing details in a user's personal contact record that are not part of the managed source record.

Current merge behavior:

- Source display name and organization fields replace managed values.
- Source emails are deduplicated case-insensitively.
- Source phone numbers are deduplicated by normalized digits.
- User-owned emails and phone numbers are preserved when they do not duplicate source data.
- Notes are treated as user-owned by default and are not overwritten by source contacts.
- Source metadata overwrites existing metadata with the same key.

When a managed contact becomes stale, ContactMesh deletes it only if no user-owned data remains. If the user has added notes, non-managed emails, non-managed phone numbers, labels, or metadata, ContactMesh removes managed fields and keeps the contact as user-owned.

When duplicate managed contacts share the same primary email, ContactMesh keeps the first contact, merges unique emails, phone numbers, labels, metadata, and notes into it, then deletes the duplicate records.

Before planning normal sync changes, providers can ask core to prune contacts that are either blank or only contain a managed-domain email with no notes, phone numbers, labels, or organization data.

Directory users are shaped into managed contacts by core: the user ID becomes the contact source ID, the work email is primary, organization fields are copied into managed company fields, labels are attached as policy output, and provider metadata is carried through without exposing provider APIs to core.

Email policy cleanup removes duplicate email addresses on a contact and prefers the provider-resolved send-as address as the primary work email when it is present.

The goal is predictable sync without deleting useful personal details users have added themselves.
