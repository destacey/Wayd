import type { McpToolDefinition } from '../types.js';

/**
 * Deployments — one version or package reaching one environment.
 *
 * This is the substrate every delivery metric is computed from, which is why the records are
 * append-only: a deployment that started is only ever completed, and there is no edit and no delete.
 *
 * The rule an agent is most likely to break: a deployment carries **either a version or a package,
 * never both and never neither**. The request schema cannot express that — both ids are optional
 * fields — so the API validates it and the descriptions here state it.
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

const ID_ONLY = 'Deployment ID. This endpoint takes a UUID only, not a deployment key.';
const IN_FLIGHT = 'Offered only while the deployment is still in flight — once an outcome is recorded, none of the outcome tools can be called again.';

export const definitions: [string, McpToolDefinition][] = [

  ['Deployments_GetDeployments', {
    name: 'Deployments_GetDeployments',
    description: `List deployments, most recently started first. Each carries either a version or a package, never both. Filtering by environment category is how to scope to production, because environment *names* are free text and endlessly varied (\`prod\`, \`Production\`, \`prd\`, \`live\`) while the category is fixed.`,
    inputSchema: {"type":"object","properties":{"versionId":{"type":"string","format":"uuid","description":"Only deployments of this version. A version that shipped inside a package has no deployment of its own — the package was deployed and the version rode along."},"packageId":{"type":"string","format":"uuid","description":"Only deployments of this package."},"environmentId":{"type":"string","format":"uuid","description":"Only deployments into this environment."},"environmentCategory":{"type":"integer","description":"Only deployments into environments of this category. Each deployment freezes the category as it stood at the time, so reclassifying an environment does not rewrite history."},"startedOnOrAfter":{"type":"string","format":"date-time","description":"Only deployments started at or after this instant."}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/deployments',
    executionParameters: [{"name":"versionId","in":"query"},{"name":"packageId","in":"query"},{"name":"environmentId","in":"query"},{"name":"environmentCategory","in":"query"},{"name":"startedOnOrAfter","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List deployments', ...readsOnly },
  }],

  ['Deployments_GetDeployment', {
    name: 'Deployments_GetDeployment',
    description: `Get one deployment in full — what it carried, the environment it reached, its frozen environment category, its artifact identifier and its outcome. Accepts the deployment's UUID or its short key.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Deployment ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/deployments/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get deployment', ...readsOnly },
  }],

  ['Deployments_GetStatusHistory', {
    name: 'Deployments_GetStatusHistory',
    description: `Get a deployment's status change history, newest first. Each entry reports the status names as they were at the time.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Deployment ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/deployments/{idOrKey}/status-history',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get deployment status history', ...readsOnly },
  }],

  ['Deployments_Start', {
    name: 'Deployments_Start',
    description: `Record a deployment beginning. **Supply exactly one of versionId or packageId — never both, never neither.** The request is refused otherwise. Where a package exists it is the unit that shipped, so deploy the package rather than each component version: one pipeline run counts once, not once per service.

Only an **active** environment is accepted. Leaving \`startedAt\` empty records the deployment as starting now, which is what a pipeline reporting in real time would do. The artifact identifier is the build that actually shipped — \`4.8.2.008\` where the version number is \`4.8.2\` — and two builds of one version are two deployments.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"versionId":{"type":"string","format":"uuid","description":"The version deployed. Supply this OR packageId, never both and never neither."},"packageId":{"type":"string","format":"uuid","description":"The package deployed. Supply this OR versionId, never both and never neither. Prefer this where a package exists."},"environmentId":{"type":"string","format":"uuid","description":"The environment being reached. Must be active."},"artifactId":{"type":"string","description":"The build that actually shipped. Free text, never parsed."},"startedAt":{"type":"string","format":"date-time","description":"When it began. Omit to record it as starting now."}},"required":["environmentId"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/deployments',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Start a deployment', ...requiresConfirmation },
  }],

  ['Deployments_Succeed', {
    name: 'Deployments_Succeed',
    description: `Record that a deployment reached its environment. ${IN_FLIGHT} There is no edit and no delete on a deployment — it records something that happened.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"completedAt":{"type":"string","format":"date-time","description":"When it finished. Omit to record it as finishing now."}},"required":[]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/deployments/{id}/succeed',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Record deployment success', ...requiresConfirmation },
  }],

  ['Deployments_Fail', {
    name: 'Deployments_Fail',
    description: `Record that a deployment did not reach its environment. ${IN_FLIGHT} Note this is a deployment that *failed to arrive* — a deployment that succeeded and then broke something is a rollback, not a failure, and the distinction matters because change failure rate counts the second kind.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"reason":{"type":"string","description":"Why it failed. Optional."},"completedAt":{"type":"string","format":"date-time","description":"When it finished. Omit to record it as finishing now."}},"required":[]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/deployments/{id}/fail',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Record deployment failure', ...requiresConfirmation },
  }],

  ['Deployments_RollBack', {
    name: 'Deployments_RollBack',
    description: `Record that a deployment reached its environment and was then reverted. ${IN_FLIGHT} Distinct from a failure: this one arrived and then had to be undone, which is the signal change failure rate is computed from.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"reason":{"type":"string","description":"Why it was rolled back. Optional."},"rolledBackAt":{"type":"string","format":"date-time","description":"When it was rolled back. Omit to record it as now."}},"required":[]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/deployments/{id}/roll-back',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Record deployment rollback', ...requiresConfirmation },
  }],

];
