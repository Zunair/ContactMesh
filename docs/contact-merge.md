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

The goal is predictable sync without deleting useful personal details users have added themselves.
