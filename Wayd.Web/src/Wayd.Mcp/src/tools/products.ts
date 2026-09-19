import type { McpToolDefinition } from '../types.js';

/**
 * The product catalog — a typed, self-referencing tree of everything the organization owns.
 *
 * A product's **type** carries the one consequential flag: `isReleasable`, which decides whether
 * versions can be cut against it. A product line is not releasable; a service is.
 *
 * Type, parent and status each have their own tool rather than being fields on the update, because
 * each carries a rule the domain enforces — versions block a retype, ancestry blocks a move, the
 * workflow constrains a status — and folding them into one call would hide which rule refused.
 */

/** Shared annotation: a human must approve before the record changes. */
const requiresConfirmation = {
  destructiveHint: true,
  readOnlyHint: false,
  idempotentHint: false,
} as const;

/** Shared annotation: reads only, safe to run without asking. */
const readsOnly = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
} as const;

const ID_ONLY = 'Product ID. This endpoint takes a UUID only, not a product key.';

const STATUS_CATEGORY = {
  type: 'array',
  items: { type: 'integer', enum: [0, 1, 2, 3] },
  description:
    'Filter by status category rather than status name, since names are per-organization: 0 Proposed, 1 Active, 2 Done, 3 Removed. Omit to return every status, including retired products.',
};

export const definitions: [string, McpToolDefinition][] = [

  ['Products_GetProducts', {
    name: 'Products_GetProducts',
    description: `List products from the catalog, ordered by name. Returns a **flat list, not a tree** — each product carries its parent as a reference, so build the hierarchy client-side.

Two filter behaviours worth knowing. \`parentId\` matches **direct children only**, not a whole subtree, and there is no way to ask for root nodes: omitting it returns everything rather than only roots. \`tagId\` is repeatable and combines as **AND, not OR** — passing a Platform tag and a Compliance tag returns products carrying both.

Each product reports \`isReleasable\`, flattened from its type, which is what decides whether versions can be cut against it.`,
    inputSchema: {"type":"object","properties":{"parentId":{"type":"string","format":"uuid","description":"Direct children of this product only — not the whole subtree, and not a way to ask for roots."},"productTypeId":{"type":"string","format":"uuid","description":"Only products of this type."},"statusCategory":STATUS_CATEGORY,"tagId":{"type":"array","items":{"type":"string","format":"uuid"},"description":"Only products carrying EVERY tag listed, not any of them."}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/products',
    executionParameters: [{"name":"parentId","in":"query"},{"name":"productTypeId","in":"query"},{"name":"statusCategory","in":"query"},{"name":"tagId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List products', ...readsOnly },
  }],

  ['Products_GetProduct', {
    name: 'Products_GetProduct',
    description: `Get one product in full — its type, parent, status, tags, external identifier, and whether its type allows versions to be cut against it. Accepts the product's UUID or its short key.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Product ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/products/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get product', ...readsOnly },
  }],

  ['Products_GetStatusHistory', {
    name: 'Products_GetStatusHistory',
    description: `Get a product's status change history, newest first. Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Product ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/products/{idOrKey}/status-history',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get product status history', ...readsOnly },
  }],

  ['Products_GetStatusOptions', {
    name: 'Products_GetStatusOptions',
    description: `Get the statuses a product can be moved to, in the order an administrator laid the lifecycle out rather than alphabetically. **Call this before Products_ChangeStatus**: that tool needs a status UUID, statuses are per-organization configuration with no fixed list, and any id outside this workflow is refused. The same list serves every product, so one call covers them all.`,
    inputSchema: {"type":"object","properties":{},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/products/status-options',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get product status options', ...readsOnly },
  }],

  ['Products_Create', {
    name: 'Products_Create',
    description: `Add a product to the catalog. The **type** decides what the node can do — most consequentially whether versions can be cut against it — and must be an active type. Omit the parent to create a root node.

The external identifier is the node's id in whatever system owns it: a repository, a pipeline, a registry package. Capturing it now makes reconciling against a later automated feed a matching problem rather than a re-authoring one.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"name":{"type":"string","description":"The product's name."},"description":{"type":"string","description":"An optional description."},"productTypeId":{"type":"string","format":"uuid","description":"The product type. Must be active. Use ProductTypes_GetProductTypes to find one, and note isReleasable decides whether versions can be cut against this product."},"parentId":{"type":"string","format":"uuid","description":"The parent product. Omit to create a root node."},"externalId":{"type":"string","description":"The node's identifier in the system that owns it — a repository, pipeline or registry package. Free text, max 256 characters, not required to be unique."}},"required":["name","productTypeId"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/products',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Create a product', ...requiresConfirmation },
  }],

  ['Products_Update', {
    name: 'Products_Update',
    description: `Update a product's name and description. **This is a whole-record overwrite of those two fields: an omitted description is cleared.**

Only the name and description. Type, parent, status, tags and the external link each have their own tool, because each carries a rule this one does not — and keeping the external link out means a rename cannot silently clear it.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"name":{"type":"string","description":"The product's name. Cannot be blank."},"description":{"type":"string","description":"Cleared when omitted."}},"required":["id","name"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/products/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update a product', ...requiresConfirmation },
  }],

  ['Products_Retype', {
    name: 'Products_Retype',
    description: `Change a product's type. **Refused if the product has versions and the new type is not releasable** — the versions already cut against it would be left hanging off a node that cannot carry them. The target type must be active, unless it is the type the product already has.

Note this is gated on *versions*, not releases: releasability asks whether an artifact can be cut against a node, and a release is an announcement that may sit under any node.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"productTypeId":{"type":"string","format":"uuid","description":"The new type. Must be active."}},"required":["id","productTypeId"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/products/{id}/type',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: "Change a product's type", ...requiresConfirmation },
  }],

  ['Products_Reparent', {
    name: 'Products_Reparent',
    description: `Move a product to a different parent, or to the root by omitting the parent. **Refused if the new parent is the product itself or one of its own descendants** — that would make a cycle. Any type may parent any other; there are no allowed-parent rules.

**Also refused when the move would put two products with an open dependency between them above and below one another** — that relationship is composition, which the tree already records. End the dependency first (\`Products_EndDependency\`).

The move is listed in the activity history of the product and of both its old and new parent.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"parentId":{"type":"string","format":"uuid","description":"The new parent. Omit to move the product to the root. Cannot be the product itself or any of its descendants."}},"required":["id"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/products/{id}/parent',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Move a product', ...requiresConfirmation },
  }],

  ['Products_ChangeStatus', {
    name: 'Products_ChangeStatus',
    description: `Move a product to a different status. **Call Products_GetStatusOptions first** — this needs a status UUID, and statuses are per-organization configuration rather than a fixed set. A status belonging to a different workflow is refused.

Any status in the product workflow is reachable from any other; there is no transition graph. The status name is frozen onto the history at the moment of the change, so renaming a status later does not rewrite what past entries read as.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"statusId":{"type":"string","format":"uuid","description":"The target status, from Products_GetStatusOptions. Must belong to the product workflow."}},"required":["id","statusId"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/products/{id}/status',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: "Change a product's status", ...requiresConfirmation },
  }],

  ['Products_LinkExternally', {
    name: 'Products_LinkExternally',
    description: `Set or clear a product's identifier in the system that owns it — a repository, a pipeline, a registry package. Omitting the value unlinks; there is no separate unlink tool. Free text, max 256 characters, and **not required to be unique**: two products may carry the same identifier.

This is separate from the ordinary update because it answers a different question — not what the product is called, but which external record it corresponds to — and keeping it apart stops a rename from silently clearing it.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"externalId":{"type":"string","description":"The identifier in the owning system. Max 256 characters. Omit to unlink."}},"required":["id"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/products/{id}/external-link',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Link a product externally', ...requiresConfirmation },
  }],

  ['Products_Tag', {
    name: 'Products_Tag',
    description: `Apply a tag to a product. Tags live in categories — axes such as Platform or Compliance — and a category decides whether a product may carry more than one of its tags.

**On a single-value axis this silently replaces the existing tag rather than refusing.** The call succeeds, and the tag the product previously carried on that axis is gone. Read the product first if that matters. On a multi-value axis the tag joins the others.

Both the tag and its category must be active. Applying a tag the product already carries succeeds and changes nothing.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"tagId":{"type":"string","format":"uuid","description":"The tag to apply, from ProductTagCategories_GetProductTagCategories. Both the tag and its category must be active."}},"required":["id","tagId"]},
    method: 'post',
    pathTemplate: '/api/product-management/products/{id}/tags/{tagId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"tagId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Tag a product', ...requiresConfirmation },
  }],

  ['Products_Untag', {
    name: 'Products_Untag',
    description: `Remove a tag from a product. Succeeds whether or not the product carried it, and an inactive tag can still be removed.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"tagId":{"type":"string","format":"uuid","description":"The tag to remove."}},"required":["id","tagId"]},
    method: 'delete',
    pathTemplate: '/api/product-management/products/{id}/tags/{tagId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"tagId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Remove a tag from a product', ...requiresConfirmation },
  }],

  ['Products_GetDependencies', {
    name: 'Products_GetDependencies',
    description: `Get what a product depends on (\`dependsOn\`) and what depends on it (\`usedBy\`). Accepts the product's UUID or its short key.

**Rolled up across everything beneath the product.** A link from one of its children to an outside product appears under \`dependsOn\`, and a link with both ends inside the product's own subtree appears in **neither** list — from outside, that is the product depending on itself. So reading a product line answers "what does this line rely on from elsewhere", and reading a leaf service answers it for that service alone.

Each entry carries both ends whichever list it is in — \`product\` (the one with the dependency) and \`dependsOnProduct\` — so a rolled-up row says which descendant it starts or lands on. \`productPath\` and \`dependsOnProductPath\` give each end's full ancestry, root first, down to its parent. Also \`strength\` (1 Hard: stops working without it; 2 Soft: degrades but keeps working), \`description\`, \`startsOn\`, and \`endsOn\` (null while it still holds).

Ended dependencies are left out unless \`includeEnded\` is true. An empty answer means no dependency has been recorded, not that none exists — they are entered by hand or by import.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Product ID (UUID) or its short key."},"includeEnded":{"type":"boolean","description":"Include dependencies that have ended. Defaults to false."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/products/{idOrKey}/dependencies',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"includeEnded","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get product dependencies', ...readsOnly },
  }],

  ['Products_AddDependency', {
    name: 'Products_AddDependency',
    description: `Record that a product depends on another. Returns the new dependency's id.

Record the **most specific product known** — the service that makes the call, not the platform it belongs to — since the read side rolls links up to every ancestor anyway. A product cannot depend on itself, nor on anything above or below it in the tree: that is composition, which the tree already records.

**Strength has no default and must be chosen deliberately**: 1 Hard if the product stops working without it, 2 Soft if it degrades or loses a feature but keeps working. Attributing a provider's downtime to its consumers reads this, so guessing either way misstates impact — ask if the person has not said.

A product holds at most one open dependency on another product, and a later one on the same pair cannot overlap an earlier one. \`startsOn\` defaults to today, may be backdated, and cannot be in the future.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"The product that has the dependency. "+ID_ONLY},"requestBody":{"type":"object","properties":{"dependsOnProductId":{"type":"string","format":"uuid","description":"The product depended on. Cannot be the product itself, nor anything above or below it in the tree."},"strength":{"type":"integer","enum":[1,2],"description":"1 Hard (stops working without it) or 2 Soft (degrades but keeps working). No default."},"description":{"type":"string","description":"What the dependency is for. Max 1024 characters."},"startsOn":{"type":"string","format":"date","description":"The day it began, as YYYY-MM-DD. Defaults to today; may be backdated; cannot be in the future."}},"required":["dependsOnProductId","strength"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/products/{id}/dependencies',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Add a product dependency', ...requiresConfirmation },
  }],

  ['Products_UpdateDependency', {
    name: 'Products_UpdateDependency',
    description: `Reword what a product's dependency is for. Only the description — strength and dates each have their own tool. **An omitted description is cleared.** Allowed on an ended dependency.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"The product that has the dependency. "+ID_ONLY},"dependencyId":{"type":"string","format":"uuid","description":"The dependency, from Products_GetDependencies."},"requestBody":{"type":"object","properties":{"description":{"type":"string","description":"What the dependency is for. Max 1024 characters. Cleared when omitted."}},"required":[]}},"required":["id","dependencyId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/products/{id}/dependencies/{dependencyId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"dependencyId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update a product dependency', ...requiresConfirmation },
  }],

  ['Products_ChangeDependencyStrength', {
    name: 'Products_ChangeDependencyStrength',
    description: `Change whether a product stops working without one it depends on. **This ends the current dependency and records a new one** with the new strength, so the history keeps when each strength held — and the response is the id of the dependency **now open**, which replaces the one you passed. Unchanged when the strength already matches.

\`changedOn\` is the first day the new strength holds; the current dependency ends the day before. Defaults to today, must be after the day the dependency started, and cannot be in the future.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"The product that has the dependency. "+ID_ONLY},"dependencyId":{"type":"string","format":"uuid","description":"The open dependency, from Products_GetDependencies."},"requestBody":{"type":"object","properties":{"strength":{"type":"integer","enum":[1,2],"description":"1 Hard (stops working without it) or 2 Soft (degrades but keeps working)."},"changedOn":{"type":"string","format":"date","description":"First day the new strength holds, as YYYY-MM-DD. Defaults to today."}},"required":["strength"]}},"required":["id","dependencyId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/products/{id}/dependencies/{dependencyId}/strength',
    executionParameters: [{"name":"id","in":"path"},{"name":"dependencyId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Change a product dependency strength', ...requiresConfirmation },
  }],

  ['Products_EndDependency', {
    name: 'Products_EndDependency',
    description: `Record that a product stopped depending on another. **The dependency is kept** and still counts for the period it held — this is the right tool when a dependency was true and no longer is. Use \`Products_RemoveDependency\` only for one that was never true.

\`endsOn\` is the last day it held: defaults to today, may be the day it started, and cannot be in the future.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"The product that has the dependency. "+ID_ONLY},"dependencyId":{"type":"string","format":"uuid","description":"The dependency, from Products_GetDependencies."},"requestBody":{"type":"object","properties":{"endsOn":{"type":"string","format":"date","description":"The last day it held, as YYYY-MM-DD. Defaults to today."}},"required":[]}},"required":["id","dependencyId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/products/{id}/dependencies/{dependencyId}/end',
    executionParameters: [{"name":"id","in":"path"},{"name":"dependencyId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'End a product dependency', ...requiresConfirmation },
  }],

  ['Products_RemoveDependency', {
    name: 'Products_RemoveDependency',
    description: `**Delete** a dependency that was recorded by mistake. Requires a reason saying why it was never true. Not for a dependency that stopped — \`Products_EndDependency\` keeps the history of when it held, and this erases it.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"The product that has the dependency. "+ID_ONLY},"dependencyId":{"type":"string","format":"uuid","description":"The dependency, from Products_GetDependencies."},"requestBody":{"type":"object","properties":{"reason":{"type":"string","description":"Why the dependency was never true. Required, max 1024 characters."}},"required":["reason"]}},"required":["id","dependencyId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/products/{id}/dependencies/{dependencyId}/remove',
    executionParameters: [{"name":"id","in":"path"},{"name":"dependencyId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Remove a product dependency', ...requiresConfirmation },
  }],

  ['Products_Delete', {
    name: 'Products_Delete',
    description: `Permanently delete a product. **This is a hard delete, not a retirement.** Consider changing the status instead if the product merely stopped being current. It takes its status history with it; its activity history is kept.

Refused while anything depends on it, each with its own reason: it has **child products** (move or remove them first), it has **versions**, it appears in a **release package manifest**, or it is named on **either end of a product dependency**. The manifest check is separate from versions because a carried-forward manifest line often names a product with no version row at all. The dependency check counts **ended** dependencies too — deleting the product would erase the record of what relied on it.

Tag assignments are removed with the product. Status does not block deletion.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY}},"required":["id"]},
    method: 'delete',
    pathTemplate: '/api/product-management/products/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Delete a product', ...requiresConfirmation },
  }],

];
