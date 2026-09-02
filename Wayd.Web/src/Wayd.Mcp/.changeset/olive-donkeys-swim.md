---
"@wayd/mcp": minor
---

Add product management tools, covering the module end to end: the product catalog, the type and tag configuration it is built from, and the delivery records that track what shipped.

**Catalog.** `Products_GetProducts`, `_GetProduct`, `_GetStatusHistory`, `_GetStatusOptions`, `_Create`, `_Update`, `_Retype`, `_Reparent`, `_ChangeStatus`, `_LinkExternally`, `_Tag`, `_Untag`, `_Delete`. The catalog is one typed tree, and a product's type carries the flag deciding whether versions can be cut against it. Type, parent and status are separate tools rather than fields on the update, so a refusal says which rule refused.

**Configuration.** `ProductTypes_*` and `ProductTagCategories_*` — create, update, activate, delete, reorder, plus the tags on an axis. Two rules run through all of it: seeded system records cannot be modified or deleted but *can* be deactivated, and nothing in use can be deleted. Tags inside a system axis are the exception, refusing deactivation too, so there is no per-tag fallback.

**Delivery.** `Releases_*`, `Versions_*`, `ReleasePackages_*`, `Deployments_*`, `DeploymentEnvironments_*` and `DeliveryMetrics_GetDeliveryMetrics`. The four record types are kept apart deliberately: a release is what was announced to customers, a version is one artifact that was built, a package is what moved through environments together, and a deployment is one of those reaching one environment.

Writes are annotated `destructiveHint`, so clients confirm before running them. Three behaviours worth knowing when approving one:

- **Contents and manifests replace as a set.** `Releases_SetContents` and `ReleasePackages_SetManifest` overwrite what was there, so send the full list including anything that should stay.
- **Tagging a single-value axis silently replaces** the existing tag rather than refusing.
- **`ProductTypes_Update` requires `isReleasable`.** It is a whole-record overwrite, so a rename that resends the wrong value silently changes whether versions can be cut against every product of that type.

Ships with two agent skills, `wayd-products` and `wayd-delivery`.
