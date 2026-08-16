---
name: wayd-ppm
description: Guides agents working with Wayd Portfolio, Program, Project, and Task management via the Wayd MCP server. Use when looking up portfolios, programs, or projects, exploring project lifecycles and phases, viewing the project plan or team, reviewing project scores or a portfolio's ranking board, or creating, updating, or managing tasks within a project.
---

# Wayd PPM (Portfolio / Program / Project / Task Management)

## When to use

- Finding or listing portfolios, programs, or projects
- Understanding what projects or programs are in a portfolio
- Exploring project lifecycles and their phases
- Viewing a project's plan tree, phases, team, or plan summary metrics
- Listing, creating, updating, or deleting tasks within a project
- Managing task hierarchies, dependencies, or the critical path
- Reviewing or logging project health checks (Healthy / AtRisk / Unhealthy)
- Reviewing project scores and a portfolio's ranking board

> **Note:** Portfolios, programs, lifecycles, and phases are **read-only** via MCP — only GET and LIST operations are exposed. Tasks support full CRUD (create, update, delete). Project **health checks** are read+create (no update or delete from MCP). **Scoring and ranking are read-only** — scores cannot be recorded and ranks cannot be reordered from MCP. The rest of the project surface remains read-only.

---

## Entity context

### Hierarchy

```
Portfolio
└── Program (optional grouping)
    └── Project
        ├── Lifecycle (optional — defines the phases a project moves through)
        │   └── Phases (ordered stages of the project plan)
        │       └── Tasks (leaf tasks assigned to a phase)
        ├── Team Members (employees with project roles)
        ├── Work Items
        └── Tasks
            └── Subtasks (nested via parentId)
```

### Portfolio

- Top-level container for programs and projects
- Has a status (integer enum — call `Portfolios_GetPortfolioStatuses` to resolve values)

### Program

- Groups related projects under a portfolio; projects can exist without one
- Has a status (integer enum — call `Programs_GetProgramStatuses` to resolve values)

### Project

- Must belong to a portfolio; optionally belongs to a program
- Has a unique string `key` (2–20 uppercase alphanumeric, e.g. `MYPROJ`)
- Has a status (integer enum — call `Projects_GetStatuses` to resolve values)
- May have an assigned lifecycle that defines its phases

### Project Lifecycle

- A reusable template that defines an ordered set of named phases
- Has a **state**: `1=Proposed`, `2=Active`, `3=Archived`
- Only `Active` lifecycles can be assigned to projects
- Phases within a lifecycle are ordered and named (e.g. Initiation, Planning, Execution, Closure)

### Project Phase

- A stage of a specific project's plan, derived from its assigned lifecycle
- Has a **status**, date range, progress, and assignees
- Tasks in the project are associated with a phase

### Task

- Scoped to a project; accessed via `projectIdOrKey` (UUID or string key)
- Has a **type** (call `Tasks_GetTaskTypes` to resolve), **status** (`Tasks_GetTaskStatuses`), and **priority** (`Tasks_GetTaskPriorities`)
- Two task types: `Task` and `Milestone` — behavior differs per type:
  - Tasks: use `plannedStart`/`plannedEnd` and `progress` (0.0–100.0); can be nested under another task via `parentId` (this is how subtasks are modelled, not a separate type)
  - Milestones: use `plannedDate` instead; `progress` is not applicable
- Supports parent/child nesting via `parentId` (UUID of the parent task); nesting does not change the `typeId`
- `assigneeIds` — optional UUID array; resolve user names → UUIDs with `Users_GetUsers`
- `estimatedEffortHours` — optional decimal
- Dependencies are finish-to-start: predecessor must complete before successor starts
- `taskIdOrKey` — GET endpoints accept either a UUID or a string key

### Common patterns

- **`idOrKey`** — most GET endpoints accept either a UUID or a string key
- **Status filters** — take integer arrays; call the matching `*_GetStatuses` endpoint (e.g., portfolios, programs, projects, tasks) to resolve enum values
- **Role filters** — also take integer arrays; use the documented mapping `1=Sponsor, 2=Owner, 3=Manager, 4=Member` for project team roles (there is no `GetStatuses` endpoint for roles)
- **UUID references** — `portfolioId`, `programId`, etc. are always UUIDs; resolve name → UUID with list/options endpoints
- **`Portfolios_GetPortfolioOptions`** — lightweight `{ id, name }` list; prefer this over `GetPortfolios` when you only need a UUID lookup

---

## Instructions

### Listing and filtering

| Goal | Tool | Notes |
|---|---|---|
| All portfolios (optionally by status) | `Portfolios_GetPortfolios` | |
| Portfolio details | `Portfolios_GetPortfolio` | |
| Portfolio name → UUID lookup | `Portfolios_GetPortfolioOptions` | |
| Programs in a portfolio | `Portfolios_GetPortfolioPrograms` | |
| Projects in a portfolio | `Portfolios_GetPortfolioProjects` | |
| All programs (cross-portfolio) | `Programs_GetPrograms` | |
| Projects in a program | `Programs_GetProgramProjects` | |
| All projects (cross-portfolio) | `Projects_GetProjects` | Optional `role` filter: `1=Sponsor, 2=Owner, 3=Manager, 4=Member` |
| Project details | `Projects_GetProject` | |
| Project status change history | `Projects_GetStatusHistory` | Takes project `id` (**UUID only** — unlike most project endpoints, it does not accept a key) |
| All project lifecycles | `ProjectLifecycles_GetProjectLifecycles` | Optional `state` filter: `1=Proposed, 2=Active, 3=Archived` |
| Project lifecycle details (with phases) | `ProjectLifecycles_GetProjectLifecycle` | `idOrKey` accepts UUID or integer key |

Before filtering by status, call `Projects_GetStatuses` (or `Programs_GetProgramStatuses` / `Portfolios_GetPortfolioStatuses`) to resolve the integer enum values.

### "What am I working on?"

Two tools are scoped to the **caller's own** PAT — neither takes a user parameter, and neither can report on anyone else. Prefer them over listing and filtering every project.

| Goal | Tool | Notes |
|---|---|---|
| My project involvement, by role | `Projects_GetMyProjectsSummary` | Counts only: total, sponsor, owner, manager, member, assignee. Optional `status` filter. |
| My open task counts | `Projects_GetMyProjectsTaskMetrics` | Overdue, due this week (through Saturday), upcoming (next Sunday–Saturday). Optional `status` and `role` filters. |

Both return aggregate counts, not the projects or tasks themselves — follow up with `Projects_GetProjects` (with a `role` filter) when the user wants the actual list.

### Plan metrics across many projects

`Projects_GetProjectsPlanSummaries` returns plan summaries for a set of projects in one call, keyed by project ID. Pass `projectId` as an array of **UUIDs** (keys are not accepted). Use it instead of calling `Projects_GetProjectPlanSummary` once per project — surveying a portfolio otherwise costs one round trip per project.

### Exploring a project's plan and team

| Goal | Tool | Notes |
|---|---|---|
| Project team members | `Projects_GetProjectTeam` | Returns roles, assigned phases, and active task count per member |
| All phases for a project | `Projects_GetProjectPhases` | Takes project `id` (UUID) |
| Single phase details | `Projects_GetProjectPhase` | Takes project `id` and `phaseId` (both UUIDs) |
| Unified plan tree (phases + tasks) | `Projects_GetProjectPlanTree` | Top-level nodes are phases; tasks nested within with WBS codes |
| Plan summary metrics | `Projects_GetProjectPlanSummary` | Returns overdue, due this week, upcoming, and total task counts; optional `employeeId` to scope to one person |
| Work items linked to a project | `Projects_GetWorkItems` | Takes project `id` (UUID) |

Prefer `Projects_GetProjectPlanTree` over `Tasks_GetProjectTasks` when you need a full hierarchical view of the project plan including phases.

### Listing and navigating tasks

| Goal | Tool | Notes |
|---|---|---|
| All tasks in a project | `Tasks_GetProjectTasks` | Optional `status` (int) and `parentId` (UUID) filters |
| Single task details | `Tasks_GetProjectTask` | `taskIdOrKey` accepts UUID or string key |
| Critical path | `Tasks_GetCriticalPath` | Returns ordered list of task UUIDs |

Before filtering by status, call `Tasks_GetTaskStatuses` to resolve the integer enum values.

### Creating a task

Required fields: `name`, `typeId`, `statusId`, `priorityId`

1. Resolve reference values first (can be done in parallel):
   - `Tasks_GetTaskTypes` → `typeId`
   - `Tasks_GetTaskStatuses` → `statusId`
   - `Tasks_GetTaskPriorities` → `priorityId`
2. For assignees, resolve user name → UUID with `Users_GetUsers`.
3. To nest a task under another (subtask pattern), provide `parentId` (UUID of the parent task) — the `typeId` stays `Task`.
4. For milestones: use `plannedDate`; omit `plannedStart`/`plannedEnd` and `progress`.
5. Call `Tasks_CreateProjectTask` with the assembled `requestBody`.

### Updating a task

`Tasks_UpdateProjectTask` — requires `id` (UUID), `name`, `statusId`, `priorityId` in the request body. All other fields are optional patches.

### Deleting a task

`Tasks_DeleteProjectTask` — requires `projectIdOrKey` and `id` (UUID).

### Managing dependencies

- **Add** (finish-to-start): `Tasks_AddTaskDependency` with `{ predecessorId, successorId }` — both UUIDs. Also pass the predecessor task's `id` as the path parameter.
- **Remove**: `Tasks_RemoveTaskDependency` with path params `id` (predecessor UUID) and `successorId`.

### Project scoring and portfolio ranking

Scoring is **read-only via MCP** — there is no tool to record a score.

A **scoring model** is assigned to a *portfolio*, and every project in it is scored against that model's criteria. A **score** is a frozen snapshot: the criterion ratings and computed outputs as they were at scoring time. Re-scoring a project adds a new entry to its history rather than editing the old one, so an old score reflects the model as it was then, not as it is now.

| Goal | Tool | Notes |
|---|---|---|
| A project's model, current score, and whether it can be scored | `Projects_GetScoringContext` | `scoringModel` is null when the portfolio has no model assigned — that project cannot be scored. |
| A project's full scoring history | `Projects_GetScores` | Headline values only, no per-criterion breakdown. |
| One score in full | `Projects_GetScore` | Every criterion rating and output value in the frozen snapshot. |
| Score breakdown across a portfolio | `Portfolios_GetRankingScoreboard` | The model definition plus per-project ratings and outputs. |

All four take **UUIDs only** — not project or portfolio keys.

Notes:

- A project's latest score is already embedded as `currentScore` on `Projects_GetProject` and `Projects_GetProjects`. Prefer those when you only need the headline number; use the scoring tools for history or per-criterion detail.
- In the ranking scoreboard, a project with empty `ratings` and `outputs` is either unscored **or** was last scored under a different or older model than the portfolio's current one. Do not read empty as "scored zero".
- The scoreboard returns score breakdowns keyed by project ID only — no names, no positions. Join it against `Portfolios_GetPortfolioProjects` to label rows.
- On project DTOs, `rank` is an **opaque fractional sort key**, not a displayed position — never show it to a user. The 1-based display position is `position`, which is only populated when results are scoped to a single portfolio (a cross-portfolio position would be meaningless).
- `canManageProject` on the project DTO indicates whether the caller could record a score, but recording one is not available through MCP.

### Project health checks

A health check records a point-in-time RAG assessment of a project (Healthy / AtRisk / Unhealthy) with a reporter, a note, and an expiration. Only one non-expired check is active at a time — logging a new check automatically expires the previous one.

| Goal | Tool | Notes |
|---|---|---|
| Full health check history for a project | `Projects_GetProjectHealthChecks` | Takes project `id` (UUID). Newest first. |
| One specific health check | `Projects_GetProjectHealthCheck` | Takes project `id` and `healthCheckId` (both UUIDs). |
| Log a new health check | `Projects_CreateProjectHealthCheck` | Takes project `id`; body: `{ status, expiration, note? }`. |

Notes for logging a check:

- `status` is a **string enum**: `"Healthy"`, `"AtRisk"`, or `"Unhealthy"` (asymmetric with the PI objective version, which takes a numeric `statusId`).
- `expiration` is an ISO 8601 UTC datetime and **must be in the future**.
- `note` is optional, max 1024 characters.
- Authorization (server-enforced): the caller must be an Owner or Manager of the project, the parent portfolio, or the parent program. Sponsors are intentionally excluded.

The current active check (if any) is also embedded in `Projects_GetProject` and `Projects_GetProjects` — prefer those when you only need the latest status, and use the dedicated tools when you need history or want to log a new check.
