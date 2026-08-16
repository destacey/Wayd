import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  ['Tasks_GetTaskTypes', {
    name: 'Tasks_GetTaskTypes',
    description: `Get a list of all task types.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/ppm/projects/tasks/types',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_GetTaskStatuses', {
    name: 'Tasks_GetTaskStatuses',
    description: `Get a list of all task statuses.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/ppm/projects/tasks/statuses',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_GetTaskPriorities', {
    name: 'Tasks_GetTaskPriorities',
    description: `Get a list of all task priorities.`,
    inputSchema: {"type":"object","properties":{}},
    method: 'get',
    pathTemplate: '/api/ppm/projects/tasks/priorities',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_GetProjectTasks', {
    name: 'Tasks_GetProjectTasks',
    description: `Get a list of project tasks.`,
    inputSchema: {"type":"object","properties":{"projectIdOrKey":{"type":"string"},"status":{"type":["number","null"],"format":"int32"},"parentId":{"type":["string","null"],"format":"uuid"}},"required":["projectIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{projectIdOrKey}/tasks',
    executionParameters: [{"name":"projectIdOrKey","in":"path"},{"name":"status","in":"query"},{"name":"parentId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_GetProjectTask', {
    name: 'Tasks_GetProjectTask',
    description: `Get project task details.`,
    inputSchema: {"type":"object","properties":{"projectIdOrKey":{"type":"string"},"taskIdOrKey":{"type":"string"}},"required":["projectIdOrKey","taskIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{projectIdOrKey}/tasks/{taskIdOrKey}',
    executionParameters: [{"name":"projectIdOrKey","in":"path"},{"name":"taskIdOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_GetCriticalPath', {
    name: 'Tasks_GetCriticalPath',
    description: `Get the critical path for the project. Returns an ordered list of task IDs on the critical path.`,
    inputSchema: {"type":"object","properties":{"projectIdOrKey":{"type":"string"}},"required":["projectIdOrKey"]},
    method: 'get',
    pathTemplate: '/api/ppm/projects/{projectIdOrKey}/tasks/critical-path',
    executionParameters: [{"name":"projectIdOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_CreateProjectTask', {
    name: 'Tasks_CreateProjectTask',
    description: `Create a project task.`,
    inputSchema: {"type":"object","properties":{"projectIdOrKey":{"type":"string"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128},"description":{"type":["string","null"],"maxLength":2048},"typeId":{"type":"number","format":"int32","description":"Task type ID. Use Tasks_GetTaskTypes to get valid values."},"statusId":{"type":"number","format":"int32","description":"Task status ID. Use Tasks_GetTaskStatuses to get valid values."},"priorityId":{"type":"number","format":"int32","description":"Task priority ID. Use Tasks_GetTaskPriorities to get valid values."},"assigneeIds":{"type":["array","null"],"items":{"type":"string","format":"uuid"}},"progress":{"type":["number","null"],"format":"decimal","description":"Progress percentage (0.0–100.0). Required for tasks, not applicable for milestones."},"parentId":{"type":["string","null"],"format":"uuid"},"plannedStart":{"type":["string","null"],"format":"date","description":"ISO date string (YYYY-MM-DD). For tasks only."},"plannedEnd":{"type":["string","null"],"format":"date","description":"ISO date string (YYYY-MM-DD). For tasks only."},"plannedDate":{"type":["string","null"],"format":"date","description":"ISO date string (YYYY-MM-DD). For milestones only."},"estimatedEffortHours":{"type":["number","null"],"format":"decimal"}},"required":["name","typeId","statusId","priorityId"]}},"required":["projectIdOrKey","requestBody"]},
    method: 'post',
    pathTemplate: '/api/ppm/projects/{projectIdOrKey}/tasks',
    executionParameters: [{"name":"projectIdOrKey","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_UpdateProjectTask', {
    name: 'Tasks_UpdateProjectTask',
    description: `Update a project task.`,
    inputSchema: {"type":"object","properties":{"projectIdOrKey":{"type":"string"},"id":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid"},"name":{"type":"string","maxLength":128},"description":{"type":["string","null"],"maxLength":2048},"statusId":{"type":"number","format":"int32","description":"Task status ID. Use Tasks_GetTaskStatuses to get valid values."},"priorityId":{"type":"number","format":"int32","description":"Task priority ID. Use Tasks_GetTaskPriorities to get valid values."},"assigneeIds":{"type":["array","null"],"items":{"type":"string","format":"uuid"}},"progress":{"type":["number","null"],"format":"decimal","description":"Progress percentage (0.0–100.0). Not applicable for milestones."},"parentId":{"type":["string","null"],"format":"uuid"},"plannedStart":{"type":["string","null"],"format":"date","description":"ISO date string (YYYY-MM-DD)."},"plannedEnd":{"type":["string","null"],"format":"date","description":"ISO date string (YYYY-MM-DD)."},"plannedDate":{"type":["string","null"],"format":"date","description":"ISO date string (YYYY-MM-DD). For milestones only."},"estimatedEffortHours":{"type":["number","null"],"format":"decimal"}},"required":["id","name","statusId","priorityId"]}},"required":["projectIdOrKey","id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/ppm/projects/{projectIdOrKey}/tasks/{id}',
    executionParameters: [{"name":"projectIdOrKey","in":"path"},{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_DeleteProjectTask', {
    name: 'Tasks_DeleteProjectTask',
    description: `Delete a project task.`,
    inputSchema: {"type":"object","properties":{"projectIdOrKey":{"type":"string"},"id":{"type":"string","format":"uuid"}},"required":["projectIdOrKey","id"]},
    method: 'delete',
    pathTemplate: '/api/ppm/projects/{projectIdOrKey}/tasks/{id}',
    executionParameters: [{"name":"projectIdOrKey","in":"path"},{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { destructiveHint: true, readOnlyHint: false, idempotentHint: true },
  }],

  ['Tasks_AddTaskDependency', {
    name: 'Tasks_AddTaskDependency',
    description: `Add a dependency to a task. Creates a finish-to-start dependency where the specified task is the predecessor.`,
    inputSchema: {"type":"object","properties":{"projectIdOrKey":{"type":"string"},"id":{"type":"string","format":"uuid","description":"The predecessor task ID."},"requestBody":{"type":"object","properties":{"predecessorId":{"type":"string","format":"uuid"},"successorId":{"type":"string","format":"uuid"}},"required":["predecessorId","successorId"]}},"required":["projectIdOrKey","id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/ppm/projects/{projectIdOrKey}/tasks/{id}/dependencies',
    executionParameters: [{"name":"projectIdOrKey","in":"path"},{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['Tasks_RemoveTaskDependency', {
    name: 'Tasks_RemoveTaskDependency',
    description: `Remove a dependency from a task. Removes the finish-to-start dependency between the predecessor and successor tasks.`,
    inputSchema: {"type":"object","properties":{"projectIdOrKey":{"type":"string"},"id":{"type":"string","format":"uuid","description":"The predecessor task ID."},"successorId":{"type":"string","format":"uuid"}},"required":["projectIdOrKey","id","successorId"]},
    method: 'delete',
    pathTemplate: '/api/ppm/projects/{projectIdOrKey}/tasks/{id}/dependencies/{successorId}',
    executionParameters: [{"name":"projectIdOrKey","in":"path"},{"name":"id","in":"path"},{"name":"successorId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { destructiveHint: true, readOnlyHint: false, idempotentHint: true },
  }],

];
