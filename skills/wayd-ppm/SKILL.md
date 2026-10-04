---
name: wayd-ppm
description: Guides agents working with Wayd portfolio, program, project, and task management via the Wayd MCP server. Use when looking up or creating and updating portfolios, programs, or projects; exploring project lifecycles, stages, the project plan, or team; approving, activating, completing, cancelling, closing, archiving, or reverting any of those records or a strategic initiative; creating, updating, or deleting tasks within a project; reviewing project scores or a portfolio's ranking board; exploring strategic initiatives and recording KPI measurements; or recommending, logging, or correcting project health checks.
---

# Wayd PPM (Portfolio / Program / Project / Task Management)

## Reference files

Read the matching file before working in these areas:

- **Project health checks** — reviewing, recommending, logging, correcting, or deleting one: [health-checks.md](health-checks.md)
- **Strategic initiatives and KPIs** — reading KPI progress or recording a measurement: [kpis.md](kpis.md)
- **Project scores and portfolio ranking** (read-only): [scoring.md](scoring.md)

## What MCP can change

Portfolios, programs, and projects can be created, updated, and moved through their statuses; tasks support full create, update, and delete. Read-only: project lifecycles and stages, scores and rankings, and strategic initiative records and KPI definitions. Nothing deletes a portfolio, program, or project.

## Entity model

```
Portfolio
├── Strategic Initiative ── KPIs ── Checkpoints (plan) / Measurements (actuals)
│        └── linked Projects (many-to-many)
└── Program (optional)
    └── Project
        ├── Lifecycle (template) → Stages
        │                            └── Tasks / Milestones
        │                                  └── child Tasks (via parentId)
        ├── Team (employees in project roles)
        └── Work Items (with Monte Carlo forecast)
```

- **Project** — belongs to one portfolio and at most one program. Has a unique `key` (2–20 uppercase letters and digits), a committed timeline `start`/`end`, a status, an optional lifecycle, and an embedded current `healthCheck`. There is no project-level progress field; progress lives on stages and tasks.
- **Lifecycle** — a reusable template of ordered stages, with a state `1=Proposed`, `2=Active`, `3=Archived`. Only Active lifecycles can be assigned. Through MCP a lifecycle can only be set at creation (`projectLifecycleId` on `Projects_Create`); otherwise it is assigned in the Wayd UI.
- **Stage** — one stage of a project's plan, from its lifecycle, with its own status, `start`/`end`, and `progress`.
- **Task** — type `Task` (planned `plannedStart`/`plannedEnd`, `progress` 0–100) or `Milestone` (a single `plannedDate`, no progress). Every task sits under a stage or under another task. Dependencies are finish-to-start.

## Common patterns

- **Ids and keys.** A parameter named `idOrKey` (or `projectIdOrKey`, `taskIdOrKey`) accepts a UUID or a key. On portfolio, program, project, and task tools, a parameter named `id` takes a **UUID only** — this covers every record update, `Projects_GetStatusHistory`, and the stage, health-check, and scoring tools. Read the UUID off a read tool first.
- **Status enums.** Status filters take integer arrays. Resolve the values first with `Portfolios_GetPortfolioStatuses`, `Programs_GetProgramStatuses`, `Projects_GetStatuses`, `Tasks_GetTaskStatuses`, or `StrategicInitiatives_GetStatuses`.
- **Role filters.** `1=Sponsor`, `2=Owner`, `3=Manager`, `4=Member`, `5=Task Assignee`. There is no lookup tool for these.
- **People are employee ids.** Role lists, task `assigneeIds`, `employeeId` filters, and people in activity payloads all take or return employee ids, never user ids. Resolve a name with `employee.id` from `Users_GetUsers` — see the `wayd-users` skill.
- **Delivery leadership.** Changing a portfolio, program, or project — updates, status transitions, key and program changes — and writing project health checks require the caller to be an **Owner or Manager** of the record or of an ancestor (project ← program ← portfolio). Sponsors and Members do not qualify, and a permission alone is not enough. Strategic initiative transitions and task changes check permissions only. When a call is rejected as unauthorized, check the record's role lists before retrying.

## Finding records

| Goal                                                       | Tool                                                                                                                 |
| ---------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Portfolios                                                 | `Portfolios_GetPortfolios`, `Portfolios_GetPortfolio`                                                                |
| Portfolio name → UUID                                      | `Portfolios_GetPortfolioOptions` (lightweight `{ id, name }`)                                                        |
| A portfolio's programs / projects                          | `Portfolios_GetPortfolioPrograms`, `Portfolios_GetPortfolioProjects`                                                 |
| Programs                                                   | `Programs_GetPrograms`, `Programs_GetProgram`                                                                        |
| A program's projects                                       | `Programs_GetProgramProjects`                                                                                        |
| Projects (filter by status, portfolio, role, `employeeId`) | `Projects_GetProjects`, `Projects_GetProject`                                                                        |
| Project status history                                     | `Projects_GetStatusHistory` (UUID only; carries the reason for each revert)                                          |
| Lifecycles and their stages                                | `ProjectLifecycles_GetProjectLifecycles`, `ProjectLifecycles_GetProjectLifecycle`                                    |
| Everything that changed on a record                        | `Portfolios_GetActivities`, `Programs_GetActivities`, `Projects_GetActivities`, `StrategicInitiatives_GetActivities` |

### Activity history

Use activity history for "what changed?", "who moved this date?", or "when did the owner change?" — the record itself holds only its current state.

- Parse `payload` (a JSON string) for detail; each change carries its before and after values, so one entry answers what moved.
- A `Baseline` entry is where tracking began for a record that already existed. Do not report it as the record's creation.
- Each record has its own history. A project's covers its details, key, program, lifecycle, timeline, roles, themes, status, health checks, and scores — **not its tasks or stages**. A program's does not include its projects' changes.
- Results are paged; check `hasNextPage` before concluding something never happened.

## Someone's work

For the **caller**, use `Projects_GetMyProjectsSummary` (project counts per role) and `Projects_GetMyProjectsTaskMetrics` (overdue, due this week, upcoming task counts). Neither takes a user parameter.

For a **colleague**, get their employee id (see Common patterns), then use `Projects_GetTaskMetrics` with `employeeId` for task counts, and `Projects_GetProjects` with `employeeId` (plus a `role` filter to narrow it) for the projects themselves.

The metrics tools return counts only; follow up with `Projects_GetProjects` when the user wants the list.

## A project's plan and team

| Goal                                     | Tool                                                                     |
| ---------------------------------------- | ------------------------------------------------------------------------ |
| Full plan: stages with nested tasks, WBS | `Projects_GetProjectPlanTree` (prefer this for a hierarchical view)      |
| Plan metrics for one project             | `Projects_GetProjectPlanSummary`                                         |
| Plan metrics for many projects           | `Projects_GetProjectsPlanSummaries`                                      |
| Stages                                   | `Projects_GetProjectStages`, `Projects_GetProjectStage`                  |
| Team: roles, stages, active task counts  | `Projects_GetProjectTeam`                                                |
| Linked work items                        | `Projects_GetWorkItems`                                                  |
| When the work items will be done         | `Projects_GetForecast` (read it as the `wayd-teams` skill describes)     |
| Tasks, one task, critical path           | `Tasks_GetProjectTasks`, `Tasks_GetProjectTask`, `Tasks_GetCriticalPath` |

When surveying a portfolio or program, call `Projects_GetProjectsPlanSummaries` once with every project UUID and `allTasks: true` — not `Projects_GetProjectPlanSummary` per project. Without `allTasks` it counts only the tasks the caller can see.

## Tasks

A project needs an assigned lifecycle before it can hold tasks.

### Creating a task

1. Resolve `typeId`, `statusId`, and `priorityId` with `Tasks_GetTaskTypes`, `Tasks_GetTaskStatuses`, and `Tasks_GetTaskPriorities` (in parallel).
2. Choose `parentId` — **required**. Use the stage's id (from `Projects_GetProjectStages`) for a top-level task in that stage, or another task's id to nest it beneath that task.
3. Resolve assignees to employee ids (see Common patterns).
4. Set the type-specific fields:
   - **Task**: `progress` is required (use `0` for new work); dates go in `plannedStart` and `plannedEnd`.
   - **Milestone**: `plannedDate` is required; omit `progress`, `plannedStart`, and `plannedEnd`.
5. Call `Tasks_CreateProjectTask`.

### Updating a task

`Tasks_UpdateProjectTask` overwrites the whole task: an omitted description, effort, date, or assignee list is cleared. An omitted or empty `assigneeIds` removes every assignee.

1. Read the task with `Tasks_GetProjectTask`.
2. Build the body from what you read, changing only what the user asked for:
   - `statusId` = `status.id`, `priorityId` = `priority.id`
   - `parentId` = `parentId` if set, otherwise `projectStageId` — required
   - `assigneeIds` = every `assignees[].id`
   - `progress` — required for a Task; omit for a Milestone
   - `name`, `description`, `plannedStart`, `plannedEnd`, `plannedDate`, `estimatedEffortHours` as read
3. Call `Tasks_UpdateProjectTask`. The task type cannot be changed.

### Deleting tasks and managing dependencies

- **Delete**: `Tasks_DeleteProjectTask`. Confirm with the user first.
- **Add a dependency** (finish-to-start): `Tasks_AddTaskDependency` with the predecessor's id as the path `id` and `{ predecessorId, successorId }` in the body.
- **Remove a dependency**: `Tasks_RemoveTaskDependency` with the predecessor's `id` and the `successorId`.

## Creating and updating records

| Goal                                             | Tool                                                                                           |
| ------------------------------------------------ | ---------------------------------------------------------------------------------------------- |
| Create                                           | `Portfolios_Create`, `Programs_Create`, `Projects_Create`                                      |
| Update                                           | `Portfolios_Update`, `Programs_Update`, `Projects_Update`                                      |
| Move a project to another program, or out of one | `Projects_ChangeProgram` (target must be in the same portfolio; `null` detaches)               |
| Change a project's key                           | `Projects_ChangeKey` — only when the user explicitly asks; it breaks task keys and saved links |

New records start in **Proposed**. Creating a project needs an `expenditureCategoryId` from `ExpenditureCategories_GetOptions`. Updates never change status, a project's key, program, or lifecycle, or a program's portfolio.

### Updating a record (checklist)

Every update overwrites the whole record: an omitted field is cleared. Role lists replace that role's membership, and **an omitted or empty list removes everyone in that role** — a rename that leaves them out strips every Owner and Manager, leaving a record nobody is authorized to manage.

1. **Read** the record: `Portfolios_GetPortfolio`, `Programs_GetProgram`, or `Projects_GetProject`.
2. **Copy every role list and theme set** into the request as employee or theme ids:
   - Portfolio: `sponsorIds`, `ownerIds`, `managerIds`
   - Program: `sponsorIds`, `ownerIds`, `managerIds`, `strategicThemeIds`
   - Project: `sponsorIds`, `ownerIds`, `managerIds`, `memberIds`, `strategicThemeIds`
3. **Copy every other field** as read (name, description, dates, and for a project `expenditureCategoryId`, `businessCase`, `expectedBenefits`).
4. **Apply** only the requested change.
5. **Show the user** any role that would lose members, by name, and confirm before calling.
6. **Call** the update.
7. **Re-read** the record and verify the Owners and Managers are intact and the change landed.

## Changing status

Status changes only through dedicated transition tools, never through an update. **Confirm every transition with the user first**, naming the record and the transition: others rely on the status, and portfolio, program, and initiative transitions cannot be undone.

| Record               | Tools and allowed from                                                                                                                                                                                   |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Portfolio            | `Portfolios_Activate` (Proposed), `Portfolios_Close` (Active or OnHold), `Portfolios_Archive` (Closed)                                                                                                   |
| Program              | `Programs_Activate` (Proposed), `Programs_Complete` (Active), `Programs_Cancel` (any status not already closed)                                                                                          |
| Project              | `Projects_Approve` (Proposed), `Projects_Activate` (Proposed or Approved), `Projects_Complete` (Active), `Projects_Cancel` (any status not already closed), `Projects_RevertStatus` (below)              |
| Strategic initiative | `StrategicInitiatives_Approve` (Proposed), `StrategicInitiatives_Activate` (Approved), `StrategicInitiatives_Complete` (Active or OnHold), `StrategicInitiatives_Cancel` (any status not already closed) |

Every transition tool takes the record's UUID, not its key. No forward transition records a reason; the status history shows who and when, but not why.

### Prerequisites that cause rejections

Check these first so a transition fails in conversation rather than at the API:

- **Project approve** — a lifecycle is assigned.
- **Project or program activate and complete** — both a start and an end date are set.
- **Program complete, or cancel from Active** — every project in it is completed or canceled (`Programs_GetProgramProjects`).
- **Portfolio archive** — the portfolio is Closed.

### Side effects

- **`Portfolios_Activate` stamps the start date as today and `Portfolios_Close` stamps the end date as today.** Neither can be backdated, so never use them to tidy up a portfolio that really started or ended on another date.
- **Completing or cancelling a strategic initiative closes it**: its KPIs and linked projects are frozen, though measurements can still be recorded.

### Moving a project backwards

`Projects_RevertStatus` is the only way back to an earlier status — reopening a completed or canceled project, or returning an active one to Approved. Programs and portfolios cannot be reverted.

1. Read `backwardStatusTargets` from `Projects_GetProject` and offer only those. Do not derive targets: a status keeps its entry requirements in either direction (Approved needs a lifecycle, Active needs both dates), so a project cancelled straight from Proposed may only return to Proposed.
2. If the project's program or portfolio is closed, the revert is rejected — that parent must be reopened first.
3. Agree a `reason` with the user. It is required and stored in the status history as the record of why a decision was reversed, so write something a later reader will understand — never "revert" or "fix".
4. Confirm, then call with `toStatus` and `reason`.
