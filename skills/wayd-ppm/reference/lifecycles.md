# Lifecycles and project stages

A **lifecycle** is a reusable template of ordered stages. Assigning one to a project creates the project's own **stages** from it, and every task sits in one of those stages. The template and the project's stages are separate records: editing a project stage never changes the template, and a template can be edited only before anyone can use it.

## A project's lifecycle

| Goal                                  | Tool                                                          |
| ------------------------------------- | ------------------------------------------------------------- |
| Pick a lifecycle                      | `ProjectLifecycles_GetProjectLifecycles` with `state: Active` |
| See its stages                        | `ProjectLifecycles_GetProjectLifecycle`                       |
| Give a project its first lifecycle    | `Projects_AssignLifecycle`                                    |
| Switch a project to another lifecycle | `Projects_ChangeLifecycle`                                    |

Both take the project's UUID, need an **Active** lifecycle, are rejected for a completed or canceled project, and require delivery leadership. A project needs a lifecycle before it can be approved or hold tasks.

### Changing a project's lifecycle (checklist)

`Projects_ChangeLifecycle` **replaces every stage**: the old stages are removed with their status, dates, progress and assignees, and new stages are created empty from the new lifecycle. Tasks are kept and moved.

1. Read the current stages with `Projects_GetProjectStages`, and the tasks in each with `Projects_GetProjectPlanTree`.
2. Read the new lifecycle's stages with `ProjectLifecycles_GetProjectLifecycle`.
3. Build `stageMapping`: each current **project stage id** → the **lifecycle stage id** in the new lifecycle its tasks should move to. Every current stage that holds tasks must be mapped.
4. Show the user the mapping, and the stage status, dates, progress and assignees that will be lost. Confirm.
5. Call `Projects_ChangeLifecycle`, then re-read the stages and set any status, dates or assignees the user wants on the new stages.

## Updating a project stage

Use `Projects_PatchStage` for a partial change (see [tasks.md](tasks.md)). To replace the whole stage, `Projects_UpdateStage` overwrites every field:

1. Read it with `Projects_GetProjectStage` (project UUID and stage UUID).
2. Send back every field: `description` (required), `status` = `status.id` (a task status id from `Tasks_GetTaskStatuses`), `plannedStart` and `plannedEnd` = `start` and `end` (both or neither), `progress`, and `assigneeIds` = every `assignees[].id`.
3. Change only what the user asked for. An omitted date is cleared, an omitted progress becomes 0, and an omitted or empty `assigneeIds` removes every assignee.

A stage's dates must cover its dated top-level tasks. Its name comes from the lifecycle and cannot be changed. Stage updates check the project update permission only.

## Lifecycle templates

| Goal                         | Tool                                                                                           |
| ---------------------------- | ---------------------------------------------------------------------------------------------- |
| Create, with optional stages | `ProjectLifecycles_Create` (returns the id)                                                    |
| Rename or redescribe         | `ProjectLifecycles_Update` (overwrites both fields)                                            |
| Add, edit, or remove a stage | `ProjectLifecycles_AddStage`, `ProjectLifecycles_UpdateStage`, `ProjectLifecycles_RemoveStage` |
| Reorder stages               | `ProjectLifecycles_ReorderStages` (every stage id, once, in the new order)                     |
| Make it assignable           | `ProjectLifecycles_Activate` (needs at least one stage)                                        |
| Retire it                    | `ProjectLifecycles_Archive` (Active only; cannot be undone)                                    |
| Delete it                    | `ProjectLifecycles_Delete` (Proposed only)                                                     |

- **Only a Proposed lifecycle can change.** Its name, description and stages are editable until `ProjectLifecycles_Activate`, which freezes the template for good. Build and review the full stage list before activating.
- **Archiving** stops new assignments; projects already using the lifecycle keep it and their stages. An Active lifecycle cannot be deleted — archive it.
- Write tools take the lifecycle's UUID; the read tools also accept its numeric key.
- Lifecycles are shared templates, so confirm every change with the user.
