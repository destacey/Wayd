# Managing the configuration itself

Types and tag categories are administrator-managed, organization-wide configuration. Two rules run
through every tool that changes them.

**Seeded system records cannot be modified or deleted — but they can be deactivated.** The guard
protects what a seeded record *means*: its name, its tags, whether it takes many. Whether the
organization currently uses it is a different question, so `ProductTypes_SetActive` and
`ProductTagCategories_SetActive` work on system records. An organization that does not ship libraries
hides that type rather than fighting the seeder.

The exception is at the tag level. `ProductTagCategories_AddTag`, `ProductTagCategories_RenameTag`,
`ProductTagCategories_SetTagActive` and `ProductTagCategories_DeleteTag` are **all** refused on a
system category, deactivation included — there is no per-tag fallback. Retire the whole axis instead.

**Nothing in use can be deleted.** A type is in use when any product carries it; an axis is in use
when any product is tagged along it; a tag is in use when any product carries it (its `productCount`
is above zero). All three refuse with "Deactivate it instead", so in practice delete only removes
something created by mistake and never applied.

**`allowsMany` is set once, by `ProductTagCategories_Create`.** No tool changes it afterwards, and it
decides whether a second tag joins the first or silently replaces it — confirm it with the user before
creating the axis.
