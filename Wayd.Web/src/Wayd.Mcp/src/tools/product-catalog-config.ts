import type { McpToolDefinition } from '../types.js';

/**
 * The two lookups the product tools depend on: product types and tag categories.
 *
 * Both are administrator-managed configuration. Only the reads are exposed here, because they are
 * what a caller needs to create or tag a product — a type id and a tag id cannot be guessed, and
 * both are per-organization. Managing the configuration itself stays in the UI for now.
 */

/** Shared annotation: reads only, safe to run without asking. */
const readsOnly = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
} as const;

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

];
