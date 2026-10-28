import type { McpToolDefinition } from '../types.js';

const readsOnly = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
} as const;

const addsOnly = {
  destructiveHint: false,
  readOnlyHint: false,
  idempotentHint: false,
} as const;

const requiresConfirmation = {
  destructiveHint: true,
  readOnlyHint: false,
  idempotentHint: false,
} as const;

export const definitions: [string, McpToolDefinition][] = [
  ['IncidentSeveritySets_GetSets', {
    name: 'IncidentSeveritySets_GetSets',
    description: 'List incident severity sets, their ordered levels, and which set is selected for the organization.',
    inputSchema: {"type":"object","properties":{"includeInactive":{"type":"boolean"}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/incident-severity-sets',
    executionParameters: [{ name: 'includeInactive', in: 'query' }],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List incident severity sets', ...readsOnly },
  }],
  ['IncidentSeveritySets_GetSet', {
    name: 'IncidentSeveritySets_GetSet',
    description: 'Get one incident severity set by id.',
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/product-management/incident-severity-sets/{id}',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get incident severity set', ...readsOnly },
  }],
  ['IncidentSeveritySets_CreateSet', {
    name: 'IncidentSeveritySets_CreateSet',
    description: 'Create a custom severity set with at least two levels, ordered most severe first.',
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"name":{"type":"string"},"description":{"type":"string"},"levelNames":{"type":"array","items":{"type":"string"}}},"required":["name","levelNames"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/incident-severity-sets',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Create incident severity set', ...addsOnly },
  }],
  ['IncidentSeveritySets_UpdateSet', {
    name: 'IncidentSeveritySets_UpdateSet',
    description: 'Update a custom severity set. Replaces the full ordered level list; omitting an existing level id removes that level unless an incident still uses it. Refused on system sets.',
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid"},"name":{"type":"string"},"description":{"type":"string"},"levels":{"type":"array","items":{"type":"object","properties":{"id":{"type":"string","format":"uuid"},"name":{"type":"string"}},"required":["name"]}}},"required":["id","name","levels"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/incident-severity-sets/{id}',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update incident severity set', ...requiresConfirmation },
  }],
  ['IncidentSeveritySets_SetActiveStatus', {
    name: 'IncidentSeveritySets_SetActiveStatus',
    description: 'Activate or deactivate a custom severity set. The selected set and system sets cannot be deactivated.',
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid"},"isActive":{"type":"boolean"}},"required":["id","isActive"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/incident-severity-sets/{id}/active',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Activate or deactivate incident severity set', ...requiresConfirmation },
  }],
  ['IncidentSeveritySets_SelectSet', {
    name: 'IncidentSeveritySets_SelectSet',
    description: 'Select which severity set new incidents use. Open incidents keep the level they were given.',
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"severitySetId":{"type":"string","format":"uuid"}},"required":["severitySetId"]}},"required":["requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/incident-severity-sets/selection',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Select incident severity set', ...requiresConfirmation },
  }],
  ['IncidentSeveritySets_DeleteSet', {
    name: 'IncidentSeveritySets_DeleteSet',
    description: 'Delete a custom severity set. Refused for system sets, the selected set, or when an incident still uses a level in the set.',
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"}},"required":["id"]},
    method: 'delete',
    pathTemplate: '/api/product-management/incident-severity-sets/{id}',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Delete incident severity set', ...requiresConfirmation },
  }],
];
