# @wayd/mcp

## 0.3.0

### Minor Changes

- 3e9dd9c: Add MCP tools for reading, creating, and managing Story Maps.

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

## 0.2.0

### Minor Changes

- fab40f5: Add MCP tools for inspecting and creating health checks on planning interval objectives and PPM projects.

  **New tools:**

  - `PlanningIntervals_GetObjectiveHealthChecks` — get the full health check history for a PI objective (newest first)
  - `PlanningIntervals_GetObjectiveHealthCheck` — get a specific PI objective health check by ID
  - `PlanningIntervals_CreateObjectiveHealthCheck` — log a new health check on a PI objective (`statusId` 1=Healthy, 2=AtRisk, 3=Unhealthy); auto-expires the previously active check
  - `Projects_GetProjectHealthChecks` — get the full health check history for a project (newest first)
  - `Projects_GetProjectHealthCheck` — get a specific project health check by ID
  - `Projects_CreateProjectHealthCheck` — log a new health check on a project (`status` "Healthy" / "AtRisk" / "Unhealthy"); auto-expires the previously active check

## 0.1.0

### Minor Changes

- bcba8b3: Add project lifecycle tools, new project read endpoints, and remove the deprecated task tree tool.

  **New tools:**

  - `ProjectLifecycles_GetProjectLifecycles` — list project lifecycles with optional state filter (Proposed/Active/Archived)
  - `ProjectLifecycles_GetProjectLifecycle` — get project lifecycle details including phases
  - `Projects_GetProjectTeam` — get team members for a project with their roles, assigned phases, and active workload
  - `Projects_GetProjectPhases` — get all phases for a project
  - `Projects_GetProjectPhase` — get details for a specific project phase
  - `Projects_GetProjectPlanTree` — get a unified plan tree with phases as top-level nodes and tasks nested within (replaces the removed task tree endpoint)
  - `Projects_GetProjectPlanSummary` — get summary metrics for a project's plan (overdue, due this week, upcoming, total task counts)

  **Updated tools:**

  - `Projects_GetProjects` — added `role` query filter to scope results to projects where the current user holds a specific role (Sponsor/Owner/Manager/Member)

  **Removed tools:**

  - `Tasks_GetProjectTaskTree` — the underlying API endpoint was removed; use `Projects_GetProjectPlanTree` instead for a richer unified view of phases and tasks
