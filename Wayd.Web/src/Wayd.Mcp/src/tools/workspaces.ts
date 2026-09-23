import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Workspaces_GetWorkItemForecast', {
    name: 'Workspaces_GetWorkItemForecast',
    description: `Forecast when a work item will be done, by Monte Carlo simulation of its team's recent throughput, its position in the team's backlog (everything ranked ahead of it counts), and the open predecessors it waits on. A portfolio work item (an Epic or Feature, say) is forecast from its open backlog descendants. Returns an \`outcome\` (Forecast, Done, Not Enough History, Blocked by Dependency, Cannot Forecast, Nothing Remaining), \`backlogPosition\`, and on Forecast completion \`percentiles\` (a \`date\` per \`confidence\`), plus \`chanceOfFinishingByTargetDate\` (0 to 1) when \`targetDate\` is given. \`issues\` explain what could not be forecast (No Team, Not a Backlog Item, Not Enough History); \`dependencies\` gives each predecessor's \`shareOfTrialsSettingFinish\`. Requires the delivery-forecasting feature flag; returns 404 when it is off.`,
    inputSchema: {"type":"object","properties":{
      "idOrKey":{"type":"string","description":"Workspace ID (UUID) or key — the prefix of the work item key (CORE for CORE-123)."},
      "workItemKey":{"type":"string","pattern":"^[A-Z][A-Z0-9]{1,19}-\\d+$","description":"The work item key, e.g. CORE-123."},
      "targetDate":{"type":"string","format":"date","description":"Date to report the chance of finishing by, as YYYY-MM-DD. Omit for dates only."},
      "lookbackDays":{"type":"integer","minimum":14,"maximum":365,"description":"Days of history, ending yesterday (UTC), to sample throughput from (default 90)."},
      "ignoreDependencies":{"type":"boolean","description":"What-if: forecast as if nothing waited on its predecessors (default false)."}
    },"required":["idOrKey","workItemKey"]},
    method: 'get',
    pathTemplate: '/api/work/workspaces/{idOrKey}/work-items/{workItemKey}/forecast',
    executionParameters: [
      {"name":"idOrKey","in":"path"},
      {"name":"workItemKey","in":"path"},
      {"name":"targetDate","in":"query"},
      {"name":"lookbackDays","in":"query"},
      {"name":"ignoreDependencies","in":"query"}
    ],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

];
