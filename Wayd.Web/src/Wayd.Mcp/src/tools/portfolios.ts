import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Portfolios_GetPortfolios', {
    name: 'Portfolios_GetPortfolios',
    description: `Get a list of project portfolios.`,
    inputSchema: {"type":"object","properties":{"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}}}},
    method: 'get',
    pathTemplate: '/api/ppm/portfolios',
    executionParameters: [{"name":"status","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Portfolios_GetPortfolio', {
    name: 'Portfolios_GetPortfolio',
    description: `Get project portfolio details.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/portfolios/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Portfolios_GetPortfolioOptions', {
    name: 'Portfolios_GetPortfolioOptions',
    description: `Get a lightweight list of project portfolio options (id and name) for use in lookups.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/ppm/portfolios/options',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Portfolios_GetPortfolioStatuses', {
    name: 'Portfolios_GetPortfolioStatuses',
    description: `Get a list of all project portfolio statuses.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/ppm/portfolios/statuses',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Portfolios_GetPortfolioPrograms', {
    name: 'Portfolios_GetPortfolioPrograms',
    description: `Get a list of programs for a portfolio. Optionally filter by status.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/portfolios/{idOrKey}/programs',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"status","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Portfolios_GetPortfolioProjects', {
    name: 'Portfolios_GetPortfolioProjects',
    description: `Get a list of projects for a portfolio. Optionally filter by status.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/portfolios/{idOrKey}/projects',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"status","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Portfolios_GetPortfolioStrategicInitiatives', {
    name: 'Portfolios_GetPortfolioStrategicInitiatives',
    description: `Get a list of strategic initiatives for a portfolio. Optionally filter by status.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/portfolios/{idOrKey}/strategic-initiatives',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"status","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

];
