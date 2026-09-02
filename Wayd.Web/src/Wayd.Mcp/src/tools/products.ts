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
    description: `Move a product to a different parent, or to the root by omitting the parent. **Refused if the new parent is the product itself or one of its own descendants** — that would make a cycle. Any type may parent any other; there are no allowed-parent rules.`,
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

  ['Products_Delete', {
    name: 'Products_Delete',
    description: `Permanently delete a product. **This is a hard delete, not a retirement** — unlike everything in delivery, where records are withdrawn and kept. Consider changing the status instead if the product merely stopped being current.

Refused while anything depends on it, each with its own reason: it has **child products** (move or remove them first), it has **versions**, or it appears in a **release package manifest**. The third is checked separately from versions because a carried-forward manifest line often names a product with no version row at all.

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
