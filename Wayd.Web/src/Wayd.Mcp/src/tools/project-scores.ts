import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Projects_GetScoringContext', {
    name: 'Projects_GetScoringContext',
    description: `Get the scoring context for a project: the scoring model assigned to its portfolio (criteria, scales, and outputs), whether that model has been archived, and the project's current score. The scoring model is null when the project's portfolio has no model assigned, which means the project cannot be scored.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID. This endpoint takes a UUID only, not a project key."}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/scoring-context',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetScores', {
    name: 'Projects_GetScores',
    description: `Get the scoring history for a project — every score ever recorded, each with its headline value, the model used, who scored it, and when. Returns headline values only; use Projects_GetScore for a single score's full per-criterion rating breakdown.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID. This endpoint takes a UUID only, not a project key."}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/scores',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetScore', {
    name: 'Projects_GetScore',
    description: `Get one recorded project score in full. Returns the frozen snapshot as it was at scoring time — every criterion rating and computed output value, plus the model name and version used. Because the snapshot is frozen, an old score reflects the model as it was then, not the model as it is now.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID. This endpoint takes a UUID only, not a project key."},"scoreId":{"type":"string","format":"uuid"}},"required":["id","scoreId"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/scores/{scoreId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"scoreId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Portfolios_GetRankingScoreboard', {
    name: 'Portfolios_GetRankingScoreboard',
    description: `Get the per-project score breakdown behind a portfolio's ranking board: the portfolio's current scoring model definition, plus each project's criterion ratings and output values. A project's ratings and outputs are empty when it is unscored or its latest score came from a different or older model. Returns the score breakdown only — it does not include project names or rank positions, so pair it with Portfolios_GetPortfolioProjects and join on project ID.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Portfolio ID. This endpoint takes a UUID only, not a portfolio key."}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/portfolios/{id}/ranking-scoreboard',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

];
