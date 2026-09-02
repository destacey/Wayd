import type { McpToolDefinition } from '../types.js';

/**
 * Release packages — several component versions shipped as one coordinated unit.
 *
 * A package is what moved through environments together: the weekly pipeline run that deploys
 * fifteen services at once. **Where a package exists it is the unit of deployment**, not its
 * components — one run shipping fifteen services must count as one deployment, or deployment
 * frequency measures how finely the estate is subdivided rather than how often it ships.
 *
 * Not to be confused with a Release Train, which is SAFe's organizational construct and is modelled
 * in Wayd as a Team of Teams.
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

const ID_ONLY = 'Package ID. This endpoint takes a UUID only, not a package key.';

/** One manifest line. Shared by assemble and replace, which take the same component shape. */
const componentSchema = {
  type: 'array',
  items: {
    type: 'object',
    properties: {
      productId: { type: 'string', format: 'uuid', description: 'The component this line is for. A component may appear only once in a manifest.' },
      versionId: { type: 'string', format: 'uuid', description: "The version record this line came from, where one exists. Optional: a carried-forward component often names a version that was never cut in Wayd, in which case send only the version string. Naming the record is what lets a release know this version is already inside a package — a line without it covers nothing." },
      version: { type: 'string', description: 'The component version as text. Free text, never parsed. Required even when versionId is given.' },
      kind: { type: 'string', enum: ['Changed', 'CarriedForward'], description: 'Whether the component changed in this package or shipped unchanged. Recording the carried-forward lines is what lets a reader reconstruct what was in the box, not merely what moved.' },
    },
    required: ['productId', 'version', 'kind'],
  },
  description: 'Every component version in this package.',
};

export const definitions: [string, McpToolDefinition][] = [

  ['ReleasePackages_GetReleasePackages', {
    name: 'ReleasePackages_GetReleasePackages',
    description: `List release packages — coordinated shipments such as \`WAYD-2026.09.1\`. Unreleased first, then the most recently released. Use \`containingVersionId\` to answer "what did this version ship in?": a version carries no pointer back to its package, so membership is read from the manifest side rather than duplicated.`,
    inputSchema: {"type":"object","properties":{"statusCategory":{"type":"array","items":{"type":"integer"},"description":"Filter by status category rather than status name, since names are per-organization."},"containingProductId":{"type":"string","format":"uuid","description":"Only packages whose manifest names any version of this product."},"containingVersionId":{"type":"string","format":"uuid","description":"Only packages whose manifest names this specific version record. This is how to answer \"what did this version ship in?\"."}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/release-packages',
    executionParameters: [{"name":"statusCategory","in":"query"},{"name":"containingProductId","in":"query"},{"name":"containingVersionId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List release packages', ...readsOnly },
  }],

  ['ReleasePackages_GetReleasePackage', {
    name: 'ReleasePackages_GetReleasePackage',
    description: `Get one package in full, including its complete manifest — every component version it shipped, and whether each changed or was carried forward. Accepts the package's UUID or its short key.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Package ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/release-packages/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get release package', ...readsOnly },
  }],

  ['ReleasePackages_GetStatusHistory', {
    name: 'ReleasePackages_GetStatusHistory',
    description: `Get a package's status change history, newest first. Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Package ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/release-packages/{idOrKey}/status-history',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get package status history', ...readsOnly },
  }],

  ['ReleasePackages_Assemble', {
    name: 'ReleasePackages_Assemble',
    description: `Assemble a package and its manifest together. **A package ships at least one component**, so the manifest is authored here rather than added afterwards — an empty one is refused. The package is versioned in its own right, separately from anything inside it. A component may appear only once in a manifest, though the same component version may appear in several different packages.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"version":{"type":"string","description":"The package's own version — WAYD-2026.09.1. Free text, never parsed. Distinct from any component's version."},"name":{"type":"string","description":"An optional human name for the package."},"targetDate":{"type":"string","format":"date","description":"When the package is expected to ship. Format YYYY-MM-DD."},"components":componentSchema},"required":["version","components"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/release-packages',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Assemble a release package', ...requiresConfirmation },
  }],

  ['ReleasePackages_SetManifest', {
    name: 'ReleasePackages_SetManifest',
    description: `Replace a package's manifest as a whole. **This is a whole-set replacement: a line left out is removed from the package.** Components carry no identifier of their own and cannot be addressed individually, so read the package first and send back every line it should end up with. The manifest closes once the package is released or withdrawn — what was in the box cannot be rewritten after the box shipped.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"components":componentSchema},"required":["components"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/release-packages/{id}/manifest',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Replace a package manifest', ...requiresConfirmation },
  }],

  ['ReleasePackages_MarkReleased', {
    name: 'ReleasePackages_MarkReleased',
    description: `Record that a package shipped, and close its manifest. **A package with an empty manifest cannot be released.** This is not announcing anything to customers — that is Releases_MarkReleased on a release that carries this package.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"releasedDate":{"type":"string","format":"date","description":"The date it shipped. Format YYYY-MM-DD."}},"required":["releasedDate"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/release-packages/{id}/release',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Mark a package released', ...requiresConfirmation },
  }],

  ['ReleasePackages_Withdraw', {
    name: 'ReleasePackages_Withdraw',
    description: `Pull a package. Terminal, and it closes the manifest. A released package can still be withdrawn; a withdrawn one cannot be released. **The package itself is never deleted** — deployments point at it, and erasing it would take the record of what reached an environment with it.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"reason":{"type":"string","description":"Why it was pulled. Optional — recorded on the status transition where given."}},"required":[]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/release-packages/{id}/withdraw',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Withdraw a package', ...requiresConfirmation },
  }],

];
