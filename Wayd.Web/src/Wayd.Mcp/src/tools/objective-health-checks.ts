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
    description: `Log a new health check on a planning interval objective. Creating a new check automatically expires the previously active check (only one non-expired check can exist at a time). Note: the API requires planningIntervalObjectiveId in the body in addition to the objectiveId path parameter — they must match. To recommend a check, first gather the evidence: PlanningIntervals_GetPlanningInterval (the PI's dates, which bound the check), PlanningIntervals_GetObjective (progress and dates; unset dates default to the PI's), PlanningIntervals_GetObjectiveWorkItems and PlanningIntervals_GetObjectiveWorkItemMetrics (linked work and daily flow), PlanningIntervals_GetObjectiveForecast (a 404 means forecasting is off — judge without it and say so), PlanningIntervals_GetObjectiveHealthChecks (the latest check), PlanningIntervals_GetObjectiveActivities for changes since it, and PlanningIntervals_GetRisks with the team's id. Healthy (1) when progress keeps pace with elapsed time, flow is steady and the forecast chance of finishing by the target date is about 80% or more; AtRisk (2) when progress lags the schedule, work in progress piles up, the chance is 50–80% or the 85% date slightly passes the target, or a dependency or risk threatens delivery; Unhealthy (3) when progress has stalled, the chance is below 50%, the 85% date is far past the target or the PI's end, or the outcome is Blocked by Dependency. Draft a note (at most 1024 characters) citing that evidence — progress since the last check, forecast confidence, concerns — with a future expiration no later than the PI's end. Present the status, the expiration, the note and its rationale, and call this only after the user confirms.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Planning interval ID."},"objectiveId":{"type":"string","format":"uuid","description":"Planning interval objective ID. Must match requestBody.planningIntervalObjectiveId."},"requestBody":{"type":"object","properties":{"planningIntervalObjectiveId":{"type":"string","format":"uuid","description":"Must match the objectiveId path parameter."},"statusId":{"type":"number","format":"int32","description":"Health status: 1=Healthy, 2=AtRisk, 3=Unhealthy."},"expiration":{"type":"string","format":"date-time","description":"ISO 8601 UTC datetime when this health check expires. Must be in the future."},"note":{"type":["string","null"],"maxLength":1024}},"required":["planningIntervalObjectiveId","statusId","expiration"]}},"required":["id","objectiveId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/planning-intervals/{id}/objectives/{objectiveId}/health-checks',
    executionParameters: [{"name":"id","in":"path"},{"name":"objectiveId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Log a PI objective health check', destructiveHint: false },
  }],

];
