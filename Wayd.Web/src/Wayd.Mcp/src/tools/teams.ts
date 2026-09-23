import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Teams_GetTeams', {
    name: 'Teams_GetTeams',
    description: `Get a list of teams.`,
    inputSchema: {"type":"object","properties":{"includeInactive":{"type":"boolean"}}},
    method: 'get',
    pathTemplate: '/api/organization/teams',
    executionParameters: [{"name":"includeInactive","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Teams_GetTeam', {
    name: 'Teams_GetTeam',
    description: `Get team details.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"number","format":"int32"}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/organization/teams/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Teams_GetBacklogHealth', {
    name: 'Teams_GetBacklogHealth',
    description: `Grade a team's open backlog. Returns \`checks\` — Runway, Net Flow and WIP Load measure the whole backlog; Stale, Old Proposed, Aging WIP, Missing Story Points, Oversized, No Parent, No Project, Unassigned Active, Carry-over, Closed Parent and Rank Inversion flag work items — each with an \`outcome\` (Assessed, Not Enough History, Not Applicable), a \`grade\` (Healthy, At Risk, Unhealthy; absent when not assessed), and a \`value\` (weeks, a ratio, active items per member, or the percent of in-scope work items flagged). \`workItems\` lists every open backlog work item in rank order with the \`flags\` that apply to it, and \`thresholds\` states the values it was graded with. Every threshold is optional and falls back to its default.`,
    inputSchema: {"type":"object","properties":{
      "idOrCode":{"type":"string","description":"Team ID (UUID) or team code — not the integer key."},
      "lookbackDays":{"type":"integer","minimum":14,"maximum":365,"description":"Days of history for throughput, cycle time and net flow (default 90)."},
      "staleDays":{"type":"integer","minimum":1,"maximum":3650,"description":"Days without a change before a work item is stale (default 90)."},
      "oldProposedDays":{"type":"integer","minimum":1,"maximum":3650,"description":"Days since creation before a proposed work item is old (default 180)."},
      "agingWipPercentile":{"type":"integer","minimum":1,"maximum":100,"description":"Cycle time percentile an active work item is aging beyond (default 85)."},
      "oversizedPercentile":{"type":"integer","minimum":1,"maximum":100,"description":"Story point percentile of completed work a work item is oversized above (default 85)."},
      "readinessWindowWeeks":{"type":"integer","minimum":1,"maximum":52,"description":"Weeks of throughput the readiness checks look ahead (default 4)."},
      "readinessFallbackItems":{"type":"integer","minimum":1,"maximum":1000,"description":"Top-ranked work items the readiness checks look at without enough history (default 20)."},
      "atRiskPercent":{"type":"integer","minimum":1,"maximum":100,"description":"Percent of in-scope work items flagged at which a check is At Risk (default 10)."},
      "unhealthyPercent":{"type":"integer","minimum":1,"maximum":100,"description":"Percent flagged at which a check is Unhealthy (default 25); not below atRiskPercent."},
      "runwayAtRiskWeeks":{"type":"number","minimum":0,"maximum":520,"description":"Runway weeks below which the backlog is At Risk (default 4)."},
      "runwayUnhealthyWeeks":{"type":"number","minimum":0,"maximum":520,"description":"Runway weeks below which the backlog is Unhealthy (default 2); not above runwayAtRiskWeeks."},
      "runwayTooLongWeeks":{"type":"number","minimum":1,"maximum":520,"description":"Runway weeks above which the backlog is At Risk for being too long (default 26)."},
      "netFlowAtRisk":{"type":"number","minimum":0.01,"maximum":100,"description":"Work items created per completed above which net flow is At Risk (default 1.2)."},
      "netFlowUnhealthy":{"type":"number","minimum":0.01,"maximum":100,"description":"Net flow above which it is Unhealthy (default 1.5); not below netFlowAtRisk."},
      "wipLoadAtRisk":{"type":"number","minimum":0.01,"maximum":100,"description":"Active work items per member above which WIP load is At Risk (default 1.5)."},
      "wipLoadUnhealthy":{"type":"number","minimum":0.01,"maximum":100,"description":"WIP load above which it is Unhealthy (default 2); not below wipLoadAtRisk."}
    },"required":["idOrCode"]},
    method: 'get',
    pathTemplate: '/api/organization/teams/{idOrCode}/backlog-health',
    executionParameters: [
      {"name":"idOrCode","in":"path"},
      {"name":"lookbackDays","in":"query"},
      {"name":"staleDays","in":"query"},
      {"name":"oldProposedDays","in":"query"},
      {"name":"agingWipPercentile","in":"query"},
      {"name":"oversizedPercentile","in":"query"},
      {"name":"readinessWindowWeeks","in":"query"},
      {"name":"readinessFallbackItems","in":"query"},
      {"name":"atRiskPercent","in":"query"},
      {"name":"unhealthyPercent","in":"query"},
      {"name":"runwayAtRiskWeeks","in":"query"},
      {"name":"runwayUnhealthyWeeks","in":"query"},
      {"name":"runwayTooLongWeeks","in":"query"},
      {"name":"netFlowAtRisk","in":"query"},
      {"name":"netFlowUnhealthy","in":"query"},
      {"name":"wipLoadAtRisk","in":"query"},
      {"name":"wipLoadUnhealthy","in":"query"}
    ],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

];
