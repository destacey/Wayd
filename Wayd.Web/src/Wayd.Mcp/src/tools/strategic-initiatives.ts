import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['StrategicInitiatives_GetStrategicInitiatives', {
    name: 'StrategicInitiatives_GetStrategicInitiatives',
    description: `Get a list of strategic initiatives. Optionally filter by status and/or portfolioId.`,
    inputSchema: {"type":"object","properties":{"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}},"portfolioId":{"type":["string","null"],"format":"uuid"}}},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives',
    executionParameters: [{"name":"status","in":"query"},{"name":"portfolioId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_GetStrategicInitiative', {
    name: 'StrategicInitiatives_GetStrategicInitiative',
    description: `Get strategic initiative details, including its portfolio, date range, sponsors, and owners.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_GetStatuses', {
    name: 'StrategicInitiatives_GetStatuses',
    description: `Get a list of all strategic initiative statuses. Call this to resolve the integer enum values used by the status filter.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives/statuses',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_GetProjects', {
    name: 'StrategicInitiatives_GetProjects',
    description: `Get the projects linked to a strategic initiative — the delivery work carried out to achieve it.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives/{idOrKey}/projects',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_GetKpis', {
    name: 'StrategicInitiatives_GetKpis',
    description: `Get the KPIs for a strategic initiative — the measures that define whether it succeeded. Each KPI carries a starting (baseline) value, a target value, the latest actual value, and a computed progress percentage toward the target. targetDirection is 1=Increase or 2=Decrease; for a Decrease KPI a falling value is improvement, so never assume a lower number is worse.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","description":"Strategic initiative ID or key."}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives/{id}/kpis',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_GetKpi', {
    name: 'StrategicInitiatives_GetKpi',
    description: `Get a single KPI for a strategic initiative.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","description":"Strategic initiative ID or key."},"kpiId":{"type":"string","description":"KPI ID or key."}},"required":["id","kpiId"]},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives/{id}/kpis/{kpiId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"kpiId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_GetKpiCheckpoints', {
    name: 'StrategicInitiatives_GetKpiCheckpoints',
    description: `Get the checkpoints for a KPI — the dated milestones a KPI is expected to hit, each with its own target value and optional at-risk threshold. Returns the checkpoint definitions only, without the measurements taken against them; use StrategicInitiatives_GetKpiCheckpointPlan for both together.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","description":"Strategic initiative ID or key."},"kpiId":{"type":"string","description":"KPI ID or key."}},"required":["id","kpiId"]},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives/{id}/kpis/{kpiId}/checkpoints',
    executionParameters: [{"name":"id","in":"path"},{"name":"kpiId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_GetKpiCheckpointPlan', {
    name: 'StrategicInitiatives_GetKpiCheckpointPlan',
    description: `Get the checkpoint plan for a KPI: every checkpoint paired with the measurement recorded against it, plus a computed health and trend per checkpoint. This is the best single call for assessing whether a KPI is on track over time. A checkpoint with no measurement has a null measurement, health, and trend.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","description":"Strategic initiative ID or key."},"kpiId":{"type":"string","description":"KPI ID or key."}},"required":["id","kpiId"]},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives/{id}/kpis/{kpiId}/checkpoints/plan',
    executionParameters: [{"name":"id","in":"path"},{"name":"kpiId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_GetKpiMeasurements', {
    name: 'StrategicInitiatives_GetKpiMeasurements',
    description: `Get every measurement recorded against a KPI, each with its actual value, the date it was taken, who took it, and an optional note.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","description":"Strategic initiative ID or key."},"kpiId":{"type":"string","description":"KPI ID or key."}},"required":["id","kpiId"]},
    method: 'get',
    pathTemplate: '/api/ppm/strategic-initiatives/{id}/kpis/{kpiId}/measurements',
    executionParameters: [{"name":"id","in":"path"},{"name":"kpiId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_AddKpiMeasurement', {
    name: 'StrategicInitiatives_AddKpiMeasurement',
    description: `Record a measurement against a KPI — the actual observed value at a point in time. Measurements accumulate as a history rather than overwriting; the KPI's headline actual value is the measurement with the latest measurementDate. Measurement dates must be unique within a KPI, so re-submitting an existing date is rejected rather than treated as an update. strategicInitiativeId and kpiId in the body must match the path parameters. Unlike the KPI read tools, this takes UUIDs only, not keys.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Strategic initiative ID (UUID only)."},"kpiId":{"type":"string","format":"uuid","description":"KPI ID (UUID only)."},"requestBody":{"type":"object","properties":{"strategicInitiativeId":{"type":"string","format":"uuid","description":"Must match the id path parameter."},"kpiId":{"type":"string","format":"uuid","description":"Must match the kpiId path parameter."},"actualValue":{"type":"number","format":"double","description":"The measured value. Must be non-zero."},"measurementDate":{"type":"string","format":"date-time","description":"ISO 8601 UTC datetime the measurement was taken."},"note":{"type":["string","null"],"maxLength":1024}},"required":["strategicInitiativeId","kpiId","actualValue","measurementDate"]}},"required":["id","kpiId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/ppm/strategic-initiatives/{id}/kpis/{kpiId}/measurements',
    executionParameters: [{"name":"id","in":"path"},{"name":"kpiId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StrategicInitiatives_RemoveKpiMeasurement', {
    name: 'StrategicInitiatives_RemoveKpiMeasurement',
    description: `Remove a measurement from a KPI. This deletes the recorded history entry and changes the KPI's derived actual value and progress. To record a new observation, add a measurement instead — deletion is only for correcting a wrong entry, or for freeing up a date so it can be re-recorded.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Strategic initiative ID (UUID only)."},"kpiId":{"type":"string","format":"uuid","description":"KPI ID (UUID only)."},"measurementId":{"type":"string","format":"uuid"}},"required":["id","kpiId","measurementId"]},
    method: 'delete',
    pathTemplate: '/api/ppm/strategic-initiatives/{id}/kpis/{kpiId}/measurements/{measurementId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"kpiId","in":"path"},{"name":"measurementId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

];
