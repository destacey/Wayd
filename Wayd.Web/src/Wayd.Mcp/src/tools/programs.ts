import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Programs_GetPrograms', {
    name: 'Programs_GetPrograms',
    description: `Get a list of programs. Optionally filter by status and/or portfolioId.`,
    inputSchema: {"type":"object","properties":{"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}},"portfolioId":{"type":["string","null"],"format":"uuid"}}},
    method: 'get',
    pathTemplate: '/api/ppm/programs',
    executionParameters: [{"name":"status","in":"query"},{"name":"portfolioId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List programs' },
  }],

  ['Programs_GetProgram', {
    name: 'Programs_GetProgram',
    description: `Get program details.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/programs/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get program' },
  }],

  ['Programs_GetProgramStatuses', {
    name: 'Programs_GetProgramStatuses',
    description: `Get a list of all program statuses.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/ppm/programs/statuses',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List program statuses' },
  }],

  ['Programs_GetProgramProjects', {
    name: 'Programs_GetProgramProjects',
    description: `Get a list of projects for a program. Optionally filter by status.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"},"status":{"type":["array","null"],"items":{"type":"number","format":"int32"}}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/programs/{idOrKey}/projects',
    executionParameters: [{"name":"idOrKey","in":"path"},{"name":"status","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: "List a program's projects" },
  }],

];
