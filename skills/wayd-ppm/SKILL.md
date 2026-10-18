---
name: wayd-ppm
description: Read before calling the Wayd MCP tools named Portfolios_*, Programs_*, Projects_*, Tasks_*, ProjectLifecycles_*, StrategicInitiatives_* or ExpenditureCategories_*. Guides agents working with Wayd portfolio, program, project, and task management via the Wayd MCP server. Use when looking up or creating and updating portfolios, programs, or projects; exploring project lifecycles, stages, the project plan, or team; approving, activating, completing, cancelling, closing, archiving, or reverting any of those records or a strategic initiative; creating, updating, or deleting tasks within a project; reviewing project scores or a portfolio's ranking board; exploring strategic initiatives and recording KPI measurements; or recommending, logging, or correcting project health checks.
---

# Wayd PPM (Portfolio / Program / Project / Task Management)

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

## Reference

Read the matching file before working in these areas. The rules on this page still apply there.

| File                                                       | Read it when                                                                                                     |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| [reference/status-changes.md](reference/status-changes.md) | Approving, activating, completing, cancelling, closing, or archiving a portfolio, program, or project, or reverting a project |
| [reference/tasks.md](reference/tasks.md)                   | Creating, updating, or deleting a task, or adding or removing a task dependency                                  |
| [reference/health-checks.md](reference/health-checks.md)   | Reviewing, recommending, logging, correcting, or deleting a project health check                                 |
| [reference/initiatives-kpis.md](reference/initiatives-kpis.md) | Exploring strategic initiatives, changing an initiative's status, reading KPI progress, or recording a measurement |
| [reference/scoring.md](reference/scoring.md)               | Project scores and portfolio ranking (read-only)                                                                 |

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
- **Lifecycle** — a reusable template of ordered stages, with a state of `Proposed`, `Active` or `Archived`. Only Active lifecycles can be assigned. Through MCP a lifecycle can only be set at creation (`projectLifecycleId` on `Projects_Create`); otherwise it is assigned in the Wayd UI.
- **Stage** — one stage of a project's plan, from its lifecycle, with its own status, `start`/`end`, and `progress`.
- **Task** — type `Task` (planned `plannedStart`/`plannedEnd`, `progress` 0–100) or `Milestone` (a single `plannedDate`, no progress). Every task sits under a stage or under another task. Dependencies are finish-to-start.

## Common patterns

- **Ids and keys.** A parameter named `idOrKey` (or `projectIdOrKey`, `taskIdOrKey`) accepts a UUID or a key. On portfolio, program, project, and task tools, a parameter named `id` takes a **UUID only** — this covers every record update, `Projects_GetStatusHistory`, and the stage, health-check, and scoring tools. Read the UUID off a read tool first.
- **Status filters** take status names (`Active`, `Completed`, …), which each tool's schema lists. The status tools — `Portfolios_GetPortfolioStatuses`, `Programs_GetProgramStatuses`, `Projects_GetStatuses`, `Tasks_GetTaskStatuses`, `StrategicInitiatives_GetStatuses` — describe each one; its `code` is the name a filter takes.
- **Role filters** take `Sponsor`, `Owner`, `Manager`, `Member` or `Assignee` (assigned a task on the project). There is no lookup tool for these.
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
| Evidence for a health check (hosted)     | `Projects_GetHealthCheckEvidence` (see [reference/health-checks.md](reference/health-checks.md)) |

When surveying a portfolio or program, call `Projects_GetProjectsPlanSummaries` once with every project UUID and `allTasks: true` — not `Projects_GetProjectPlanSummary` per project. Without `allTasks` it counts only the tasks the caller can see.

## Creating and updating records

| Goal                                             | Tool                                                                                           |
| ------------------------------------------------ | ---------------------------------------------------------------------------------------------- |
| Create                                           | `Portfolios_Create`, `Programs_Create`, `Projects_Create`                                      |
| Change some fields or roles (hosted)             | `Portfolios_Patch`, `Programs_Patch`, `Projects_Patch`                                         |
| Update the whole record                          | `Portfolios_Update`, `Programs_Update`, `Projects_Update`                                      |
| Move a project to another program, or out of one | `Projects_ChangeProgram` (target must be in the same portfolio; `null` detaches)               |
| Change a project's key                           | `Projects_ChangeKey` — only when the user explicitly asks; it breaks task keys and saved links |

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
