import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['PlanningIntervals_GetObjectiveHealthChecks', {
    name: 'PlanningIntervals_GetObjectiveHealthChecks',
    description: `Get the full health check history for a planning interval objective, ordered newest first.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Planning interval ID."},"objectiveId":{"type":"string","format":"uuid","description":"Planning interval objective ID."}},"required":["id","objectiveId"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{id}/objectives/{objectiveId}/health-checks',
    executionParameters: [{"name":"id","in":"path"},{"name":"objectiveId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List PI objective health checks' },
  }],

  ['PlanningIntervals_GetObjectiveHealthCheck', {
    name: 'PlanningIntervals_GetObjectiveHealthCheck',
    description: `Get a single planning interval objective health check by ID.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Planning interval ID."},"objectiveId":{"type":"string","format":"uuid","description":"Planning interval objective ID."},"healthCheckId":{"type":"string","format":"uuid"}},"required":["id","objectiveId","healthCheckId"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{id}/objectives/{objectiveId}/health-checks/{healthCheckId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"objectiveId","in":"path"},{"name":"healthCheckId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get PI objective health check' },
  }],

  ['PlanningIntervals_CreateObjectiveHealthCheck', {
    name: 'PlanningIntervals_CreateObjectiveHealthCheck',
    description: `Log a new health check on a planning interval objective. Creating a new check automatically expires the previously active check (only one non-expired check can exist at a time). Note: the API requires planningIntervalObjectiveId in the body in addition to the objectiveId path parameter — they must match.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Planning interval ID."},"objectiveId":{"type":"string","format":"uuid","description":"Planning interval objective ID. Must match requestBody.planningIntervalObjectiveId."},"requestBody":{"type":"object","properties":{"planningIntervalObjectiveId":{"type":"string","format":"uuid","description":"Must match the objectiveId path parameter."},"statusId":{"type":"number","format":"int32","description":"Health status: 1=Healthy, 2=AtRisk, 3=Unhealthy."},"expiration":{"type":"string","format":"date-time","description":"ISO 8601 UTC datetime when this health check expires. Must be in the future."},"note":{"type":["string","null"],"maxLength":1024}},"required":["planningIntervalObjectiveId","statusId","expiration"]}},"required":["id","objectiveId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/planning-intervals/{id}/objectives/{objectiveId}/health-checks',
    executionParameters: [{"name":"id","in":"path"},{"name":"objectiveId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Log a PI objective health check', destructiveHint: false },
  }],

];
