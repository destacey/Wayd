import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['ProjectLifecycles_GetProjectLifecycles', {
    name: 'ProjectLifecycles_GetProjectLifecycles',
    description: `Get a list of project lifecycles.`,
    inputSchema: {"type":"object","properties":{"state":{"type":["number","null"],"format":"int32","description":"Lifecycle state filter. 1=Proposed, 2=Active, 3=Archived."}}},
    method: 'get',
    pathTemplate: '/api/ppm/project-lifecycles',
    executionParameters: [{"name":"state","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List project lifecycles' },
  }],

  ['ProjectLifecycles_GetProjectLifecycle', {
    name: 'ProjectLifecycles_GetProjectLifecycle',
    description: `Get project lifecycle details.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/project-lifecycles/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get project lifecycle' },
  }],

];
