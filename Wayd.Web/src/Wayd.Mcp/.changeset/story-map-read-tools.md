---
'@wayd/mcp': minor
---

Add MCP tools for reading, creating, and managing Story Maps.

**Read tools:**

- `StoryMaps_GetStoryMaps` — list story maps (id, key, name, description, status, owner), with an `includeArchived` option
- `StoryMaps_GetStoryMap` — get a story map in full by ID or key: goals, each with ordered steps and tasks (including checklists, persona tags, and linked work item IDs), plus the map's swim lanes and personas

**Management tools:**

- Maps — `StoryMaps_CreateStoryMap`, `StoryMaps_UpdateStoryMap`, `StoryMaps_ChangeStoryMapOwner`, `StoryMaps_ArchiveStoryMap`, `StoryMaps_DeleteStoryMap`
- Goals — `StoryMaps_AddGoal`, `StoryMaps_RenameGoal`, `StoryMaps_ReorderGoal`, `StoryMaps_DeleteGoal`, `StoryMaps_SetGoalPersonas`
- Steps — `StoryMaps_AddStep`, `StoryMaps_RenameStep`, `StoryMaps_ReorderStep`, `StoryMaps_MoveStep`, `StoryMaps_DeleteStep`, `StoryMaps_SetStepPersonas`
- Tasks — `StoryMaps_AddTask`, `StoryMaps_UpdateTask`, `StoryMaps_MoveTask`, `StoryMaps_DeleteTask`, `StoryMaps_SetTaskPersonas`
- Checklists — `StoryMaps_AddChecklistItem`, `StoryMaps_RenameChecklistItem`, `StoryMaps_SetChecklistItemChecked`, `StoryMaps_RemoveChecklistItem`, `StoryMaps_PromoteChecklistItem`
- Work item links — `StoryMaps_LinkWorkItem`, `StoryMaps_UnlinkWorkItem`
- Swim lanes — `StoryMaps_AddSwimLane`, `StoryMaps_RenameSwimLane`, `StoryMaps_SetSwimLaneDates`, `StoryMaps_ReorderSwimLane`, `StoryMaps_RemoveSwimLane`
- Personas — `StoryMaps_AddPersona`, `StoryMaps_UpdatePersona`, `StoryMaps_ReorderPersona`, `StoryMaps_DeletePersona`

Also adds a `wayd-story-maps` agent skill covering story map analysis (scope per swim lane, persona coverage, checklist progress, work item traceability) and safe management workflows (UUID resolution, replace-set persona tagging, destructive-operation cautions).
