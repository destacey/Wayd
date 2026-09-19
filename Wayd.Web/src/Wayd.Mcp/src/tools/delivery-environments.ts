import type { McpToolDefinition } from '../types.js';

/**
 * Deployment environments, what is running in them, and the delivery measures computed over them.
 *
 * These belong together because the measures depend on the environments: every production-scoped
 * figure counts on an environment's **category**, not its name. Pipeline environment names are free
 * text and endlessly varied — `prod`, `Production`, `prd`, `live`, somebody's `prod-canary` — so the
 * category is what makes "deployments to production" answerable at all.
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

const CATEGORY = {
  type: 'integer',
  enum: [1, 2, 3, 4, 5],
  description:
    'Environment category: 1 Development (used while building), 2 Testing (verifying a change before release), 3 Staging (production-like final validation), 4 Production (live, serving real users — the denominator for delivery metrics), 5 Other (none of those — a sandbox, a demo, a training environment; never counted as production).',
};

export const definitions: [string, McpToolDefinition][] = [

  ['DeploymentEnvironments_GetDeploymentEnvironments', {
    name: 'DeploymentEnvironments_GetDeploymentEnvironments',
    description: `List the deployment environments defined for the organization. Environments are defined once and any product can deploy into any of them. Each carries a **category** and a **ring order**, so progressive rollout is representable. Filter by category rather than matching on names, which are free text.`,
    inputSchema: {"type":"object","properties":{"isActive":{"type":"boolean","description":"Only active environments (true) or only retired ones (false). Omit for all. Only an active environment is accepted as a deployment target."},"category":CATEGORY},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/deployment-environments',
    executionParameters: [{"name":"isActive","in":"query"},{"name":"category","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List deployment environments', ...readsOnly },
  }],

  ['DeploymentEnvironments_Create', {
    name: 'DeploymentEnvironments_Create',
    description: `Define a deployment environment. The **category** is what every production-scoped measure counts on, so set it deliberately rather than relying on the name. **Ring order** places the environment in a progressive rollout sequence — lower rings are reached first.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"name":{"type":"string","description":"The environment's name, as your pipeline calls it."},"category":CATEGORY,"ringOrder":{"type":"integer","description":"Position in a progressive rollout. Lower rings are reached first."}},"required":["name","category","ringOrder"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/deployment-environments',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Create a deployment environment', ...requiresConfirmation },
  }],

  ['DeploymentEnvironments_Update', {
    name: 'DeploymentEnvironments_Update',
    description: `Update an environment's name, category or ring order. **This is a whole-record overwrite — send every field, including ones you are not changing.**

Changing the category is not an ordinary edit: each deployment **froze** the category of the environment it went into, so reclassifying changes where *future* deployments count and leaves past ones exactly as they were. A staging environment promoted to production does not retroactively inflate deployment frequency. Refused on a retired environment.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Environment ID. This endpoint takes a UUID only."},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"name":{"type":"string","description":"The environment's name."},"category":CATEGORY,"ringOrder":{"type":"integer","description":"Position in a progressive rollout."}},"required":["id","name","category","ringOrder"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/deployment-environments/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update a deployment environment', ...requiresConfirmation },
  }],

  ['DeploymentEnvironments_SetActive', {
    name: 'DeploymentEnvironments_SetActive',
    description: `Retire an environment or reinstate one. **Retire rather than delete** unless the user explicitly wants the history gone: deleting an environment takes every deployment into it with it, while retiring keeps them.

A retired environment is no longer offered as a deployment target, but it and every deployment recorded against it are kept, and those deployments keep counting toward the measures they already count toward. Editing and reclassifying are refused on a retired environment, so reinstate it first if you need to change it.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Environment ID. This endpoint takes a UUID only."},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"isActive":{"type":"boolean","description":"false retires the environment; true reinstates it."}},"required":["id","isActive"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/deployment-environments/{id}/active',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Retire or reinstate an environment', ...requiresConfirmation },
  }],

  ['DeploymentEnvironments_Delete', {
    name: 'DeploymentEnvironments_Delete',
    description: `Permanently delete an environment **and every deployment into it**, with their status history. The delivery measures and rollout stop counting those deployments. For an environment defined by mistake, or when the user asks to purge history — otherwise retire it with \`DeploymentEnvironments_SetActive\`, which keeps them. The \`deploymentCount\` from \`DeploymentEnvironments_GetDeploymentEnvironments\` says how many would go; state it before confirming. Needs the environment Delete permission, and — when the environment has any deployments — the delivery Delete permission as well.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Environment ID. This endpoint takes a UUID only."}},"required":["id"]},
    method: 'delete',
    pathTemplate: '/api/product-management/deployment-environments/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Delete a deployment environment', ...requiresConfirmation },
  }],

  ['DeploymentEnvironments_GetRollout', {
    name: 'DeploymentEnvironments_GetRollout',
    description: `Get what is running in each environment right now, in rollout order (lowest ring first). Answers "what version of X is in production?" and "how far has this got?" in one call.

Each environment lists \`running\`: **one entry per product**, never per package. A package deployment is expanded into its manifest and each component takes its own product's slot, so two successive bundles carrying the same component do not both show as running. Each entry is the **latest deployment that succeeded and was not rolled back** — a failed attempt leaves its predecessor running, and a rollback takes its own deployment out and leaves the one before it in. So "what is here" and "what happened last" differ: check \`hasFailedAttemptSince\`, true when a later deployment touching that product failed or was rolled back there.

Each entry carries \`versionLabel\` (always set, free text), \`version\` (null for a packaged component never cut as a version in Wayd), \`package\` (set when it arrived inside one), \`artifactId\`, \`deployedAt\`, and the \`deploymentId\`/\`deploymentKey\` it was read from. An empty \`running\` list means nothing has ever succeeded into that environment — a complete answer, not missing data.`,
    inputSchema: {"type":"object","properties":{"includeInactive":{"type":"boolean","description":"Include retired environments. Defaults to false — nothing runs in a retired environment, though its history is kept."}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/deployment-environments/rollout',
    executionParameters: [{"name":"includeInactive","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get environment rollout', ...readsOnly },
  }],

  ['DeliveryMetrics_GetDeliveryMetrics', {
    name: 'DeliveryMetrics_GetDeliveryMetrics',
    description: `Get the delivery measures over a window, computed from deployment records. Returns **deployment frequency** and **change failure rate**, plus an \`unavailable\` list naming the measures this module cannot compute yet and why — read that list rather than treating a missing measure as zero.

Two caveats worth carrying into any answer. **Production-scoped measures depend on environment categories**, not names, so a deployment into an environment whose category is not Production does not count toward deployment frequency. And **change failure rate is a proxy**: a pipeline run that failed before reaching production is a failure that was *prevented*, while a real change failure is a deployment that succeeded and then broke something — which the pipeline has no way to know. Report it as approximate rather than as the metric.`,
    inputSchema: {"type":"object","properties":{"from":{"type":"string","format":"date-time","description":"Start of the window, inclusive. A full ISO-8601 instant, not a plain date — 2026-08-01T00:00:00Z."},"to":{"type":"string","format":"date-time","description":"End of the window, inclusive. A full ISO-8601 instant, not a plain date — 2026-09-02T23:59:59Z."},"productId":{"type":"string","format":"uuid","description":"Scope the measures to one product."}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/delivery-metrics',
    executionParameters: [{"name":"from","in":"query"},{"name":"to","in":"query"},{"name":"productId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get delivery metrics', ...readsOnly },
  }],

  ['DeliveryOverview_GetDeliveryOverview', {
    name: 'DeliveryOverview_GetDeliveryOverview',
    description: `Get version activity over a window — what was **cut and shipped**, measured from versions rather than deployments. Separate from \`DeliveryMetrics_GetDeliveryMetrics\`, which measures deployments: a product that ships continuously has a healthy cadence here whether or not its pipeline is recorded in Wayd.

Scoping to a product covers **that node and everything beneath it**, so a product line rolls up its children. Omit \`productId\` for the whole catalog.

Returns:
- \`scope\` — the selected product (null for the whole catalog) and \`releasableNodeCount\`, the denominator for judging the rest: three releases a week means something different across four products than across forty.
- \`frequency\` — \`count\`, \`windowDays\`, \`perWeek\`, and \`previousPerWeek\` for the equal window just before. **A null previous value means nothing shipped then** — the change is unknowable, so do not report it as a rise from zero.
- \`cutToReleased\` — \`averageDays\` from cut to release, with \`measuredCount\` of \`releasedCount\` and \`previousAverageDays\`. Versions released without ever being cut (imports and backfills do this) carry no latency and are excluded rather than counted as zero, so say how much of the window the average speaks for.
- \`activity\` — the subtree depth-first, one row per node with \`depth\` and \`isReleasable\`. Groupings appear so the hierarchy reads but never release anything themselves. Every releasable node in scope is listed even if it shipped nothing — that is part of the answer. \`days\` lists only days with a release: \`released\`, and \`withdrawn\` (how many of that day's versions have since been withdrawn — a count, not a rate).`,
    inputSchema: {"type":"object","properties":{"from":{"type":"string","format":"date-time","description":"Start of the window, inclusive. A full ISO-8601 instant, not a plain date — 2026-08-01T00:00:00Z. Truncated to its UTC date."},"to":{"type":"string","format":"date-time","description":"End of the window, inclusive. A full ISO-8601 instant — 2026-09-01T00:00:00Z. Truncated to its UTC date."},"productId":{"type":"string","format":"uuid","description":"Scope to this product and everything beneath it. A grouping is a valid scope. Omit for the whole catalog."}},"required":["from","to"]},
    method: 'get',
    pathTemplate: '/api/product-management/delivery-overview',
    executionParameters: [{"name":"from","in":"query"},{"name":"to","in":"query"},{"name":"productId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get delivery overview', ...readsOnly },
  }],

  ['DeliveryOverview_GetRecentDeliveryEvents', {
    name: 'DeliveryOverview_GetRecentDeliveryEvents',
    description: `Get what has happened to versions and packages lately, most recent first — a feed answering "what shipped recently?". **One entry per record**, at its latest status change, not one per transition; the record's own status history has the rest.

Each entry has \`kind\` (1 Version, 2 ReleasePackage), \`recordId\`/\`recordKey\`, \`label\` (the version number or the package's own version, free text), \`product\` (null for a package, which spans several), \`statusName\` as the organization named it, \`alias\` (its well-known meaning — 10 Ready, 11 Released, 12 Withdrawn — which is what to reason on, since names are per-organization), \`changedOn\` (an instant), \`releasedDate\` where the record has shipped (so a withdrawal says what it pulled), and \`componentCount\` for a package.

Read from status transitions rather than the records' own dates, which carry no time of day. **Scoping to a product covers its subtree and leaves packages out entirely.**`,
    inputSchema: {"type":"object","properties":{"take":{"type":"integer","description":"How many entries, 1 to 50. Defaults to 10."},"productId":{"type":"string","format":"uuid","description":"Only versions of this product and everything beneath it. Packages are excluded when set."}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/delivery-overview/recent',
    executionParameters: [{"name":"take","in":"query"},{"name":"productId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get recent delivery events', ...readsOnly },
  }],

];
