import type { McpToolDefinition } from '../types.js';

export const definitions: [string, McpToolDefinition][] = [

  // -------------------------------------------------------------------------------------------
  // Story maps
  // -------------------------------------------------------------------------------------------

  ['StoryMaps_GetStoryMaps', {
    name: 'StoryMaps_GetStoryMaps',
    description: `Get a list of story maps (id, key, name, description, status, owner).`,
    inputSchema: {"type":"object","properties":{"includeArchived":{"type":["boolean","null"],"description":"Include archived story maps. Defaults to false."}}},
    method: 'get',
    pathTemplate: '/api/planning/story-maps',
    executionParameters: [{"name":"includeArchived","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_GetStoryMap', {
    name: 'StoryMaps_GetStoryMap',
    description: `Get a story map in full: goals, each with ordered steps and tasks (including checklists, persona tags, and linked work item IDs), plus the map's swim lanes and personas.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string"}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/planning/story-maps/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_CreateStoryMap', {
    name: 'StoryMaps_CreateStoryMap',
    description: `Create a story map. Returns the new map's ID and key.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128},"description":{"type":["string","null"],"maxLength":2048}},"required":["name"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/story-maps',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_UpdateStoryMap', {
    name: 'StoryMaps_UpdateStoryMap',
    description: `Update a story map's name and description.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128},"description":{"type":["string","null"],"maxLength":2048}},"required":["name"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_ChangeStoryMapOwner', {
    name: 'StoryMaps_ChangeStoryMapOwner',
    description: `Change the owner of a story map.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"ownerId":{"type":"string","description":"User ID of the new owner. Use Users_GetUsers to resolve a name to an ID."}},"required":["ownerId"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{id}/owner',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_ArchiveStoryMap', {
    name: 'StoryMaps_ArchiveStoryMap',
    description: `Archive a story map.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"}},"required":["id"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{id}/archive',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_DeleteStoryMap', {
    name: 'StoryMaps_DeleteStoryMap',
    description: `Delete a story map and everything on it. This is permanent — prefer StoryMaps_ArchiveStoryMap unless deletion is explicitly intended.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid"}},"required":["id"]},
    method: 'delete',
    pathTemplate: '/api/planning/story-maps/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  // -------------------------------------------------------------------------------------------
  // Goals
  // -------------------------------------------------------------------------------------------

  ['StoryMaps_AddGoal', {
    name: 'StoryMaps_AddGoal',
    description: `Add a goal to a story map. The goal is created with one step already in it.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128}},"required":["name"]}},"required":["storyMapId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/goals',
    executionParameters: [{"name":"storyMapId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_RenameGoal', {
    name: 'StoryMaps_RenameGoal',
    description: `Rename a goal.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"goalId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128}},"required":["name"]}},"required":["storyMapId","goalId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/goals/{goalId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"goalId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_ReorderGoal', {
    name: 'StoryMaps_ReorderGoal',
    description: `Reorder a goal within the map.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"goalId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"newOrder":{"type":"number","format":"int32","description":"Zero-based target position."}},"required":["newOrder"]}},"required":["storyMapId","goalId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/goals/{goalId}/order',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"goalId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_DeleteGoal', {
    name: 'StoryMaps_DeleteGoal',
    description: `Delete a goal, along with its steps and their tasks.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"goalId":{"type":"string","format":"uuid"}},"required":["storyMapId","goalId"]},
    method: 'delete',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/goals/{goalId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"goalId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_SetGoalPersonas', {
    name: 'StoryMaps_SetGoalPersonas',
    description: `Set the personas tagged on a goal. Replaces the full set — pass every persona ID that should remain tagged.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"goalId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"personaIds":{"type":"array","items":{"type":"string","format":"uuid"}}},"required":["personaIds"]}},"required":["storyMapId","goalId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/goals/{goalId}/personas',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"goalId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  // -------------------------------------------------------------------------------------------
  // Steps
  // -------------------------------------------------------------------------------------------

  ['StoryMaps_AddStep', {
    name: 'StoryMaps_AddStep',
    description: `Add a step to a goal.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"goalId":{"type":"string","format":"uuid"},"name":{"type":"string","maxLength":128}},"required":["goalId","name"]}},"required":["storyMapId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/steps',
    executionParameters: [{"name":"storyMapId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_RenameStep', {
    name: 'StoryMaps_RenameStep',
    description: `Rename a step.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"stepId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128}},"required":["name"]}},"required":["storyMapId","stepId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/steps/{stepId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"stepId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_ReorderStep', {
    name: 'StoryMaps_ReorderStep',
    description: `Reorder a step within its goal.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"stepId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"newOrder":{"type":"number","format":"int32","description":"Zero-based target position within the goal."}},"required":["newOrder"]}},"required":["storyMapId","stepId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/steps/{stepId}/order',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"stepId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_MoveStep', {
    name: 'StoryMaps_MoveStep',
    description: `Move a step into a different goal.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"stepId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"targetGoalId":{"type":"string","format":"uuid"},"newOrder":{"type":"number","format":"int32","description":"Zero-based target position within the target goal."}},"required":["targetGoalId","newOrder"]}},"required":["storyMapId","stepId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/steps/{stepId}/move',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"stepId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_DeleteStep', {
    name: 'StoryMaps_DeleteStep',
    description: `Delete a step and its tasks.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"stepId":{"type":"string","format":"uuid"}},"required":["storyMapId","stepId"]},
    method: 'delete',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/steps/{stepId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"stepId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_SetStepPersonas', {
    name: 'StoryMaps_SetStepPersonas',
    description: `Set the personas tagged on a step. Replaces the full set — pass every persona ID that should remain tagged.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"stepId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"personaIds":{"type":"array","items":{"type":"string","format":"uuid"}}},"required":["personaIds"]}},"required":["storyMapId","stepId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/steps/{stepId}/personas',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"stepId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  // -------------------------------------------------------------------------------------------
  // Tasks
  // -------------------------------------------------------------------------------------------

  ['StoryMaps_AddTask', {
    name: 'StoryMaps_AddTask',
    description: `Add a task (story card) to a step. Without a swimLaneId, it lands in the default swim lane.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"stepId":{"type":"string","format":"uuid"},"title":{"type":"string","maxLength":128},"swimLaneId":{"type":["string","null"],"format":"uuid"}},"required":["stepId","title"]}},"required":["storyMapId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks',
    executionParameters: [{"name":"storyMapId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_UpdateTask', {
    name: 'StoryMaps_UpdateTask',
    description: `Update a task's title and description.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"title":{"type":"string","maxLength":128},"description":{"type":["string","null"],"maxLength":2048}},"required":["title"]}},"required":["storyMapId","taskId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_MoveTask', {
    name: 'StoryMaps_MoveTask',
    description: `Move a task to a step and swim lane.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"targetStepId":{"type":"string","format":"uuid"},"targetSwimLaneId":{"type":"string","format":"uuid"},"newOrder":{"type":"number","format":"int32","description":"Zero-based target position within the step/lane cell."}},"required":["targetStepId","targetSwimLaneId","newOrder"]}},"required":["storyMapId","taskId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/move',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_DeleteTask', {
    name: 'StoryMaps_DeleteTask',
    description: `Delete a task.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"}},"required":["storyMapId","taskId"]},
    method: 'delete',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_SetTaskPersonas', {
    name: 'StoryMaps_SetTaskPersonas',
    description: `Set the personas tagged on a task. Replaces the full set — pass every persona ID that should remain tagged.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"personaIds":{"type":"array","items":{"type":"string","format":"uuid"}}},"required":["personaIds"]}},"required":["storyMapId","taskId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/personas',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  // -------------------------------------------------------------------------------------------
  // Checklist
  // -------------------------------------------------------------------------------------------

  ['StoryMaps_AddChecklistItem', {
    name: 'StoryMaps_AddChecklistItem',
    description: `Add a checklist item to a task. Returns the updated task.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128}},"required":["name"]}},"required":["storyMapId","taskId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/checklist',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_RenameChecklistItem', {
    name: 'StoryMaps_RenameChecklistItem',
    description: `Rename a checklist item.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"itemId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128}},"required":["name"]}},"required":["storyMapId","taskId","itemId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/checklist/{itemId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"},{"name":"itemId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_SetChecklistItemChecked', {
    name: 'StoryMaps_SetChecklistItemChecked',
    description: `Check or uncheck a checklist item.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"itemId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"isChecked":{"type":"boolean"}},"required":["isChecked"]}},"required":["storyMapId","taskId","itemId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/checklist/{itemId}/checked',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"},{"name":"itemId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_RemoveChecklistItem', {
    name: 'StoryMaps_RemoveChecklistItem',
    description: `Remove a checklist item from a task.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"itemId":{"type":"string","format":"uuid"}},"required":["storyMapId","taskId","itemId"]},
    method: 'delete',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/checklist/{itemId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"},{"name":"itemId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_PromoteChecklistItem', {
    name: 'StoryMaps_PromoteChecklistItem',
    description: `Promote a checklist item into its own task in the same step. Returns the new task.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"itemId":{"type":"string","format":"uuid"}},"required":["storyMapId","taskId","itemId"]},
    method: 'post',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/checklist/{itemId}/promote',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"},{"name":"itemId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  // -------------------------------------------------------------------------------------------
  // Work item links
  // -------------------------------------------------------------------------------------------

  ['StoryMaps_LinkWorkItem', {
    name: 'StoryMaps_LinkWorkItem',
    description: `Link a task to an existing work item. A work item can be linked to at most one task per map.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"workItemId":{"type":"number","format":"int32","description":"The work item's integer ID."}},"required":["workItemId"]}},"required":["storyMapId","taskId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/work-item-link',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_UnlinkWorkItem', {
    name: 'StoryMaps_UnlinkWorkItem',
    description: `Unlink a task from its work item.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"taskId":{"type":"string","format":"uuid"}},"required":["storyMapId","taskId"]},
    method: 'delete',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/tasks/{taskId}/work-item-link',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"taskId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  // -------------------------------------------------------------------------------------------
  // Swim lanes
  // -------------------------------------------------------------------------------------------

  ['StoryMaps_AddSwimLane', {
    name: 'StoryMaps_AddSwimLane',
    description: `Add a swim lane, appended below the existing ones.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128}},"required":["name"]}},"required":["storyMapId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/swim-lanes',
    executionParameters: [{"name":"storyMapId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_RenameSwimLane', {
    name: 'StoryMaps_RenameSwimLane',
    description: `Rename a swim lane. The default swim lane cannot be renamed.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"swimLaneId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128}},"required":["name"]}},"required":["storyMapId","swimLaneId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/swim-lanes/{swimLaneId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"swimLaneId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_SetSwimLaneDates', {
    name: 'StoryMaps_SetSwimLaneDates',
    description: `Set a swim lane's descriptive start and end dates. Pass null to clear a date.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"swimLaneId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"startDate":{"type":["string","null"],"format":"date","description":"ISO date string (YYYY-MM-DD)."},"endDate":{"type":["string","null"],"format":"date","description":"ISO date string (YYYY-MM-DD)."}}}},"required":["storyMapId","swimLaneId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/swim-lanes/{swimLaneId}/dates',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"swimLaneId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_ReorderSwimLane', {
    name: 'StoryMaps_ReorderSwimLane',
    description: `Reorder a swim lane. The default swim lane cannot be reordered.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"swimLaneId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"newOrder":{"type":"number","format":"int32","description":"Zero-based target position."}},"required":["newOrder"]}},"required":["storyMapId","swimLaneId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/swim-lanes/{swimLaneId}/order',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"swimLaneId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_RemoveSwimLane', {
    name: 'StoryMaps_RemoveSwimLane',
    description: `Remove a swim lane. Its tasks return to the default swim lane; the response is the number of tasks moved.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"swimLaneId":{"type":"string","format":"uuid"}},"required":["storyMapId","swimLaneId"]},
    method: 'delete',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/swim-lanes/{swimLaneId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"swimLaneId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

  // -------------------------------------------------------------------------------------------
  // Personas
  // -------------------------------------------------------------------------------------------

  ['StoryMaps_AddPersona', {
    name: 'StoryMaps_AddPersona',
    description: `Define a persona on the map.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128},"description":{"type":["string","null"],"maxLength":256},"color":{"type":"string","description":"Hex color, e.g. #1677ff."}},"required":["name","color"]}},"required":["storyMapId","requestBody"]},
    method: 'post',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/personas',
    executionParameters: [{"name":"storyMapId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_UpdatePersona', {
    name: 'StoryMaps_UpdatePersona',
    description: `Update a persona's name, description, and color.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"personaId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"name":{"type":"string","maxLength":128},"description":{"type":["string","null"],"maxLength":256},"color":{"type":"string","description":"Hex color, e.g. #1677ff."}},"required":["name","color"]}},"required":["storyMapId","personaId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/personas/{personaId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"personaId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_ReorderPersona', {
    name: 'StoryMaps_ReorderPersona',
    description: `Reorder a persona within the map's persona list.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"personaId":{"type":"string","format":"uuid"},"requestBody":{"type":"object","properties":{"newOrder":{"type":"number","format":"int32","description":"Zero-based target position."}},"required":["newOrder"]}},"required":["storyMapId","personaId","requestBody"]},
    method: 'put',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/personas/{personaId}/order',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"personaId","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
  }],

  ['StoryMaps_DeletePersona', {
    name: 'StoryMaps_DeletePersona',
    description: `Delete a persona and strip its tag from every goal, step, and task. The response is the number of nodes untagged.`,
    inputSchema: {"type":"object","properties":{"storyMapId":{"type":"string","format":"uuid"},"personaId":{"type":"string","format":"uuid"}},"required":["storyMapId","personaId"]},
    method: 'delete',
    pathTemplate: '/api/planning/story-maps/{storyMapId}/personas/{personaId}',
    executionParameters: [{"name":"storyMapId","in":"path"},{"name":"personaId","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
  }],

];
