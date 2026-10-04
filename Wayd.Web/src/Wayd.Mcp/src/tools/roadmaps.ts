import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Roadmaps_GetRoadmaps', {
    name: 'Roadmaps_GetRoadmaps',
    description: `Get a list of roadmaps.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/planning/roadmaps',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List roadmaps' },
  }],

  ['Roadmaps_GetRoadmap', {
    name: 'Roadmaps_GetRoadmap',
    description: `Get roadmap details.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/roadmaps/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get roadmap' },
  }],

  ['Roadmaps_GetItems', {
    name: 'Roadmaps_GetItems',
    description: `Get all items (activities, timeboxes, milestones) for a roadmap.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/roadmaps/{idOrKey}/items',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List roadmap items' },
  }],

  ['Roadmaps_GetActivities', {
    name: 'Roadmaps_GetActivities',
    description: `Get roadmap activities.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/roadmaps/{idOrKey}/items/activities',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List roadmap activities' },
  }],

  ['Roadmaps_GetItem', {
    name: 'Roadmaps_GetItem',
    description: `Get roadmap item details.`,
    inputSchema: {"type":"object","properties":{"roadmapIdOrKey":{"type":"string"},"itemId":{"type":"string","format":"uuid"}},"required":["roadmapIdOrKey","itemId"]},
    method: 'get',
    pathTemplate: '/api/planning/roadmaps/{roadmapIdOrKey}/items/{itemId}',
    executionParameters: [{"name":"roadmapIdOrKey","in":"path"},{"name":"itemId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get roadmap item' },
  }],

  ['Roadmaps_GetVisibilityOptions', {
    name: 'Roadmaps_GetVisibilityOptions',
    description: `Get a list of all roadmap visibility options.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/planning/roadmaps/visibility-options',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List roadmap visibility options' },
  }],

];
