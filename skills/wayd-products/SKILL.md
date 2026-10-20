---
name: wayd-products
description: Read before calling the Wayd MCP tools named Products_*, ProductTypes_*, ProductTagCategories_*, DeploymentEnvironments_*, DeliveryMetrics_* or DeliveryOverview_*. Guides agents working with the Wayd product catalog via the Wayd MCP server — the typed tree of products, platforms, services and tools an organization owns, plus product dependencies, product types, tags, deployment environments, what is running where, and delivery measures. Use when looking up or creating products, moving one to a different parent, changing a product's type or status, tagging products, deleting one, recording or reading what a product depends on (or what would be hit if it went down), defining or retiring deployment environments, asking what is running in an environment, reading delivery measures or recent shipping activity, or managing the product types and tag axes themselves. For releases, versions, packages and deployments, use the wayd-delivery skill instead.
---

# Wayd Products (Catalog / Dependencies / Types / Tags / Environments / Metrics)

For releases, versions, packages and deployments, use the **wayd-delivery** skill instead.

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

## Tool map

| Area | Read | Change |
| --- | --- | --- |
| Products | `Products_GetProducts`, `Products_GetProduct`, `Products_GetStatusOptions`, `Products_GetStatusHistory`, `Products_GetActivities` | `Products_Create`, `Products_Update` (name and description only), `Products_Retype`, `Products_Reparent`, `Products_ChangeStatus`, `Products_LinkExternally` (external identifier), `Products_Tag`, `Products_Untag`, `Products_Delete` |
| Dependencies | `Products_GetDependencies` | `Products_AddDependency`, `Products_UpdateDependency`, `Products_ChangeDependencyTerms`, `Products_EndDependency`, `Products_RemoveDependency` |
| Product types | `ProductTypes_GetProductTypes` | `ProductTypes_Create`, `ProductTypes_Update`, `ProductTypes_SetActive`, `ProductTypes_Delete` |
| Tag axes and tags | `ProductTagCategories_GetProductTagCategories` | `ProductTagCategories_Create`, `ProductTagCategories_Update`, `ProductTagCategories_SetActive`, `ProductTagCategories_Delete`, `ProductTagCategories_Reorder`, `ProductTagCategories_AddTag`, `ProductTagCategories_RenameTag`, `ProductTagCategories_SetTagActive`, `ProductTagCategories_DeleteTag` |
| Environments | `DeploymentEnvironments_GetDeploymentEnvironments`, `DeploymentEnvironments_GetRollout` | `DeploymentEnvironments_Create`, `DeploymentEnvironments_Update`, `DeploymentEnvironments_SetActive`, `DeploymentEnvironments_Delete` |
| Measures | `DeliveryMetrics_GetDeliveryMetrics`, `DeliveryOverview_GetDeliveryOverview`, `DeliveryOverview_GetRecentDeliveryEvents` | — |

## Reference

Read the matching file before working in these areas. The rules on this page still apply there.

| File | Read it when |
| --- | --- |
| [reference/deleting-a-product.md](reference/deleting-a-product.md) | Deleting a product, purging its history, or answering why one cannot be deleted |
| [reference/dependencies.md](reference/dependencies.md) | Reading, recording, changing, ending or removing product dependencies, or answering what would be hit if a product went down |
| [reference/configuration.md](reference/configuration.md) | Creating, changing, deactivating or deleting product types, tag axes or tags |
| [reference/environments-metrics.md](reference/environments-metrics.md) | Defining, changing or retiring deployment environments, asking what is running where, or reading delivery measures and recent shipping activity |

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
| `Products_Retype` | The product has versions and the new type is not releasable, or the new type is inactive |
| `Products_Reparent` | The new parent is the product itself or one of its descendants, or the move would put two products with an open dependency above and below one another (end the dependency first) |
| `Products_ChangeStatus` | The status id does not come from `Products_GetStatusOptions`, or the workflow does not allow the move from the product's current status |
| `Products_Delete` | See [reference/deleting-a-product.md](reference/deleting-a-product.md) |

---

## Tagging a single-value axis silently replaces

Before `Products_Tag`, check the axis's `allowsMany` in `ProductTagCategories_GetProductTagCategories`.
Where it is false, the call **succeeds and removes the tag the product already carried on that axis** —
it never refuses. If the existing value matters, read it with `Products_GetProduct` and tell the user
what will be replaced. An inactive tag, or a tag on an inactive axis, is refused.

---

## Deleting a product

`Products_Delete` is a **hard delete**. If the product has merely stopped being current, change its
status instead. Before deleting, read [reference/deleting-a-product.md](reference/deleting-a-product.md).

---

## Statuses and history

`Products_ChangeStatus` takes a status id from `Products_GetStatusOptions`. Statuses are
per-organization, and the workflow may restrict which status can move to which: a move it does not
allow is refused, naming both statuses. Pass the product's id as `productId` to
`Products_GetStatusOptions` to get only the moves allowed from its current status, and offer the user
those; without it you get every status in the workflow.

For what changed on a product, `Products_GetStatusHistory` covers status moves and
`Products_GetActivities` covers everything else — details, type, parent, tags, external link. Each
activity entry's `payload` carries the value before and after. A `Baseline` entry marks where tracking
began for a product that already existed; nothing before it is recorded. Check `hasNextPage` before
concluding something never happened.

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

`Products_Delete` names the reason. To check ahead, run the four checks in [reference/deleting-a-product.md](reference/deleting-a-product.md).
