---
name: wayd-products
description: Guides agents working with the Wayd product catalog via the Wayd MCP server — the typed tree of products, platforms, services and tools an organization owns, plus product dependencies, product types, tags, deployment environments, what is running where, and delivery measures. Use when looking up or creating products, moving one to a different parent, changing a product's type or status, tagging products, deleting one, recording or reading what a product depends on (or what would be hit if it went down), defining or retiring deployment environments, asking what is running in an environment, reading delivery measures or recent shipping activity, or managing the product types and tag axes themselves.
---

# Wayd Products (Catalog / Dependencies / Types / Tags / Environments / Metrics)

## When to use

- Finding what the organization owns, and how it fits together
- Creating a product, or moving one to a different parent
- Changing a product's type or status, or tagging it
- Deleting a product, and understanding why one refuses to delete
- Recording what a product depends on, and answering what depends on it
- Defining, updating or retiring deployment environments
- Asking what is running in each environment right now
- Reading delivery measures and version activity over a window, or what shipped recently
- Managing the configuration itself — product types, tag axes and their tags

For releases, versions, packages and deployments, use the **wayd-delivery** skill instead.

---

## Tool map

| Area | Read | Change |
| --- | --- | --- |
| Products | `Products_GetProducts`, `Products_GetProduct`, `Products_GetStatusOptions`, `Products_GetStatusHistory`, `Products_GetActivities` | `Products_Create`, `Products_Update` (name and description only), `Products_Retype`, `Products_Reparent`, `Products_ChangeStatus`, `Products_LinkExternally` (external identifier), `Products_Tag`, `Products_Untag`, `Products_Delete` |
| Dependencies | `Products_GetDependencies` | `Products_AddDependency`, `Products_UpdateDependency`, `Products_ChangeDependencyTerms`, `Products_EndDependency`, `Products_RemoveDependency` |
| Product types | `ProductTypes_GetProductTypes` | `ProductTypes_Create`, `ProductTypes_Update`, `ProductTypes_SetActive`, `ProductTypes_Delete` |
| Tag axes and tags | `ProductTagCategories_GetProductTagCategories` | `ProductTagCategories_Create`, `ProductTagCategories_Update`, `ProductTagCategories_SetActive`, `ProductTagCategories_Delete`, `ProductTagCategories_Reorder`, `ProductTagCategories_AddTag`, `ProductTagCategories_RenameTag`, `ProductTagCategories_SetTagActive`, `ProductTagCategories_DeleteTag` |
| Environments | `DeploymentEnvironments_GetDeploymentEnvironments`, `DeploymentEnvironments_GetRollout` | `DeploymentEnvironments_Create`, `DeploymentEnvironments_Update`, `DeploymentEnvironments_SetActive`, `DeploymentEnvironments_Delete` |
| Measures | `DeliveryMetrics_GetDeliveryMetrics`, `DeliveryOverview_GetDeliveryOverview`, `DeliveryOverview_GetRecentDeliveryEvents` | — |

---

## The catalog is one typed tree

Every product is a node with a **type**, and the tree is self-referencing: a node's parent is another
product, or nothing if it sits at the root.

```
Wayd                    Product Line     not releasable
├── Wayd API            Service          releasable
├── Wayd Client         Application      releasable
└── @wayd/mcp           Tool             releasable
```

**The type carries one consequential flag: `isReleasable`.** It decides whether versions can be cut
against the node. That is the flag to check before trying to record a version, and it is why a
product line usually holds no versions of its own.

Two things are commonly assumed and are **not** true:

- **There are no allowed-parent rules.** Any type may parent any other. The only structural rule is
  that a product cannot be its own parent or move beneath one of its own descendants.
- **There is no depth limit.**

---

## Each guarded change has its own tool

Type, parent and status are not fields on `Products_Update`. Each carries a rule, and separating them
is what makes a refusal say which rule refused.

| Tool | Refuses when |
| --- | --- |
| `Products_Retype` | The product has versions and the new type is not releasable |
| `Products_Reparent` | The new parent is the product itself or one of its descendants, or the move would put two products with an open dependency above and below one another (end the dependency first) |
| `Products_ChangeStatus` | The status id does not come from `Products_GetStatusOptions` |
| `Products_Delete` | See **Deleting a product** |

---

## Tagging a single-value axis silently replaces

Before `Products_Tag`, check the axis's `allowsMany` in `ProductTagCategories_GetProductTagCategories`.
Where it is false, the call **succeeds and removes the tag the product already carried on that axis** —
it never refuses. If the existing value matters, read it with `Products_GetProduct` and tell the user
what will be replaced.

---

## Deleting a product

`Products_Delete` is a **hard delete**. If the product has merely stopped being current, change its
status instead.

It refuses while any of these exist, each with its own reason:

| Blocker | Check with | Cleared by |
| --- | --- | --- |
| Child products | `Products_GetProducts` with `parentId` | `Products_Reparent`, or deleting each child first |
| Versions | `Versions_GetVersions` with `productId` | `Versions_Delete` |
| Lines in a package manifest | `ReleasePackages_GetReleasePackages` with `containingProductId` | `ReleasePackages_SetManifest` without the line, or `ReleasePackages_Delete` |
| A dependency on either end, **ended ones included** | `Products_GetDependencies` with `includeEnded: true` | `Products_RemoveDependency` |

The manifest is checked separately from versions because a carried-forward line can name the product
with no version record at all.

`Products_Delete` never deletes anything else for you. Purge a product's history only when the user
asks for it, bottom up:

1. Run the four checks above. List everything that will go — including the deployments that
   `Versions_Delete` and `ReleasePackages_Delete` take with them — and confirm the whole list with the
   user.
2. Releases: for each release listing one of its versions (`Releases_GetReleases` with
   `containingVersionId`) or one of the packages from step 3, remove the entry with
   `Releases_SetContents` while the release is unannounced. An announced or withdrawn release's
   contents are frozen, so it can only be removed with `Releases_Delete`.
3. Packages: drop the product's line with `ReleasePackages_SetManifest` while the package is neither
   released nor withdrawn and has other lines. Otherwise use `ReleasePackages_Delete`, which is refused while a
   release still lists the package — go back to step 2 for that release.
4. Versions: `Versions_Delete` for each.
5. Dependencies: `Products_RemoveDependency` for each, ended ones included.
6. Re-run the four checks. Repeat from step 2 until all four come back empty, then call
   `Products_Delete`.

---

## Product dependencies

A product can record which other products it relies on. Each dependency has a **strength**, optional
**interaction styles**, an optional description, and the days it held (`startsOn`, and `endsOn` once
it stopped).

### Reading them rolls up the tree

`Products_GetDependencies` reports `dependsOn` and `usedBy` **across everything beneath the
product**: a product line's `dependsOn` includes its services' links to outside products, and a link
with both ends inside the line appears in neither list. `productPath` and `dependsOnProductPath` say
which descendant each row starts or lands on.

An empty answer means nothing has been **recorded** — dependencies are entered by hand or imported,
never discovered — so say that rather than "nothing depends on it".

### Recording them

Record each with `Products_AddDependency`.

- **Record the most specific product known** — the service that makes the call, not its platform.
  The read side rolls it up to every ancestor anyway, and a link on the platform cannot be rolled
  down.
- **Strength has no default.** 1 Hard: the product stops working without it. 2 Soft: it degrades or
  loses a feature but keeps working. Impact attribution reads this, so ask the person if they have not
  said rather than guessing.
- **Interaction styles refine strength.** A Hard **synchronous** dependency caps the consumer's
  availability at the provider's; a Hard **asynchronous** one turns the provider's downtime into a
  backlog worked through afterwards. Omitting them records nothing rather than recording that there
  are none, so `null` is never evidence either way.

### Changing them keeps history

| The situation | Tool |
| --- | --- |
| Strength or styles changed, or styles are being written down for the first time | `Products_ChangeDependencyTerms` — the default for any terms change. A real change ends the dependency and opens a new one; first-time styles are filled in place |
| Styles never recorded, on an ended dependency | `Products_UpdateDependency` (`Products_ChangeDependencyTerms` refuses ended dependencies) |
| The description is wrong | `Products_UpdateDependency` |
| It was true and has stopped | `Products_EndDependency` — kept, and still counts for the days it held |
| It was recorded on the wrong terms from its first day | `Products_RemoveDependency`, then `Products_AddDependency` — terms can only change from the day after it started |
| It was never true | `Products_RemoveDependency` — deletes it, with a reason |

After `Products_ChangeDependencyTerms`, use the id it returns for any further call: when the terms
changed, the id you passed now belongs to the ended dependency.

For many at once, the `product-management.product-dependencies` import (see **wayd-imports**) applies
**product by product** — every row for one `ProductId` saves together or not at all, and the other
products still import — so read a preflight's rejected rows by product.

---

## Managing the configuration itself

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

---

## Statuses and history

`Products_ChangeStatus` takes a status id from `Products_GetStatusOptions`. Statuses are
per-organization, one list serves every product, and any status is reachable from any other.

For what changed on a product, `Products_GetStatusHistory` covers status moves and
`Products_GetActivities` covers everything else — details, type, parent, tags, external link. Each
activity entry's `payload` carries the value before and after. A `Baseline` entry marks where tracking
began for a product that already existed; nothing before it is recorded. Check `hasNextPage` before
concluding something never happened.

---

## Environments and metrics

An **environment** is a named deployment target, defined once for the organization. Each carries a
**category** — Development, Testing, Staging, Production, or Other for a target that is none of those
(a sandbox, a demo box) — and a ring order for progressive rollout.

**The category is what every production-scoped measure counts on, not the name.** Pipeline
environment names are free text and endlessly varied (`prod`, `Production`, `prd`, `live`), so
filtering or reasoning by name will give wrong answers.

| The situation | Tool |
| --- | --- |
| A new deployment target | `DeploymentEnvironments_Create` — set the category deliberately |
| Rename it, reclassify it, or move its ring | `DeploymentEnvironments_Update` — resend every field; refused on a retired environment, so reinstate it first |
| It is no longer used | `DeploymentEnvironments_SetActive` with `isActive: false` — its deployments are kept and keep counting |
| It is back in use | `DeploymentEnvironments_SetActive` with `isActive: true` |
| The user explicitly wants it and its history gone | `DeploymentEnvironments_Delete` — takes **every deployment into it**; state its `deploymentCount` from `DeploymentEnvironments_GetDeploymentEnvironments` before confirming |

**Reclassifying changes the future, not the past.** Each deployment froze its environment's category
at the time, so promoting a staging environment to production does not retroactively inflate
deployment frequency.

### What is running where

`DeploymentEnvironments_GetRollout` lists every active environment in ring order with what is
**running** there: one entry per product, each the latest deployment that succeeded and was not
rolled back, with packages expanded into their components. A failed attempt leaves its predecessor
running, so check `hasFailedAttemptSince` before saying an environment is healthy.

### Two kinds of measure

`DeliveryMetrics_GetDeliveryMetrics` measures **deployments**; `DeliveryOverview_GetDeliveryOverview`
measures **versions** — what was cut and shipped, whether or not a pipeline recorded deployments. Use
the one that matches the question, and do not mix their figures.

From `DeliveryMetrics_GetDeliveryMetrics`, read the `unavailable` list rather than treating a missing
measure as zero, and report change failure rate as approximate: it is a pipeline proxy.

`DeliveryOverview_GetDeliveryOverview` scopes to a product **and everything beneath it** — a product
line is a valid scope. Pass the reader's IANA `timeZone`: it counts versions by the day they shipped,
and without it the days are UTC days, so a late-evening US release lands on the next day. Carry three
things into an answer:

- **Give the denominator.** `scope.releasableNodeCount` says how many products the figures cover.
- **A null `previousPerWeek` or `previousAverageDays` means nothing shipped in the prior window**, so
  there is no trend to report — not a rise from zero.
- **Cut-to-released excludes versions released without ever being cut** (imports and backfills).
  Report `measuredCount` of `releasedCount` alongside the average.

`DeliveryOverview_GetRecentDeliveryEvents` is the feed of what shipped lately. Reason on `alias`
(Ready, Released, Withdrawn), not on `statusName`, which is per-organization. Scoping it to a product
leaves packages out entirely.

---

## Typical flows

### Adding something to the catalog

1. `ProductTypes_GetProductTypes` — find the type, and check `isReleasable` if versions will be cut
   against it.
2. `Products_GetProducts` — find the parent, if it is not a root node.
3. `Products_Create`.
4. `ProductTagCategories_GetProductTagCategories` then `Products_Tag`, if it should carry tags.

### Answering "what do we own?"

Call `Products_GetProducts` once with no filters — it returns everything, flat and name-ordered,
retired products included — and build the tree from each product's parent reference. Walking it with
`parentId` costs one call per level, since that filter returns direct children only.

### Answering "what would be hit if this went down?"

1. `Products_GetDependencies` on the product — its `usedBy` list is everything outside it that relies
   on it or on anything beneath it.
2. Separate **Hard** (stops working) from **Soft** (degrades) rather than reporting one number.
3. To follow the impact further out, call it again on each dependent product. Say where you stopped.

### Answering "what version is in production?"

`DeploymentEnvironments_GetRollout`, then read the Production-category environments' `running` lists.
Mention any entry with `hasFailedAttemptSince`.

### Answering "why can't I delete this?"

`Products_Delete` names the reason. To check ahead, run the four checks in **Deleting a product**.
