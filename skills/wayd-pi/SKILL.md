---
name: wayd-pi
description: Guides agents working with Wayd Planning Intervals — iterations, sprint metrics, team objectives, health reports, predictability, and risks.
---

# Wayd Planning Intervals (PI)

## When to use

- Finding or exploring planning intervals and their iterations
- Getting sprint mappings, iteration metrics, or iteration backlogs
- Listing or analyzing team objectives and their linked work items
- Generating PI health reports or predictability summaries
- Reviewing or logging health checks on individual PI objectives (Healthy / AtRisk / Unhealthy)
- Reviewing PI risks
- Finding out what changed on a PI or an objective, and who changed it

---

## Entity context

### Hierarchy

```
Planning Interval (PI)
├── Iterations
│   └── Sprints (mapped from external systems, e.g. Azure DevOps)
├── Teams
│   └── Objectives
│       └── Work Items (linked)
└── Risks
```

### Planning Interval

- A time-boxed planning period (analogous to a Program Increment in SAFe)
- Contains iterations and has teams participating

### Iteration

- Sub-period within a PI (e.g. Sprint 1, Sprint 2)
- Has a category (call `PlanningIntervals_GetIterationCategories` to resolve values)
- Maps to external sprints for metrics aggregation

### Objective

- Team-scoped goal for a PI
- Has a status (call `PlanningIntervals_GetObjectiveStatuses` to resolve values)
- Can have linked work items with daily metrics

### Risk

- Scoped to a PI; optionally scoped to a team
- Open risks returned by default; closed risks must be explicitly requested (`includeClosed: true`)

### Common patterns

- **`idOrKey`** — most GET endpoints accept either a UUID or a string key
- **`teamId`** — optional filter on many PI endpoints; omit to get all teams, include to scope to one
- **`includeClosed`** on `GetRisks` — defaults to `false`; pass `true` to include resolved risks

---

## Instructions

### Finding PIs and iterations

1. List all PIs: `PlanningIntervals_GetList`
2. Get PI details: `PlanningIntervals_GetPlanningInterval` with `idOrKey`
3. Get PI calendar: `PlanningIntervals_GetCalendar` with `idOrKey`
4. List iterations in a PI: `PlanningIntervals_GetIterations`
5. Get a specific iteration: `PlanningIntervals_GetIteration` with `idOrKey` + `iterationIdOrKey`
6. Resolve iteration category values: `PlanningIntervals_GetIterationCategories`

### Sprint mappings and iteration metrics

| Goal | Tool | Required params |
|---|---|---|
| Sprint mappings for the whole PI | `PlanningIntervals_GetIterationSprints` | `idOrKey` |
| Sprint mappings for one iteration | `PlanningIntervals_GetIterationSprints` | `idOrKey`, `iterationId` (UUID) |
| Aggregated metrics for an iteration | `PlanningIntervals_GetIterationMetrics` | `idOrKey`, `iterationIdOrKey` |
| Combined backlog for an iteration | `PlanningIntervals_GetIterationBacklog` | `idOrKey`, `iterationIdOrKey` |

### Objectives

- List objectives (all teams): `PlanningIntervals_GetObjectives` with `idOrKey`
- List objectives for one team: add `teamId` (UUID) filter
- Get a specific objective: `PlanningIntervals_GetObjective` with `idOrKey` + `objectiveIdOrKey`
- Resolve objective status values: `PlanningIntervals_GetObjectiveStatuses`
- Work items linked to an objective: `PlanningIntervals_GetObjectiveWorkItems`
- Daily work item metrics for an objective: `PlanningIntervals_GetObjectiveWorkItemMetrics`

### Health report and per-objective health checks

The PI-wide health report is a dedicated endpoint — do not attempt to derive it from objectives manually.

| Goal | Tool | Notes |
|---|---|---|
| Bulk RAG snapshot (latest check per objective, all teams) | `PlanningIntervals_GetObjectivesHealthReport` | Takes `idOrKey`. Add `teamId` to scope to one team. |
| Full health check history for one objective | `PlanningIntervals_GetObjectiveHealthChecks` | Takes PI `id` and `objectiveId` (both UUIDs). Newest first. |
| One specific health check | `PlanningIntervals_GetObjectiveHealthCheck` | Takes PI `id`, `objectiveId`, and `healthCheckId`. |
| Log a new health check on an objective | `PlanningIntervals_CreateObjectiveHealthCheck` | Takes PI `id` and `objectiveId`; body: `{ planningIntervalObjectiveId, statusId, expiration, note? }`. |

Notes for logging a check:

- `statusId` is a **number**: `1=Healthy, 2=AtRisk, 3=Unhealthy` (asymmetric with the project version, which takes a string `status`).
- `expiration` is an ISO 8601 UTC datetime and **must be in the future**.
- `note` is optional, max 1024 characters.
- The body redundantly requires `planningIntervalObjectiveId` in addition to the path `objectiveId` — they must match.
- Logging a new check automatically expires the previously active check; only one non-expired check can exist at a time.

### Activity history

- A PI's changes (details, dates, teams, iterations, sprint mappings, objectives locked or unlocked): `PlanningIntervals_GetActivities` with `idOrKey`
- One objective's changes (details, status, progress, order, stretch, timeline, health checks): `PlanningIntervals_GetObjectiveActivities` with `idOrKey` + `objectiveIdOrKey`

Both return entries newest first, paged (at most 100 per page). Each entry has a `category`, a `summary`, who made the change and when, and a `payload` JSON string holding both the old and new value. A `Baseline` entry marks where tracking began for an objective or PI that already existed; nothing before it is recorded. People inside a payload are employee ids.

### Predictability

- All teams: `PlanningIntervals_GetPredictability` with `idOrKey`
- One team: `PlanningIntervals_GetTeamPredictability` with `idOrKey` + `teamId` (UUID)
- If `teamId` is unknown, call `PlanningIntervals_GetTeams` first to list participating teams.

### Risks

- `PlanningIntervals_GetRisks` — defaults to open risks only
- Pass `includeClosed: true` to include resolved/closed risks
- Pass `teamId` to scope to one team

If the user seems to be missing risks, suggest adding `includeClosed: true`.

### PI health report recipe (compound task)

When the user asks for a comprehensive PI health summary, run these in parallel then synthesize:

1. `PlanningIntervals_GetPlanningInterval` — PI dates, name, metadata
2. `PlanningIntervals_GetObjectivesHealthReport` — objective status across teams
3. `PlanningIntervals_GetPredictability` — predictability scores per team
4. `PlanningIntervals_GetRisks` — active risks (add `includeClosed: true` if a full picture is needed)
