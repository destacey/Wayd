import type { McpToolDefinition } from '../types.js';

/**
 * Product types and tag categories — the configuration the catalog is built from.
 *
 * Administrator-managed and organization-wide. The reads are what a caller needs before creating or
 * tagging a product, since a type id and a tag id cannot be guessed.
 *
 * Two rules run through everything here. **Seeded (system) records cannot be modified or deleted**,
 * though they can be deactivated — an organization that does not ship libraries should be able to
 * hide the type without the seeder recreating it. And **nothing in use can be deleted**: the answer
 * is always to deactivate, which stops new use while leaving existing records resolvable.
 */

/** Shared annotation: a human must approve before the configuration changes. */
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

const SYSTEM_AND_USE =
  'Seeded system records cannot be modified or deleted, and a record in use cannot be deleted — deactivate it instead, which stops new use without breaking what already refers to it.';

export const definitions: [string, McpToolDefinition][] = [

  ['ProductTypes_GetProductTypes', {
    name: 'ProductTypes_GetProductTypes',
    description: `List the product types an organization recognises, in the order an administrator arranged them. **Call this before Products_Create or Products_Retype**, which both need a type UUID.

The flag that matters is \`isReleasable\`: it decides whether versions can be cut against products of this type. A product line or a platform is typically not releasable; a service, application or library is. It also gates retyping — a product with versions cannot be moved to a type that is not releasable.

Inactive types cannot be assigned to a product, though a product already carrying one keeps it.`,
    inputSchema: {"type":"object","properties":{},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/product-types',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List product types', ...readsOnly },
  }],

  ['ProductTagCategories_GetProductTagCategories', {
    name: 'ProductTagCategories_GetProductTagCategories',
    description: `List the tag categories and the tags in each. **Call this before Products_Tag**, which needs a tag UUID.

A category is an axis — Platform, Tech Stack, Compliance — and its \`allowsMany\` flag decides how tagging behaves. On an axis where \`allowsMany\` is false, applying a second tag **silently replaces** the first rather than refusing, so check this before tagging if the existing value matters.

Only active tags in active categories can be applied.`,
    inputSchema: {"type":"object","properties":{},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/product-tag-categories',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List product tag categories', ...readsOnly },
  }],

  ['ProductTypes_Create', {
    name: 'ProductTypes_Create',
    description: `Define a product type. **\`isReleasable\` is the consequential field** — it decides whether versions can be cut against products of this type, and it is what a product line or platform sets to false. Names must be unique. \`order\` is presentation only and implies no hierarchy.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"name":{"type":"string","description":"The type's name. Must be unique."},"description":{"type":"string","description":"An optional description."},"isReleasable":{"type":"boolean","description":"Whether versions can be cut against products of this type. False for grouping nodes such as a product line or platform."},"order":{"type":"integer","description":"Display position. Presentation only — it implies no hierarchy."}},"required":["name","isReleasable","order"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/product-types',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Create a product type', ...requiresConfirmation },
  }],

  ['ProductTypes_Update', {
    name: 'ProductTypes_Update',
    description: `Update a product type. **This is a whole-record overwrite, and \`isReleasable\` is required — so renaming a type means resending its current releasability, and sending the wrong value silently changes whether versions can be cut against every product of this type.** Read the type first.

Refused on a seeded system type. Names must stay unique.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Product type ID. This endpoint takes a UUID only."},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"name":{"type":"string","description":"The type's name. Must be unique."},"description":{"type":"string","description":"Cleared when omitted."},"isReleasable":{"type":"boolean","description":"Required. Resend the type's current value unless you intend to change whether versions can be cut against products of this type."},"order":{"type":"integer","description":"Display position."}},"required":["id","name","isReleasable","order"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/product-types/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update a product type', ...requiresConfirmation },
  }],

  ['ProductTypes_SetActive', {
    name: 'ProductTypes_SetActive',
    description: `Take a product type out of use, or put it back. A deactivated type cannot be assigned to a product, but products already using it keep resolving it — which is why this is deactivation rather than deletion.

Unlike editing, this **is** allowed on a seeded system type: an organization that does not ship libraries should be able to hide that type without the seeder recreating it.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Product type ID. This endpoint takes a UUID only."},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"isActive":{"type":"boolean","description":"false takes the type out of use; true puts it back."}},"required":["id","isActive"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/product-types/{id}/active',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Activate or deactivate a product type', ...requiresConfirmation },
  }],

  ['ProductTypes_Delete', {
    name: 'ProductTypes_Delete',
    description: `Delete a product type. ${SYSTEM_AND_USE} A type is "in use" when any product carries it, so in practice this only removes a type created by mistake and never assigned.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Product type ID. This endpoint takes a UUID only."}},"required":["id"]},
    method: 'delete',
    pathTemplate: '/api/product-management/product-types/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Delete a product type', ...requiresConfirmation },
  }],

  ['ProductTagCategories_Create', {
    name: 'ProductTagCategories_Create',
    description: `Create a tag category — an axis such as Platform, Tech Stack or Compliance. **\`allowsMany\` cannot be changed afterwards**, so choose it deliberately: it decides whether a product may carry several tags on this axis, or whether applying a second one silently replaces the first. Names must be unique. The category is created empty; add tags with ProductTagCategories_AddTag.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"name":{"type":"string","description":"The axis name. Must be unique."},"description":{"type":"string","description":"An optional description."},"allowsMany":{"type":"boolean","description":"Whether a product may carry several tags from this axis. Cannot be changed after creation. False means applying a second tag silently replaces the first."}},"required":["name","allowsMany"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/product-tag-categories',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Create a tag category', ...requiresConfirmation },
  }],

  ['ProductTagCategories_Update', {
    name: 'ProductTagCategories_Update',
    description: `Rename a tag category or change its description. **An omitted description is cleared.** \`allowsMany\` is not here and cannot be changed after creation. Refused on a seeded system category. Names must stay unique.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Tag category ID. This endpoint takes a UUID only."},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"name":{"type":"string","description":"The axis name. Must be unique."},"description":{"type":"string","description":"Cleared when omitted."}},"required":["id","name"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/product-tag-categories/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update a tag category', ...requiresConfirmation },
  }],

  ['ProductTagCategories_SetActive', {
    name: 'ProductTagCategories_SetActive',
    description: `Take a tag category out of use, or put it back. Tags on an inactive category cannot be applied to a product, though products already carrying them keep them and can still have them removed.

As with product types, this **is** allowed on a seeded system category, unlike editing.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Tag category ID. This endpoint takes a UUID only."},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"isActive":{"type":"boolean","description":"false takes the axis out of use; true puts it back."}},"required":["id","isActive"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/product-tag-categories/{id}/active',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Activate or deactivate a tag category', ...requiresConfirmation },
  }],

  ['ProductTagCategories_Delete', {
    name: 'ProductTagCategories_Delete',
    description: `Delete a tag category and its tags. ${SYSTEM_AND_USE} A category counts as in use when any product is tagged along it, so this only removes an axis created by mistake and never applied.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Tag category ID. This endpoint takes a UUID only."}},"required":["id"]},
    method: 'delete',
    pathTemplate: '/api/product-management/product-tag-categories/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Delete a tag category', ...requiresConfirmation },
  }],

  ['ProductTagCategories_Reorder', {
    name: 'ProductTagCategories_Reorder',
    description: `Put the tag categories in a given order. **The list must name every category exactly once** — a partial list is refused, so read them all first and send the complete sequence. Ordering is presentation only.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"orderedCategoryIds":{"type":"array","items":{"type":"string","format":"uuid"},"description":"Every tag category id, exactly once, in the order they should appear."}},"required":["orderedCategoryIds"]}},"required":["requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/product-tag-categories/reorder',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Reorder tag categories', ...requiresConfirmation },
  }],

  ['ProductTagCategories_AddTag', {
    name: 'ProductTagCategories_AddTag',
    description: `Add a tag to a category. Tag names must be unique within their axis, though the same name may appear on different axes. Refused on a seeded system category.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Tag category ID. This endpoint takes a UUID only."},"requestBody":{"type":"object","properties":{"name":{"type":"string","description":"The tag's name. Must be unique within this axis."},"description":{"type":"string","description":"An optional description."}},"required":["name"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/product-tag-categories/{id}/tags',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Add a tag', ...requiresConfirmation },
  }],

  ['ProductTagCategories_RenameTag', {
    name: 'ProductTagCategories_RenameTag',
    description: `Rename a tag or change its description. **An omitted description is cleared.** The tag must belong to the category named in the path. Names must stay unique within the axis. Refused on a seeded system category.

Renaming does not rewrite history: a product carrying the tag simply reports the new name.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Tag category ID. This endpoint takes a UUID only."},"tagId":{"type":"string","format":"uuid","description":"The tag to rename. Must belong to this category."},"requestBody":{"type":"object","properties":{"name":{"type":"string","description":"The tag's new name. Must be unique within this axis."},"description":{"type":"string","description":"Cleared when omitted."}},"required":["name"]}},"required":["id","tagId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/product-tag-categories/{id}/tags/{tagId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"tagId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Rename a tag', ...requiresConfirmation },
  }],

  ['ProductTagCategories_SetTagActive', {
    name: 'ProductTagCategories_SetTagActive',
    description: `Take a tag out of use, or put it back. An inactive tag cannot be applied to a product, though products already carrying it keep it and can still have it removed. The tag must belong to the category named in the path.

**Refused on a seeded system category, and here there is no fallback** — unlike a category or a product type, an individual system tag can be neither modified nor retired. Deactivate the whole axis with \`ProductTagCategories_SetActive\` if it should stop being used.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Tag category ID. This endpoint takes a UUID only."},"tagId":{"type":"string","format":"uuid","description":"The tag to activate or deactivate. Must belong to this category."},"requestBody":{"type":"object","properties":{"isActive":{"type":"boolean","description":"false takes the tag out of use; true puts it back."}},"required":["isActive"]}},"required":["id","tagId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/product-tag-categories/{id}/tags/{tagId}/active',
    executionParameters: [{"name":"id","in":"path"},{"name":"tagId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Activate or deactivate a tag', ...requiresConfirmation },
  }],

];
