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

  ['Projects_GetForecast', {
    name: 'Projects_GetForecast',
    description: `Forecast when a project's work items will be done, by Monte Carlo simulation of each team's recent throughput and each work item's backlog position and open predecessors. Returns an \`outcome\` (Forecast, Done, Not Enough History, Blocked by Dependency, Cannot Forecast, Nothing Remaining); on Forecast, completion \`percentiles\` (a \`date\` per \`confidence\`) and \`chanceOfFinishingByTargetDate\` (0 to 1) against \`targetDate\` when given, else the project's planned end. \`excludedWorkItems\` could not be forecast (see \`issues\`), which makes the dates a lower bound; \`dependencies\` gives each predecessor's \`shareOfTrialsSettingFinish\`. Requires the delivery-forecasting feature flag; returns 404 when it is off.`,
    inputSchema: {"type":"object","properties":{
      "idOrKey":{"type":"string","description":"Project ID (UUID) or key."},
      "targetDate":{"type":"string","format":"date","description":"Date to report the chance of finishing by, as YYYY-MM-DD. Defaults to the project's planned end."},
      "lookbackDays":{"type":"integer","minimum":14,"maximum":365,"description":"Days of history, ending yesterday (UTC), to sample throughput from (default 90)."},
      "ignoreDependencies":{"type":"boolean","description":"What-if: forecast as if nothing waited on its predecessors (default false)."},
      "startedWorkFirst":{"type":"boolean","description":"Count active backlog items ahead of proposed ones, each in rank order, since teams usually finish what they have started (default true). False orders by rank alone."}
    },"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{idOrKey}/forecast',
    executionParameters: [
      {"name":"idOrKey","in":"path"},
      {"name":"targetDate","in":"query"},
      {"name":"lookbackDays","in":"query"},
      {"name":"ignoreDependencies","in":"query"},
      {"name":"startedWorkFirst","in":"query"}
    ],
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

  ['Projects_GetProjectStages', {
    name: 'Projects_GetProjectStages',
    description: `Get stages for a project.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/stages',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectStage', {
    name: 'Projects_GetProjectStage',
    description: `Get project stage details.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"},"stageId":{"type":"string","format":"uuid"}},"required":["id","stageId"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/stages/{stageId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"stageId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectPlanTree', {
    name: 'Projects_GetProjectPlanTree',
    description: `Get a unified plan tree with stages as top-level nodes and tasks nested within. Returns both stage nodes and task nodes with WBS codes.`,
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
