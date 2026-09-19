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

Two things are commonly assumed and are **not** true today:

- **There are no allowed-parent rules.** Any type may parent any other. The only structural rule is
  that a product cannot be its own parent or move beneath one of its own descendants.
- **There is no depth limit.**

---

## Reading the tree

`Products_GetProducts` returns a **flat list ordered by name**, not a tree. Each product carries its
parent as a reference, so build the hierarchy yourself.

Two filter behaviours to plan around:

- **`parentId` returns direct children only**, not a subtree. Walking a whole branch means one call
  per level. There is also no way to ask for root nodes — omitting `parentId` returns everything.
- **`tagId` combines as AND.** Passing two tags returns products carrying both, not either.

Omitting `statusCategory` returns every product **including retired ones**, which is deliberate: the
caller decides whether retired nodes are wanted.

---

## Each guarded change has its own tool

Type, parent and status are not fields on `Products_Update`. Each carries a rule, and separating them
is what makes a refusal say which rule refused.

| Tool | Refuses when |
| --- | --- |
| `Products_Retype` | The product has versions and the new type is not releasable |
| `Products_Reparent` | The new parent is the product itself or one of its descendants, or the move would put two products with an open dependency above and below one another |
| `Products_ChangeStatus` | The status id does not belong to the product workflow |
| `Products_Delete` | It has children, has versions, appears in a package manifest, or is on either end of a dependency |

`Products_Update` changes only the **name and description**, and an omitted description is cleared.
The external link is deliberately its own tool too — keeping it out of the update means a rename
cannot silently clear it.

---

## Two traps worth knowing before you call

### Tagging a single-value axis silently replaces

Tags live in categories — axes like Platform or Compliance. A category's `allowsMany` flag decides
what a second tag on that axis does:

- `allowsMany: true` — the tag joins the others.
- `allowsMany: false` — **the new tag replaces the existing one. The call succeeds and the previous
  tag is gone.** It does not refuse.

Read the product and the category first if the existing value matters. Call
`ProductTagCategories_GetProductTagCategories` to see both the tags and the `allowsMany` flag.

### Deleting a product is permanent

`Products_Delete` is a **hard delete**, unlike everything in delivery, where records are withdrawn
and kept. If the product has merely stopped being current, change its status instead.

It refuses while anything depends on it, and each reason is distinct — children, versions,
appearing in a release package manifest, or being named on either end of a product dependency. That
manifest one is checked separately because a carried-forward manifest line often names a product
that has no version row at all, and the dependency one counts **ended** links too: deleting the
product would erase the record of what relied on it.

---

## Product dependencies

A product can record which other products it relies on. Each dependency has a **strength**, an
optional description, and the days it held (`startsOn`, and `endsOn` once it stopped).

### Reading them rolls up the tree

`Products_GetDependencies` returns `dependsOn` and `usedBy`, **rolled up across everything beneath
the product**. A link from one of a product line's services to an outside product appears in the
line's `dependsOn`; a link with both ends inside the line appears in **neither** list, because from
outside it is the line depending on itself. Each row carries both ends and their full ancestry
(`productPath`, `dependsOnProductPath`), so it says which descendant it starts or lands on.

Ended dependencies are left out unless `includeEnded` is true. An empty answer means nothing has been
**recorded** — dependencies are entered by hand or imported, never discovered — so say that rather
than "nothing depends on it".

### Recording them

- **Record the most specific product known** — the service that makes the call, not its platform.
  The read side rolls it up to every ancestor anyway, and a link on the platform cannot be rolled
  down.
- **A product cannot depend on itself or on anything above or below it in the tree.** That is
  composition, which the tree already records.
- **Strength has no default.** 1 Hard: the product stops working without it. 2 Soft: it degrades or
  loses a feature but keeps working. Impact attribution reads this, so a guess either way misstates
  it — ask the person if they have not said.
- **One open dependency per pair**, and a later one on the same pair cannot overlap an earlier one.
  Dates default to today, may be backdated, and cannot be in the future.

### Changing them keeps history

| The situation | Tool |
| --- | --- |
| The description is wrong | `Products_UpdateDependency` (an omitted description is cleared) |
| It was true and has stopped | `Products_EndDependency` — kept, still counts for the days it held |
| It became harder or softer | `Products_ChangeDependencyStrength` — ends it and opens a new one |
| It was never true | `Products_RemoveDependency` — deletes it, and needs a reason |

**`Products_ChangeDependencyStrength` returns a new id.** The dependency you passed is now ended, so
use the returned id for anything that follows. Prefer ending over removing: removal erases the record
that the dependency ever held.

Dependencies also constrain other changes: `Products_Reparent` refuses a move that would put two
products with an open dependency above and below one another (end the dependency first), and
`Products_Delete` refuses while the product is on either end of any dependency, ended ones included.

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

The exception is at the tag level. `AddTag`, `RenameTag`, `SetTagActive` and `DeleteTag` are **all**
refused on a system category, deactivation included — there is no per-tag fallback. Retire the whole
axis instead.

**Nothing in use can be deleted.** A type is in use when any product carries it; an axis is in use
when any product is tagged along it; a tag is in use when any product carries it (its `productCount`
is above zero). All three refuse with "Deactivate it instead", so in practice delete only removes
something created by mistake and never applied.

### Two sharp edges

- **`allowsMany` is fixed at creation.** It is not on the update tool. Choose it deliberately, because
  it is what decides whether a second tag joins the first or silently replaces it.
- **`ProductTypes_Update` requires `isReleasable`.** It is a whole-record overwrite, so renaming a
  type means resending its current releasability — and the wrong value silently changes whether
  versions can be cut against every product of that type. Read the type first.

`ProductTagCategories_Reorder` needs **every category exactly once**; a partial list is refused. Read
them all, then send the complete sequence.

---

## Statuses are configuration, not a fixed list

`Products_ChangeStatus` takes a **status UUID**, and the statuses are per-organization. Always call
`Products_GetStatusOptions` first — it returns them in the order the administrator arranged the
lifecycle, and the same list serves every product, so one call covers them all.

Any status is reachable from any other; there is no transition graph for products. The status name is
frozen onto the history at the moment of the change, so renaming a status later does not rewrite what
past entries read as.

For everything else that changed on a product — details, type, parent, tags, external link — use
`Products_GetActivities`. It returns entries newest first, each with a `category`, a `summary`, who made
the change and when, and a `payload` JSON string carrying the value before and after. A `Baseline`
entry marks where tracking began for a product that already existed; nothing before it is recorded.

---

## Environments and metrics

An **environment** is a named deployment target, defined once for the organization. Each carries a
**category** — Development, Testing, Staging, Production, or Other for a target that is none of those
(a sandbox, a demo box) — and a ring order for progressive rollout.

**The category is what every production-scoped measure counts on, not the name.** Pipeline
environment names are free text and endlessly varied (`prod`, `Production`, `prd`, `live`), so
filtering or reasoning by name will give wrong answers.

Two consequences:

- **Environments are retired, never deleted.** There is no delete tool. A retired environment keeps
  every deployment recorded against it, and those keep counting toward the measures they already
  count toward. Editing is refused on a retired environment, so reinstate it first.
- **Reclassifying changes the future, not the past.** Each deployment froze its environment's
  category at the time, so promoting a staging environment to production does not retroactively
  inflate deployment frequency.

### What is running where

`DeploymentEnvironments_GetRollout` lists every active environment in ring order with what is
**running** there: one entry per product, each the latest deployment that **succeeded and was not
rolled back**. A failed attempt leaves its predecessor running, so check `hasFailedAttemptSince`
before saying an environment is healthy — it is true when someone has since tried to move past the
running version and could not. A package deployment is expanded into its components, each taking
its own product's slot; `versionLabel` is always set, while `version` is null for a component never
cut as a version in Wayd.

### Two kinds of measure

`DeliveryMetrics_GetDeliveryMetrics` measures **deployments**; `DeliveryOverview_GetDeliveryOverview`
measures **versions** — what was cut and shipped, whether or not a pipeline recorded deployments. Use
the one that matches the question, and do not mix their figures.

`DeliveryMetrics_GetDeliveryMetrics` returns deployment frequency and change failure rate, plus an
`unavailable` list naming what could not be computed and why. **Read that list** rather than treating
a missing measure as zero. Change failure rate is a proxy: a pipeline run that failed before reaching
production is a failure that was *prevented*, while a real change failure succeeded and then broke
something — which the pipeline cannot know. Report it as approximate.

`DeliveryOverview_GetDeliveryOverview` takes a window and optionally a product, which covers **that
node and everything beneath it** — a product line is a valid scope. Three things to carry into an
answer:

- **Give the denominator.** `scope.releasableNodeCount` says how many products the figures cover.
- **A null `previousPerWeek` or `previousAverageDays` means nothing shipped in the prior window**, so
  there is no trend to report — not a rise from zero.
- **Cut-to-released excludes versions released without ever being cut** (imports and backfills).
  Report `measuredCount` of `releasedCount` alongside the average.

`DeliveryOverview_GetRecentDeliveryEvents` is the feed: one entry per version or package at its latest
status change, newest first. Reason on `alias` (Ready, Released, Withdrawn), not on `statusName`,
which is per-organization. Scoping it to a product leaves packages out entirely.

---

## Typical flows

### Adding something to the catalog

1. `ProductTypes_GetProductTypes` — find the type, and check `isReleasable` if versions will be cut
   against it.
2. `Products_GetProducts` — find the parent, if it is not a root node.
3. `Products_Create`.
4. `ProductTagCategories_GetProductTagCategories` then `Products_Tag`, if it should carry tags.

### Answering "what do we own?"

`Products_GetProducts` with no filters returns everything, flat and name-ordered. Build the tree from
each product's parent reference rather than walking level by level.

### Answering "what would be hit if this went down?"

1. `Products_GetDependencies` on the product — its `usedBy` list is everything outside it that relies
   on it or on anything beneath it.
2. Separate **Hard** (stops working) from **Soft** (degrades) rather than reporting one number.
3. To follow the impact further out, call it again on each dependent product. Say where you stopped.

### Answering "what version is in production?"

`DeploymentEnvironments_GetRollout`, then read the Production-category environments' `running` lists.
Mention any entry with `hasFailedAttemptSince`.

### Answering "why can't I delete this?"

`Products_Delete` names the reason. To check ahead: `Products_GetProducts` with `parentId` for
children, `Versions_GetVersions` with `productId` for versions, and
`ReleasePackages_GetReleasePackages` with `containingProductId` for manifest membership, and
`Products_GetDependencies` with `includeEnded: true` for dependencies.
