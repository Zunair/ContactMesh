# Troubleshooting

## Dry-run First

Keep `ContactMesh:DryRun` set to `true` until sync output has been reviewed.

## Missing Contacts

Check that the target user is included by sync rules, not excluded by an exclusion group, and can see the source group under scoped visibility rules.

## Duplicate Contacts

Duplicates are matched primarily by managed source IDs, then normalized emails and phone numbers in merge helpers. Provider implementations should preserve stable source IDs whenever possible.

## Provider Credentials

Do not debug credentials by committing local config. Use temporary local files, environment variables, or user secrets.
