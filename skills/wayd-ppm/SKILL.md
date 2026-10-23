---
name: wayd-ppm
description: Read before calling the Wayd MCP tools named Portfolios_*, Programs_*, Projects_*, Tasks_*, ProjectLifecycles_*, StrategicInitiatives_*, ExpenditureCategories_* or ScoringModels_*. Guides agents working with Wayd portfolio, program, project, and task management via the Wayd MCP server. Use when looking up, creating, updating, or deleting portfolios, programs, or projects; exploring the project plan or team; assigning or changing a project's lifecycle or updating its stages; creating, activating, or archiving lifecycle templates; approving, activating, completing, cancelling, closing, archiving, or reverting any of those records or a strategic initiative; creating, updating, moving, or deleting tasks within a project; scoring a project, finding a scoring model and assigning it to a portfolio, or reordering its ranking board; creating, updating, or deleting strategic initiatives, their KPIs and checkpoint plans, or linking projects to them; recording KPI measurements; managing expenditure categories; or recommending, logging, or correcting project health checks.
---

# Wayd PPM (Portfolio / Program / Project / Task Management)

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

## Reference

Read the matching file before working in these areas. The rules on this page still apply there.

| File                                                           | Read it when                                                                                                                                           |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [reference/status-changes.md](reference/status-changes.md)     | Approving, activating, completing, cancelling, closing, or archiving a portfolio, program, or project, reverting a project, or deleting any of them    |
| [reference/lifecycles.md](reference/lifecycles.md)             | Assigning or changing a project's lifecycle, updating a project stage, or managing lifecycle templates                                                 |
| [reference/tasks.md](reference/tasks.md)                       | Creating, updating, moving, or deleting a task, or adding or removing a task dependency                                                                |
| [reference/health-checks.md](reference/health-checks.md)       | Reviewing, recommending, logging, correcting, or deleting a project health check                                                                       |
| [reference/initiatives-kpis.md](reference/initiatives-kpis.md) | Strategic initiatives: creating, updating, deleting, changing status, linking projects, managing KPIs and checkpoint plans, or recording a measurement |
| [reference/scoring.md](reference/scoring.md)                   | Scoring a project, finding or assigning a portfolio's scoring model, or reading or reordering a portfolio's ranking                                    |

## What MCP can change

Portfolios, programs, and projects can be created, updated, moved through their statuses, and — while still Proposed — deleted. Tasks, project stages, lifecycle templates, strategic initiatives with their KPIs, checkpoint plans and project links, scores, rankings, and expenditure categories can all be changed.

The tools that delete a portfolio, program, or project, or change lifecycles, stages, scores, rankings, scoring models, initiative details and project links, KPI definitions and checkpoint plans, task placement, or expenditure categories, and the scoring-model reads (`ScoringModels_*`), are offered only by the hosted endpoint (`/mcp` on the Wayd API), not by the `@wayd/mcp` package. When a tool named here is not in your tool list, say the change has to be made in the Wayd UI.

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
- **Lifecycle** — a reusable template of ordered stages, with a state of `Proposed`, `Active` or `Archived`. Only Active lifecycles can be assigned: at creation (`projectLifecycleId` on `Projects_Create`) or later with `Projects_AssignLifecycle` / `Projects_ChangeLifecycle` — see [reference/lifecycles.md](reference/lifecycles.md).
- **Stage** — one stage of a project's plan, from its lifecycle, with its own status, `start`/`end`, and `progress`.
- **Task** — type `Task` (planned `plannedStart`/`plannedEnd`, `progress` 0–100) or `Milestone` (a single `plannedDate`, no progress). Every task sits under a stage or under another task. Dependencies are finish-to-start.

## Common patterns

- **Ids and keys.** A parameter named `idOrKey` (or `projectIdOrKey`, `taskIdOrKey`) accepts a UUID or a key. On portfolio, program, project, and task tools, a parameter named `id` takes a **UUID only** — this covers every record update, `Projects_GetStatusHistory`, and the stage, health-check, and scoring tools. Read the UUID off a read tool first.
- **Status filters** take status names (`Active`, `Completed`, …), which each tool's schema lists. The status tools — `Portfolios_GetPortfolioStatuses`, `Programs_GetProgramStatuses`, `Projects_GetStatuses`, `Tasks_GetTaskStatuses`, `StrategicInitiatives_GetStatuses` — describe each one; its `code` is the name a filter takes.
- **Role filters** take `Sponsor`, `Owner`, `Manager`, `Member` or `Assignee` (assigned a task on the project). There is no lookup tool for these.
- **People are employee ids.** Role lists, task `assigneeIds`, `employeeId` filters, and people in activity payloads all take or return employee ids, never user ids. Resolve a name with `employee.id` from `Users_GetUsers` — see the `wayd-users` skill.
- **Delivery leadership.** Changing a portfolio, program, or project — updates, status transitions, key, program and lifecycle changes, scoring, a portfolio's scoring model and ranking — and writing project health checks require the caller to be an **Owner or Manager** of the record or of an ancestor (project ← program ← portfolio). Sponsors and Members do not qualify, and a permission alone is not enough. Deletes, stage updates, task changes, lifecycle templates, expenditure categories, and everything on strategic initiatives check permissions only. When a call is rejected as unauthorized, check the record's role lists before retrying.

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

| Goal                                     | Tool                                                                                             |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Full plan: stages with nested tasks, WBS | `Projects_GetProjectPlanTree` (prefer this for a hierarchical view)                              |
| Plan metrics for one project             | `Projects_GetProjectPlanSummary`                                                                 |
| Plan metrics for many projects           | `Projects_GetProjectsPlanSummaries`                                                              |
| Stages                                   | `Projects_GetProjectStages`, `Projects_GetProjectStage`                                          |
| Team: roles, stages, active task counts  | `Projects_GetProjectTeam`                                                                        |
| Linked work items                        | `Projects_GetWorkItems`                                                                          |
| When the work items will be done         | `Projects_GetForecast` (read it as the `wayd-teams` skill describes)                             |
| Tasks, one task, critical path           | `Tasks_GetProjectTasks`, `Tasks_GetProjectTask`, `Tasks_GetCriticalPath`                         |
| Evidence for a health check (hosted)     | `Projects_GetHealthCheckEvidence` (see [reference/health-checks.md](reference/health-checks.md)) |

When surveying a portfolio or program, call `Projects_GetProjectsPlanSummaries` once with every project UUID and `allTasks: true` — not `Projects_GetProjectPlanSummary` per project. Without `allTasks` it counts only the tasks the caller can see.

## Creating and updating records

| Goal                                             | Tool                                                                                                                       |
| ------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| Create                                           | `Portfolios_Create`, `Programs_Create`, `Projects_Create`                                                                  |
| Change some fields or roles (hosted)             | `Portfolios_Patch`, `Programs_Patch`, `Projects_Patch`                                                                     |
| Update the whole record                          | `Portfolios_Update`, `Programs_Update`, `Projects_Update`                                                                  |
| Move a project to another program, or out of one | `Projects_ChangeProgram` (target must be in the same portfolio; `null` detaches)                                           |
| Change a project's key                           | `Projects_ChangeKey` — only when the user explicitly asks; it breaks task keys and saved links                             |
| Assign or change a project's lifecycle (hosted)  | `Projects_AssignLifecycle`, `Projects_ChangeLifecycle` — see [reference/lifecycles.md](reference/lifecycles.md)            |
| Delete a proposed record (hosted)                | `Portfolios_Delete`, `Programs_Delete`, `Projects_Delete` — see [reference/status-changes.md](reference/status-changes.md) |

New records start in **Proposed**. Creating a project needs an `expenditureCategoryId` from `ExpenditureCategories_GetOptions`. Updates never change status, a project's key, program, or lifecycle, or a program's portfolio. Status changes only through dedicated transition tools — see [reference/status-changes.md](reference/status-changes.md).

Tasks follow the same rule as records: patch with `Tasks_PatchProjectTask` when it is available; otherwise `Tasks_UpdateProjectTask` overwrites the whole task, and an omitted or empty `assigneeIds` removes every assignee. See [reference/tasks.md](reference/tasks.md).

### Patching a record (hosted server)

When `Projects_Patch` (or `Programs_Patch`, `Portfolios_Patch`) is available, use it for any partial change. The body is a JSON Patch over the update's fields; anything it does not name keeps its value, role lists included.

- Rename: `[{ "op": "replace", "path": "/name", "value": "Apollo 2" }]`
- Add a manager: `[{ "op": "add", "path": "/managerIds/-", "value": "<employeeId>" }]` (likewise `/sponsorIds`, `/ownerIds`, `/memberIds`, `/strategicThemeIds`)
- Remove someone from a role: read the record, then `replace` the whole list with everyone who stays — never remove by index.
- Dates: `replace` `/start` and `/end` together.

Show the user the change, naming anyone who would lose a role, and confirm before calling. Delivery leadership still applies.

### Updating a record (checklist)

Without a patch tool, every update overwrites the whole record: an omitted field is cleared. Role lists replace that role's membership, and **an omitted or empty list removes everyone in that role** — a rename that leaves them out strips every Owner and Manager, leaving a record nobody is authorized to manage.

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

## Expenditure categories (hosted)

An expenditure category classifies how a project's spending is treated (Opex, Capex, …). Categories are identified by a **numeric id**, not a UUID.

| Goal                         | Tool                                                                                             |
| ---------------------------- | ------------------------------------------------------------------------------------------------ |
| Pick one for a project       | `ExpenditureCategories_GetOptions`                                                               |
| Full list or one category    | `ExpenditureCategories_GetExpenditureCategories`, `ExpenditureCategories_GetExpenditureCategory` |
| Create, or overwrite details | `ExpenditureCategories_Create`, `ExpenditureCategories_Update`                                   |
| Change state                 | `ExpenditureCategories_Activate` (Proposed), `ExpenditureCategories_Archive` (Active)            |
| Delete                       | `ExpenditureCategories_Delete` (Proposed only, and not used by any project)                      |

- A new category is **Proposed**, and a project can be given only an **Active** one, on create or when its category changes; a project keeps a category archived after it was assigned.
- **Activating fixes `isCapitalizable` and `requiresDepreciation`.** After that, `ExpenditureCategories_Update` must send them unchanged; name, description and accounting code stay editable. Archived categories cannot be updated or reactivated.
- `ExpenditureCategories_Update` overwrites every field: read the category first and send back what should stay, or an omitted accounting code is cleared and an omitted flag becomes false.
- Categories are shared by every project, so confirm any change with the user.
