import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Projects_GetProjects', {
    name: 'Projects_GetProjects',
    description: `Get a list of projects.`,
    inputSchema: {"type":"object","properties":{"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}},"portfolioId":{"type":["string","null"],"format":"uuid"},"role":{"type":["array","null"],"items":{"type":"number","format":"int32"},"description":"Project role filter. 1=Sponsor, 2=Owner, 3=Manager, 4=Member."}}},
    method: 'get',
    pathTemplate: '/api/ppm/projects',
    executionParameters: [{"name":"status","in":"query"},{"name":"portfolioId","in":"query"},{"name":"role","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetMyProjectsSummary', {
    name: 'Projects_GetMyProjectsSummary',
    description: `Get a summary of the current user's project involvement, as counts per role (total, sponsor, owner, manager, member, assignee). Scoped to the caller — no user parameter.`,
    inputSchema: {"type":"object","properties":{"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}}}},
    method: 'get',
    pathTemplate: '/api/ppm/projects/my-summary',
    executionParameters: [{"name":"status","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetMyProjectsTaskMetrics', {
    name: 'Projects_GetMyProjectsTaskMetrics',
    description: `Get aggregated open-task counts across the current user's projects: overdue, due this week (through Saturday), and upcoming (next Sunday through Saturday). Scoped to the caller — no user parameter.`,
    inputSchema: {"type":"object","properties":{"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}},"role":{"type":["array","null"],"items":{"type":"number","format":"int32"},"description":"Project role filter. 1=Sponsor, 2=Owner, 3=Manager, 4=Member."}}},
    method: 'get',
    pathTemplate: '/api/ppm/projects/my-task-metrics',
    executionParameters: [{"name":"status","in":"query"},{"name":"role","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProject', {
    name: 'Projects_GetProject',
    description: `Get project details.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetStatusHistory', {
    name: 'Projects_GetStatusHistory',
    description: `Get the project's status change history. Each entry records the status moved out of (null for the project's initial state), the status moved into, who made the change, when, and an optional reason. Entries are flagged as recorded live or reconstructed from the audit trail.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID. This endpoint takes a UUID only, not a project key."}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/status-history',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetStatuses', {
    name: 'Projects_GetStatuses',
    description: `Get a list of all project statuses.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/ppm/projects/statuses',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetWorkItems', {
    name: 'Projects_GetWorkItems',
    description: `Get work items for a project.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/work-items',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectTeam', {
    name: 'Projects_GetProjectTeam',
    description: `Get the team members for a project.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{idOrKey}/team',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectPhases', {
    name: 'Projects_GetProjectPhases',
    description: `Get phases for a project.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/phases',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectPhase', {
    name: 'Projects_GetProjectPhase',
    description: `Get project phase details.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"},"phaseId":{"type":"string","format":"uuid"}},"required":["id","phaseId"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/phases/{phaseId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"phaseId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectPlanTree', {
    name: 'Projects_GetProjectPlanTree',
    description: `Get a unified plan tree with phases as top-level nodes and tasks nested within. Returns both phase nodes and task nodes with WBS codes.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{idOrKey}/plan-tree',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectPlanSummary', {
    name: 'Projects_GetProjectPlanSummary',
    description: `Get summary metrics for a project's plan, computed from leaf tasks. Includes overdue, due this week, upcoming, and total task counts.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"employeeId":{"type":["string","null"],"format":"uuid"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{idOrKey}/plan-summary',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"employeeId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectsPlanSummaries', {
    name: 'Projects_GetProjectsPlanSummaries',
    description: `Get plan summary metrics for multiple projects in one request, keyed by project ID. Prefer this over calling Projects_GetProjectPlanSummary once per project when surveying several projects.`,
    inputSchema: {"type":"object","properties":{"projectId":{"type":"array","items":{"type":"string","format":"uuid"},"description":"Project IDs (UUIDs only, not keys)."},"role":{"type":["array","null"],"items":{"type":"number","format":"int32"},"description":"Project role filter. 1=Sponsor, 2=Owner, 3=Manager, 4=Member."}},"required":["projectId"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/plan-summaries',
    executionParameters: [{"name":"projectId","in":"query"},{"name":"role","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

];
