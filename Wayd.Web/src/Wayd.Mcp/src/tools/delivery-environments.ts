import type { McpToolDefinition } from '../types.js';

/**
 * Deployment environments, and the delivery measures computed over them.
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
  enum: [1, 2, 3, 4],
  description:
    'Environment category: 1 Development (used while building), 2 Testing (verifying a change before release), 3 Staging (production-like final validation), 4 Production (live, serving real users — the denominator for delivery metrics).',
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
    description: `Retire an environment or reinstate one. **Environments are retired rather than deleted** — there is no delete, deliberately: historical deployments still point at them, and removing one would take the record of everything that ever reached it.

A retired environment is no longer offered as a deployment target, but it and every deployment recorded against it are kept, and those deployments keep counting toward the measures they already count toward. Editing and reclassifying are refused on a retired environment, so reinstate it first if you need to change it.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Environment ID. This endpoint takes a UUID only."},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"isActive":{"type":"boolean","description":"false retires the environment; true reinstates it."}},"required":["id","isActive"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/deployment-environments/{id}/active',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Retire or reinstate an environment', ...requiresConfirmation },
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

];
