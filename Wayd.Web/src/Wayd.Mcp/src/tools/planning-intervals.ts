import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['PlanningIntervals_GetList', {
    name: 'PlanningIntervals_GetList',
    description: `Get a list of planning intervals.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetPlanningInterval', {
    name: 'PlanningIntervals_GetPlanningInterval',
    description: `Get planning interval details.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetCalendar', {
    name: 'PlanningIntervals_GetCalendar',
    description: `Get the PI calendar.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/calendar',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetPredictability', {
    name: 'PlanningIntervals_GetPredictability',
    description: `Get the PI predictability for all teams.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/predictability',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetTeams', {
    name: 'PlanningIntervals_GetTeams',
    description: `Get a list of teams participating in a planning interval.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/teams',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetTeamPredictability', {
    name: 'PlanningIntervals_GetTeamPredictability',
    description: `Get the PI predictability for a specific team.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"teamId":{"type":"string","format":"uuid"}},"required":["idOrKey","teamId"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/teams/{teamId}/predictability',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"teamId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetIterations', {
    name: 'PlanningIntervals_GetIterations',
    description: `Get a list of iterations for a planning interval.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/iterations',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetIteration', {
    name: 'PlanningIntervals_GetIteration',
    description: `Get a specific planning interval iteration.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"iterationIdOrKey":{"type":"string"}},"required":["idOrKey","iterationIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/iterations/{iterationIdOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"iterationIdOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetIterationCategories', {
    name: 'PlanningIntervals_GetIterationCategories',
    description: `Get a list of iteration categories.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/iteration-categories',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetIterationSprints', {
    name: 'PlanningIntervals_GetIterationSprints',
    description: `Get iteration sprint mappings for a Planning Interval. Optionally filter by iterationId.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"iterationId":{"type":["string","null"],"format":"uuid"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/iterations/sprints',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"iterationId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetIterationMetrics', {
    name: 'PlanningIntervals_GetIterationMetrics',
    description: `Get metrics for a PI iteration aggregated across all mapped sprints.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"iterationIdOrKey":{"type":"string"}},"required":["idOrKey","iterationIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/iterations/{iterationIdOrKey}/metrics',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"iterationIdOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetIterationBacklog', {
    name: 'PlanningIntervals_GetIterationBacklog',
    description: `Get combined backlog for a PI iteration from all mapped sprints.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"iterationIdOrKey":{"type":"string"}},"required":["idOrKey","iterationIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/iterations/{iterationIdOrKey}/backlog',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"iterationIdOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetObjectives', {
    name: 'PlanningIntervals_GetObjectives',
    description: `Get a list of planning interval objectives. Optionally filter by teamId.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"teamId":{"type":["string","null"],"format":"uuid"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/objectives',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"teamId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetObjective', {
    name: 'PlanningIntervals_GetObjective',
    description: `Get a planning interval objective.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"objectiveIdOrKey":{"type":"string"}},"required":["idOrKey","objectiveIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/objectives/{objectiveIdOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"objectiveIdOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetObjectiveStatuses', {
    name: 'PlanningIntervals_GetObjectiveStatuses',
    description: `Get a list of all PI objective statuses.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/objective-statuses',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetObjectivesHealthReport', {
    name: 'PlanningIntervals_GetObjectivesHealthReport',
    description: `Get a health report for planning interval objectives. Optionally filter by teamId.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"teamId":{"type":["string","null"],"format":"uuid"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/objectives/health-report',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"teamId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetObjectiveWorkItems', {
    name: 'PlanningIntervals_GetObjectiveWorkItems',
    description: `Get work items linked to a planning interval objective.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"objectiveIdOrKey":{"type":"string"}},"required":["idOrKey","objectiveIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/objectives/{objectiveIdOrKey}/work-items',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"objectiveIdOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetObjectiveWorkItemMetrics', {
    name: 'PlanningIntervals_GetObjectiveWorkItemMetrics',
    description: `Get daily metrics for work items linked to a planning interval objective.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"objectiveIdOrKey":{"type":"string"}},"required":["idOrKey","objectiveIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/objectives/{objectiveIdOrKey}/work-items/metrics',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"objectiveIdOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetObjectiveForecast', {
    name: 'PlanningIntervals_GetObjectiveForecast',
    description: `Forecast when a planning interval objective's linked work items will be done, by Monte Carlo simulation of each team's recent throughput and each work item's backlog position and open predecessors. Returns an \`outcome\` (Forecast, Done, Not Enough History, Blocked by Dependency, Cannot Forecast, Nothing Remaining); on Forecast, completion \`percentiles\` (a \`date\` per \`confidence\`) and \`chanceOfFinishingByTargetDate\` (0 to 1) against \`targetDate\` when given, else the objective's target date, else the planning interval's end. \`excludedWorkItems\` could not be forecast (see \`issues\`), which makes the dates a lower bound; \`dependencies\` gives each predecessor's \`shareOfTrialsSettingFinish\`. Requires the delivery-forecasting feature flag; returns 404 when it is off.`,
    inputSchema: {"type":"object","properties":{
      "idOrKey":{"type":"string","description":"Planning interval ID (UUID) or key."},
      "objectiveIdOrKey":{"type":"string","description":"Objective ID (UUID) or key."},
      "targetDate":{"type":"string","format":"date","description":"Date to report the chance of finishing by, as YYYY-MM-DD. Overrides the objective's target date and the planning interval's end."},
      "lookbackDays":{"type":"integer","minimum":14,"maximum":365,"description":"Days of history, ending yesterday (UTC), to sample throughput from (default 90)."},
      "ignoreDependencies":{"type":"boolean","description":"What-if: forecast as if nothing waited on its predecessors (default false)."}
    },"required":["idOrKey","objectiveIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/objectives/{objectiveIdOrKey}/forecast',
    executionParameters: [
      {"name":"idOrKey","in":"path"},
      {"name":"objectiveIdOrKey","in":"path"},
      {"name":"targetDate","in":"query"},
      {"name":"lookbackDays","in":"query"},
      {"name":"ignoreDependencies","in":"query"}
    ],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['PlanningIntervals_GetRisks', {
    name: 'PlanningIntervals_GetRisks',
    description: `Get planning interval risks. The default value for includeClosed is false. Optionally filter by teamId.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"includeClosed":{"type":["boolean","null"]},"teamId":{"type":["string","null"],"format":"uuid"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/risks',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"includeClosed","in":"query"},{"name":"teamId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

];
