import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Projects_GetProjectHealthChecks', {
    name: 'Projects_GetProjectHealthChecks',
    description: `Get the full health check history for a project, ordered newest first.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID."}},"required":["id"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/health-checks',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_GetProjectHealthCheck', {
    name: 'Projects_GetProjectHealthCheck',
    description: `Get a single project health check by ID.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID."},"healthCheckId":{"type":"string","format":"uuid"}},"required":["id","healthCheckId"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{id}/health-checks/{healthCheckId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"healthCheckId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_CreateProjectHealthCheck', {
    name: 'Projects_CreateProjectHealthCheck',
    description: `Log a new health check on a project. Creating a new check automatically expires the previously active check (only one non-expired check can exist at a time). Caller must be the project, parent portfolio, or parent program owner or manager.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID."},"requestBody":{"type":"object","properties":{"status":{"type":"string","enum":["Healthy","AtRisk","Unhealthy"],"description":"Health status."},"expiration":{"type":"string","format":"date-time","description":"ISO 8601 UTC datetime when this health check expires. Must be in the future."},"note":{"type":["string","null"],"maxLength":1024}},"required":["status","expiration"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/ppm/projects/{id}/health-checks',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Projects_UpdateProjectHealthCheck', {
    name: 'Projects_UpdateProjectHealthCheck',
    description: `Correct an existing health check's status, expiration, or note. This rewrites what was reported for that point in time — to report a *new* assessment, use Projects_CreateProjectHealthCheck instead, which preserves the history. Every field is overwritten from the request body, so read the check first and echo back anything that should stay the same. Caller must be the project, parent portfolio, or parent program owner or manager.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID."},"healthCheckId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"status":{"type":"string","enum":["Healthy","AtRisk","Unhealthy"],"description":"Health status."},"expiration":{"type":"string","format":"date-time","description":"ISO 8601 UTC datetime when this health check expires."},"note":{"type":["string","null"],"maxLength":1024}},"required":["status","expiration"]}},"required":["id","healthCheckId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/ppm/projects/{id}/health-checks/{healthCheckId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"healthCheckId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update project health check', destructiveHint: true, readOnlyHint: false, idempotentHint: true },
  }],

  ['Projects_DeleteProjectHealthCheck', {
    name: 'Projects_DeleteProjectHealthCheck',
    description: `Delete a health check from a project, permanently removing it from the project's health history. Deleting the active check leaves the project with no current health status. Prefer logging a new check over deleting an old one — deletion rewrites the record of what was reported when.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Project ID."},"healthCheckId":{"type":"string","format":"uuid"}},"required":["id","healthCheckId"]},
    method: 'delete',
    pathTemplate: '/api/ppm/projects/{id}/health-checks/{healthCheckId}',
    executionParameters: [{"name":"id","in":"path"},{"name":"healthCheckId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Delete project health check', destructiveHint: true, readOnlyHint: false, idempotentHint: true },
  }],

];
