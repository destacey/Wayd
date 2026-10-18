# Project tasks

A project needs an assigned lifecycle before it can hold tasks.

## Creating a task

1. Resolve `typeId`, `statusId`, and `priorityId` with `Tasks_GetTaskTypes`, `Tasks_GetTaskStatuses`, and `Tasks_GetTaskPriorities` (in parallel).
2. Choose `parentId` — **required**. Use the stage's id (from `Projects_GetProjectStages`) for a top-level task in that stage, or another task's id to nest it beneath that task.
3. Resolve assignees to employee ids (see Common patterns in SKILL.md).
4. Set the type-specific fields:
   - **Task**: `progress` is required (use `0` for new work); dates go in `plannedStart` and `plannedEnd`.
   - **Milestone**: `plannedDate` is required; omit `progress`, `plannedStart`, and `plannedEnd`.
5. Call `Tasks_CreateProjectTask`.

## Updating a task

**When `Tasks_PatchProjectTask` is available (the hosted server), use it**: send only the change as a JSON Patch, and every field it does not name keeps its value — e.g. `[{ "op": "add", "path": "/assigneeIds/-", "value": "<employeeId>" }]` to add an assignee, `[{ "op": "replace", "path": "/statusId", "value": <id from Tasks_GetTaskStatuses> }]` to change status. To remove an assignee, `replace` `/assigneeIds` with everyone who stays (read the task first). `Projects_PatchStage` patches a stage the same way.

Otherwise, `Tasks_UpdateProjectTask` overwrites the whole task: an omitted description, effort, date, or assignee list is cleared. An omitted or empty `assigneeIds` removes every assignee.

1. Read the task with `Tasks_GetProjectTask`.
2. Build the body from what you read, changing only what the user asked for:
   - `statusId` = `status.id`, `priorityId` = `priority.id`
   - `parentId` = `parentId` if set, otherwise `projectStageId` — required
   - `assigneeIds` = every `assignees[].id`
   - `progress` — required for a Task; omit for a Milestone
   - `name`, `description`, `plannedStart`, `plannedEnd`, `plannedDate`, `estimatedEffortHours` as read
3. Call `Tasks_UpdateProjectTask`. The task type cannot be changed.

## Deleting tasks and managing dependencies

- **Delete**: `Tasks_DeleteProjectTask`. Confirm with the user first.
- **Add a dependency** (finish-to-start): `Tasks_AddTaskDependency` with the predecessor's id as the path `id` and `{ predecessorId, successorId }` in the body.
- **Remove a dependency**: `Tasks_RemoveTaskDependency` with the predecessor's `id` and the `successorId`.
