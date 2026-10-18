# Deleting a product

`Products_Delete` is a **hard delete**. If the product has merely stopped being current, change its
status instead.

It refuses while any of these exist, each with its own reason:

| Blocker | Check with | Cleared by |
| --- | --- | --- |
| Child products | `Products_GetProducts` with `parentId` | `Products_Reparent`, or deleting each child first |
| Versions | `Versions_GetVersions` with `productId` | `Versions_Delete` |
| Lines in a package manifest | `ReleasePackages_GetReleasePackages` with `containingProductId` | `ReleasePackages_SetManifest` without the line, or `ReleasePackages_Delete` |
| A dependency on either end, **ended ones included** | `Products_GetDependencies` with `includeEnded: true` — once the product has no children | `Products_RemoveDependency` |

The manifest is checked separately from versions because a carried-forward line can name the product
with no version record at all. Check dependencies last: `Products_GetDependencies` rolls up the whole
subtree, so while children remain it lists their links rather than the product's own and misses links
between two products inside it. The delete checks only the product's own.

`Products_Delete` never deletes anything else for you. Purge a product's history only when the user
asks for it, bottom up:

1. Children: each child is reparented or purged first, by these same steps. Agree which with the user.
   Until then the dependency check below describes the children, not this product.
2. Run the four checks above. List everything that will go — including the deployments that
   `Versions_Delete` and `ReleasePackages_Delete` take with them — and confirm the whole list with the
   user.
3. Releases: for each release listing one of its versions or one of the packages from step 4, remove
   the entry with `Releases_SetContents` (or, hosted, `Releases_PatchContents` replacing that list with
   the ids that stay) while the release is unannounced. An announced or withdrawn
   release's contents are frozen, so it can only be removed with `Releases_Delete`.
   `Releases_GetReleases` with `containingVersionId` finds a release carrying the version directly or
   inside a package. There is no package filter, so a package whose line names no version record is
   found only by reading each release's `packages`.
4. Packages: drop the product's line with `ReleasePackages_SetManifest` (or `ReleasePackages_PatchManifest`,
   replacing `/components` with the lines that stay) while the package is neither
   released nor withdrawn and has other lines. Otherwise use `ReleasePackages_Delete`, which is refused while a
   release still lists the package — go back to step 3 for that release.
5. Versions: `Versions_Delete` for each.
6. Dependencies: `Products_RemoveDependency` for each, ended ones included. Its `id` is the product
   that *has* the dependency — `product.id` on the row — so a `usedBy` row is removed through the
   other product's id.
7. Re-run the four checks. Repeat from step 3 until all four come back empty, then call
   `Products_Delete`.
