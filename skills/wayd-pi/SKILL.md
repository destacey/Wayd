---
name: wayd-pi
description: Guides agents working with Wayd Planning Intervals — iterations, sprint metrics, team objectives, health recommendations and reports, predictability, and risks.
---

# Wayd Planning Intervals (PI)

## When to use

- Finding or exploring planning intervals and their iterations
- Getting sprint mappings, iteration metrics, or iteration backlogs
- Listing or analyzing team objectives and their linked work items
- Generating PI health reports or predictability summaries
- Recommending, reviewing, or logging health checks on individual PI objectives (Healthy / AtRisk / Unhealthy)
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

- Team-scoped goal and commitment for a PI
- **Name**: The actual commitment made by the team
- **Dates**: Target start date (`startDate`) and target end date (`targetDate`), falling within the PI dates. If either date is not specified, it defaults to the planning interval's start and end dates.
- **Description**: Narrative scope, intent, and context for the commitment
- **Progress**: Current overall progress value (percentage)
- **Status**: Progress state (call `PlanningIntervals_GetObjectiveStatuses` to resolve values)
- **Linked work items**: Work items contributing to the objective, categorized into Proposed, Active, and Done with total counts
- **Cumulative Flow Diagram (CFD)**: Daily rollup metrics tracking Proposed, Active, and Done counts over time to reveal flow velocity, WIP accumulation, or stagnation
- **Forecast**: Monte Carlo completion simulation against the target date (or PI end)
- **Health checks**: Historical and active point-in-time RAG assessments (Healthy / AtRisk / Unhealthy) with reporter, expiration, and notes

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

| Goal                                | Tool                                    | Required params                 |
| ----------------------------------- | --------------------------------------- | ------------------------------- |
| Sprint mappings for the whole PI    | `PlanningIntervals_GetIterationSprints` | `idOrKey`                       |
| Sprint mappings for one iteration   | `PlanningIntervals_GetIterationSprints` | `idOrKey`, `iterationId` (UUID) |
| Aggregated metrics for an iteration | `PlanningIntervals_GetIterationMetrics` | `idOrKey`, `iterationIdOrKey`   |
| Combined backlog for an iteration   | `PlanningIntervals_GetIterationBacklog` | `idOrKey`, `iterationIdOrKey`   |

### Objectives

- List objectives (all teams): `PlanningIntervals_GetObjectives` with `idOrKey`
- List objectives for one team: add `teamId` (UUID) filter
- Get a specific objective: `PlanningIntervals_GetObjective` with `idOrKey` + `objectiveIdOrKey` (returns name/commitment, description, status, progress, start and target dates, and active health check)
- Resolve objective status values: `PlanningIntervals_GetObjectiveStatuses`
- Work items linked to an objective: `PlanningIntervals_GetObjectiveWorkItems` (returns work item list and `progressSummary` with Proposed, Active, Done, and Total counts)
- Daily work item metrics for an objective (CFD): `PlanningIntervals_GetObjectiveWorkItemMetrics` (returns daily rollups of Proposed, Active, Done, and Total from PI start to today/PI end — Cumulative Flow Diagram data)
- When an objective's work will be done, and its chance of finishing by the objective's target date (else the PI's end): `PlanningIntervals_GetObjectiveForecast`. Requires the `delivery-forecasting` feature flag; see the `wayd-teams` skill for reading forecasts.

### Health report and per-objective health checks

The PI-wide health report is a dedicated endpoint — do not attempt to derive it from objectives manually.

| Goal                                                      | Tool                                           | Notes                                                                                                  |
| --------------------------------------------------------- | ---------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Bulk RAG snapshot (latest check per objective, all teams) | `PlanningIntervals_GetObjectivesHealthReport`  | Takes `idOrKey`. Add `teamId` to scope to one team.                                                    |
| Full health check history for one objective               | `PlanningIntervals_GetObjectiveHealthChecks`   | Takes PI `id` and `objectiveId` (both UUIDs). Newest first.                                            |
| One specific health check                                 | `PlanningIntervals_GetObjectiveHealthCheck`    | Takes PI `id`, `objectiveId`, and `healthCheckId`.                                                     |
| Log a new health check on an objective                    | `PlanningIntervals_CreateObjectiveHealthCheck` | Takes PI `id` and `objectiveId`; body: `{ planningIntervalObjectiveId, statusId, expiration, note? }`. |

Notes for logging a check:

- `statusId` is a **number**: `1=Healthy, 2=AtRisk, 3=Unhealthy` (asymmetric with the project version, which takes a string `status`).
- `expiration` is an ISO 8601 UTC datetime and **must be in the future**.
- `note` is optional, max 1024 characters.
- The body redundantly requires `planningIntervalObjectiveId` in addition to the path `objectiveId` — they must match.
- Logging a new check automatically expires the previously active check; only one non-expired check can exist at a time.

### Recommending and logging an objective health check (recipe)

When asked to evaluate an objective, recommend its health, or log a health check, gather evidence across the objective's commitment dates, work items, CFD, forecast, and history before formulating a recommendation.

#### 1. Gather objective context and evidence

Run these queries to assemble the complete picture:

1. **Objective details and timeline:** `PlanningIntervals_GetObjective` with `idOrKey` + `objectiveIdOrKey`.
   - Name (the actual commitment), description, status, current progress percentage.
   - Start and target dates (`startDate`, `targetDate`). If either is unset, call `PlanningIntervals_GetPlanningInterval` and use the PI's `start` and `end` dates as defaults.
2. **Linked work items and progress summary:** `PlanningIntervals_GetObjectiveWorkItems`.
   - Inspect `progressSummary` (`proposed`, `active`, `done`, `total`) and individual item details.
3. **Cumulative Flow Diagram (CFD) metrics:** `PlanningIntervals_GetObjectiveWorkItemMetrics`.
   - Daily snapshots of `proposed`, `active`, and `done` counts.
   - Flow observation: Are items moving steadily from Proposed → Active → Done? Is WIP (`active`) ballooning or flatlining?
4. **Delivery forecast:** `PlanningIntervals_GetObjectiveForecast`.
   - Read `outcome` (Forecast, Done, Blocked by Dependency, Not Enough History).
   - Check `chanceOfFinishingByTargetDate` against the objective's target date (or PI end).
   - Check the 85% confidence completion date against the target date.
   - Identify pacing blockers from `dependencies[].shareOfTrialsSettingFinish`.
5. **Past health check history:** `PlanningIntervals_GetObjectiveHealthChecks`.
   - Inspect the most recent check: status, `reportedOn` timestamp, expiration, and previous `note`.
6. **Activity history since last check:** `PlanningIntervals_GetObjectiveActivities`.
   - Identify what changed since the previous health check (work completed, status changes, progress delta, scope adjustments).
7. **Active risks (optional context):** `PlanningIntervals_GetRisks` with `teamId` for open risks impacting the objective.

#### 2. Formulate the recommendation

- **Assess RAG status (`statusId`):**
  - **Healthy (`1`)**: Progress is on track relative to elapsed time; CFD shows steady throughput and controlled WIP; forecast indicates high probability of finishing by target date (typically >= 80–85%); no critical blocking dependencies or open high-severity risks.
  - **AtRisk (`2`)**: Progress is lagging behind elapsed schedule; CFD shows bottlenecked WIP or slowed completions; forecast confidence is marginal (e.g. 50–80%) or the 85% date slightly exceeds the target date; or an external dependency/risk threatens delivery if unmitigated.
  - **Unhealthy (`3`)**: Progress is stalled or severely behind schedule; CFD shows flatlined completion; forecast probability is low (< 50%), the 85% date extends far beyond the target date/PI end, or outcome is `Blocked by Dependency`; critical unresolved blockers exist.
- **Summarize progress since the last check:**
  - Clearly state changes since the last check's `reportedOn` date (e.g., number of work items moved to Done, progress % change, scope additions). If this is the first check, summarize overall progress and velocity to date.
- **Call out specific concerns:**
  - Forecast date slippage compared to target end date.
  - Dependencies that disproportionately govern completion (`shareOfTrialsSettingFinish`).
  - Stagnant WIP or flow bottlenecks visible in the CFD.
  - Relevant team risks or predictability shortfalls.
- **Draft the proposed `note` (max 1024 characters):**
  - Synthesize a concise note combining progress made, current forecast confidence, and any concerns.
- **Determine `expiration`:**
  - Recommend a future UTC ISO datetime (e.g., end of the current iteration/sprint, or 1–2 weeks out, not exceeding the PI end date).

#### 3. Present to the user before submitting

**Never log a health check without user confirmation.** Present the recommendation clearly:

- **Recommended Status:** Healthy / At Risk / Unhealthy (with statusId)
- **Proposed Expiration:** UTC datetime
- **Proposed Note:** Full drafted text for the note
- **Rationale & Analysis:**
  - **Progress since last check:** Key deltas in work items and progress percentage
  - **CFD & Flow observation:** Trend in Active vs Done items
  - **Forecast & Timeline:** Probability of meeting target date and 85% confidence date
  - **Concerns / Blockers:** Specific dependencies, risks, or slip factors

Ask the user to confirm the recommendation or provide any adjustments (change status, revise expiration, or edit note) before calling `PlanningIntervals_CreateObjectiveHealthCheck`.

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
