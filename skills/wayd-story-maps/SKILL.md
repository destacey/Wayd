---
name: wayd-story-maps
description: Guides agents working with Wayd Story Maps — reading, analyzing, creating, and managing a map's goals, steps, tasks, swim lanes, and personas.
---

# Wayd Story Maps

## When to use

- Finding or listing story maps
- Reading a story map's full structure (goals, steps, tasks)
- Analyzing scope, slicing, or completeness of a story map
- Checking persona coverage or work item linkage across a map
- Creating a story map or building out its goals, steps, and tasks
- Reorganizing a map — reordering, moving cards between steps and lanes
- Managing swim lanes, personas, checklists, and work item links

---

## Entity context

### Structure

A story map is a single document with a fixed hierarchy:

| Level | Description |
|---|---|
| **Goal** | A top-level user goal (backbone column group), ordered left to right |
| **Step** | A step within a goal (backbone column), ordered within its goal |
| **Task** | A story card under a step, placed in exactly one swim lane |

Two cross-cutting concepts:

- **Swim lanes** — horizontal release/iteration slices. Every map has a default lane (`isDefault: true`); lanes may carry descriptive `startDate`/`endDate`. A task's `swimLaneId` says which slice it belongs to.
- **Personas** — user types defined on the map. Goals, steps, and tasks each carry a `personaIds` list of tagged personas.

Tasks may also have:

- A **checklist** (`checklist` items with `isChecked` state, plus `checklistCompletedCount` / `checklistTotalCount` rollups)
- A **linked work item** (`linkedWorkItemId`, the integer work item ID) — at most one task per map may link a given work item

### Common patterns

- **`idOrKey`** — `StoryMaps_GetStoryMap` accepts either the UUID `id` or the integer `key`. All mutation tools require UUIDs — call `StoryMaps_GetStoryMap` first to resolve them.
- **One call gets everything** — `StoryMaps_GetStoryMap` returns the entire map (goals → steps → tasks, swim lanes, personas). Never loop over sub-entities; there are no per-goal/per-step read endpoints.
- **Archived maps** — the list excludes archived maps unless `includeArchived: true`
- **Feature flag** — story maps are gated by the `StoryMaps` feature flag; if the instance has it disabled the endpoints return 404
- **Ordering** — `newOrder` values are zero-based positions among siblings
- **Persona sets replace** — `SetGoalPersonas` / `SetStepPersonas` / `SetTaskPersonas` replace the full tag set; include every persona ID that should remain

## Instructions

### Navigating story maps

1. List story maps: `StoryMaps_GetStoryMaps` (pass `includeArchived: true` to include archived ones)
2. Get the full map: `StoryMaps_GetStoryMap` with `idOrKey`

### Analyzing a map

All analysis works off the single `StoryMaps_GetStoryMap` response:

- **Scope per slice** — group tasks by `swimLaneId` and join to `swimLanes` for lane names, order, and dates
- **Persona coverage** — resolve `personaIds` on goals/steps/tasks against the map's `personas` list; nodes with an empty list are untagged
- **Progress signals** — use `checklistCompletedCount` vs `checklistTotalCount` per task
- **Traceability** — tasks with `linkedWorkItemId` are tied to work items; a null value means the task is unlinked
- **Ordering** — goals, steps, tasks, swim lanes, and personas all carry an `order` field; sort by it when presenting the map

### Building a map

1. Create the map: `StoryMaps_CreateStoryMap` (`name`, optional `description`) — returns the new `id` and `key`
2. Add goals: `StoryMaps_AddGoal` — **each new goal already contains one step**; rename it with `StoryMaps_RenameStep` instead of adding a duplicate
3. Add further steps: `StoryMaps_AddStep` (`goalId`, `name`)
4. Add tasks: `StoryMaps_AddTask` (`stepId`, `title`, optional `swimLaneId` — omit for the default lane)
5. Optionally add swim lanes (`StoryMaps_AddSwimLane`) and personas (`StoryMaps_AddPersona` — requires a hex `color`)

### Managing a map

- **Rename/describe** — `StoryMaps_UpdateStoryMap`, `StoryMaps_RenameGoal`, `StoryMaps_RenameStep`, `StoryMaps_UpdateTask`
- **Reorder** — `StoryMaps_ReorderGoal`, `StoryMaps_ReorderStep`, `StoryMaps_ReorderSwimLane`, `StoryMaps_ReorderPersona` (zero-based `newOrder`)
- **Move** — `StoryMaps_MoveStep` (to another goal), `StoryMaps_MoveTask` (to a step + swim lane)
- **Checklists** — `StoryMaps_AddChecklistItem`, `StoryMaps_RenameChecklistItem`, `StoryMaps_SetChecklistItemChecked`, `StoryMaps_RemoveChecklistItem`; `StoryMaps_PromoteChecklistItem` turns an item into a task in the same step
- **Work items** — `StoryMaps_LinkWorkItem` (integer `workItemId`), `StoryMaps_UnlinkWorkItem`
- **Ownership** — `StoryMaps_ChangeStoryMapOwner` (resolve the user ID with `Users_GetUsers`)

### Destructive operations — confirm before calling

- `StoryMaps_DeleteStoryMap` deletes the whole map permanently; prefer `StoryMaps_ArchiveStoryMap`
- `StoryMaps_DeleteGoal` removes its steps and their tasks; `StoryMaps_DeleteStep` removes its tasks
- `StoryMaps_RemoveSwimLane` keeps the tasks (they return to the default lane); the default lane can't be renamed, reordered, or removed
- `StoryMaps_DeletePersona` strips the tag from every node on the map
