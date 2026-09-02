---
name: wayd-products
description: Guides agents working with the Wayd product catalog via the Wayd MCP server — the typed tree of products, platforms, services and tools an organization owns, plus product types, tags, deployment environments and delivery metrics. Use when looking up or creating products, moving one to a different parent, changing a product's type or status, tagging products, deleting one, defining or retiring deployment environments, reading delivery measures, or managing the product types and tag axes themselves.
---

# Wayd Products (Catalog / Types / Tags / Environments / Metrics)

## When to use

- Finding what the organization owns, and how it fits together
- Creating a product, or moving one to a different parent
- Changing a product's type or status, or tagging it
- Deleting a product, and understanding why one refuses to delete
- Defining, updating or retiring deployment environments
- Reading delivery measures over a window
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
| `Products_Reparent` | The new parent is the product itself, or one of its descendants |
| `Products_ChangeStatus` | The status id does not belong to the product workflow |
| `Products_Delete` | It has children, has versions, or appears in a package manifest |

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

It refuses while anything depends on it, and each reason is distinct — children, versions, or
appearing in a release package manifest. That last one is checked separately because a
carried-forward manifest line often names a product that has no version row at all.

---

## Managing the configuration itself

Types and tag categories are administrator-managed, organization-wide configuration. Two rules run
through every tool that changes them.

**Seeded system records cannot be modified or deleted — but they can be deactivated.** The guard
protects what a seeded record *means*: its name, its tags, whether it takes many. Whether the
organization currently uses it is a different question, so `ProductTypes_SetActive` and
`ProductTagCategories_SetActive` work on system records. An organization that does not ship libraries
hides that type rather than fighting the seeder.

The exception is at the tag level. `AddTag`, `RenameTag` and `SetTagActive` are **all** refused on a
system category, deactivation included — there is no per-tag fallback. Retire the whole axis instead.

**Nothing in use can be deleted.** A type is in use when any product carries it; an axis is in use
when any product is tagged along it. Both refuse with "Deactivate it instead", so in practice delete
only removes something created by mistake and never applied.

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

---

## Environments and metrics

An **environment** is a named deployment target, defined once for the organization. Each carries a
**category** — Development, Testing, Staging, Production — and a ring order for progressive rollout.

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

`DeliveryMetrics_GetDeliveryMetrics` returns deployment frequency and change failure rate, plus an
`unavailable` list naming what could not be computed and why. **Read that list** rather than treating
a missing measure as zero. Change failure rate is a proxy: a pipeline run that failed before reaching
production is a failure that was *prevented*, while a real change failure succeeded and then broke
something — which the pipeline cannot know. Report it as approximate.

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

### Answering "why can't I delete this?"

`Products_Delete` names the reason. To check ahead: `Products_GetProducts` with `parentId` for
children, `Versions_GetVersions` with `productId` for versions, and
`ReleasePackages_GetReleasePackages` with `containingProductId` for manifest membership.
