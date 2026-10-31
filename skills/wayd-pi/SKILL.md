---
name: wayd-pi
description: Read before calling the Wayd MCP tools named PlanningIntervals_*. Guides agents working with Wayd Planning Intervals via the Wayd MCP server — iterations, teams, sprint mappings, capacity, metrics and backlogs, team objectives and their linked work items, objective health checks, PI health reports, predictability, and risks. Use when finding or exploring a PI or its iterations, creating a PI or changing its dates, iterations, teams or sprint mappings, reading PI capacity, metrics or backlog, listing, analyzing, creating, updating, reordering or deleting team objectives or linking their work items, locking or unlocking objectives, recommending, reviewing, logging, correcting or deleting a health check on an objective (Healthy / AtRisk / Unhealthy), building a PI health report or predictability summary, reviewing PI risks, or finding out what changed on a PI or an objective and who changed it.
---

# Wayd Planning Intervals (PI)

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

## Reference

Read the matching file before working in these areas. The rules on this page still apply there.

| File | Read it when |
| --- | --- |
| [reference/changing-a-pi.md](reference/changing-a-pi.md) | Creating or updating a PI, changing its dates, iterations, teams or sprint mappings, creating, updating, reordering or deleting objectives, linking their work items, or correcting or deleting a health check |

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

### Common patterns

- **`idOrKey`** — most GET endpoints accept either a UUID or a string key
- **`teamId`** — optional filter on many PI endpoints; omit to get all teams, include to scope to one

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
| Combined backlog for the whole PI   | `PlanningIntervals_GetBacklog`          | `idOrKey`                       |
| PI metrics per team                 | `PlanningIntervals_GetMetrics`          | `idOrKey`                       |
| Team capacity in person-days        | `PlanningIntervals_GetCapacity`         | `idOrKey`                       |

`PlanningIntervals_GetMetrics` rolls up cycle time, predictability and objective counts per team across every mapped sprint; only teams with mapped sprints appear. A 404 means the PI was not found, or a mapped sprint's team no longer resolves to a team with a code. `PlanningIntervals_GetCapacity` needs the `team-capacity-management` feature flag (404 when it is off): each team's person-days for the whole PI and per iteration, after holidays, time away and team days off. A team of teams counts its own allocations plus those of the teams beneath it, so never add a team-of-teams row to its teams' rows.

A Scrum team's sprints are mapped automatically when it joins a PI, each to the iteration holding most of its days, and only where the team had no mapping. In `PlanningIntervals_GetActivities` those mappings have `actorKind` `Automation`, and `employee` is whoever added the team, not someone who chose the mappings (none for a PI import). An iteration with no sprint for a Scrum team means no sprint lined up with it, the sprint was already mapped elsewhere, or the sprint or iteration was created after the team joined.

Reading iteration metrics: each sprint in `sprintMetrics` is measured in its team's `sizingMethod` (StoryPoints, Effort, Size or Count), so its `*Estimate` fields are in that unit; the `*WorkItems` fields are always item counts. The iteration's own `*Estimate` totals and `sizingMethod` are null when the teams size in different units — report counts then, and never add estimates across units yourself. `unestimatedWorkItems` counts items with no value in their team's unit (0 is an estimate).

Kanban teams have no sprints, so each one in the PI appears in `dateMeasuredTeamMetrics` instead, measured by the iteration's dates: what it completed within them and what it had in progress at their end. A Kanban team has no scope — no total, committed or not-started figures — so report those as not applicable, never as zero. Its completed and in-progress figures are in the iteration's totals but not in `totalWorkItems`, so take a completion rate from `sprintMetrics` alone. `totalEstimate` and `notStartedEstimate` are in `sprintSizingMethod`, the sprints' unit; `completedEstimate` and `inProgressEstimate` are in `sizingMethod`, the unit shared with the Kanban teams. When `isHistoryIncomplete` is true, say that the team's past in-progress figure may be short.

### Objectives

- List objectives (all teams): `PlanningIntervals_GetObjectives` with `idOrKey`
- List objectives for one team: add `teamId` (UUID) filter
- Get a specific objective: `PlanningIntervals_GetObjective` with `idOrKey` + `objectiveIdOrKey` (returns name/commitment, description, status, progress, start and target dates, and active health check)
- Resolve objective status values: `PlanningIntervals_GetObjectiveStatuses`
- Work items linked to an objective: `PlanningIntervals_GetObjectiveWorkItems` (returns work item list and `progressSummary` with Proposed, Active, Done, and Total counts)
- Daily work item metrics for an objective (CFD): `PlanningIntervals_GetObjectiveWorkItemMetrics` (returns daily rollups of Proposed, Active, Done, and Total from PI start to today/PI end — Cumulative Flow Diagram data)
- When an objective's work will be done, and its chance of finishing by the objective's target date (else the PI's end): `PlanningIntervals_GetObjectiveForecast`. Requires the `delivery-forecasting` feature flag (404 when it is off); see the `wayd-teams` skill for reading forecasts.

### Health report and per-objective health checks

The PI-wide health report is a dedicated endpoint — do not attempt to derive it from objectives manually.

| Goal                                                      | Tool                                           | Notes                                                                                                  |
| --------------------------------------------------------- | ---------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Bulk RAG snapshot (latest check per objective, all teams) | `PlanningIntervals_GetObjectivesHealthReport`  | Takes `idOrKey`. Add `teamId` to scope to one team.                                                    |
| Full health check history for one objective               | `PlanningIntervals_GetObjectiveHealthChecks`   | Takes PI `id` and `objectiveId` (both UUIDs). Newest first.                                            |
| One specific health check                                 | `PlanningIntervals_GetObjectiveHealthCheck`    | Takes PI `id`, `objectiveId`, and `healthCheckId`.                                                     |
| Log a new health check on an objective                    | `PlanningIntervals_CreateObjectiveHealthCheck` | Takes PI `id` and `objectiveId`; body: `{ planningIntervalObjectiveId, statusId, expiration, note? }`. |
| Correct an unexpired check                                | `PlanningIntervals_UpdateObjectiveHealthCheck` | Whole-record overwrite; see [reference/changing-a-pi.md](reference/changing-a-pi.md#health-checks).    |
| Delete a check                                            | `PlanningIntervals_DeleteObjectiveHealthCheck` | Leaves the objective with no active check if it was the active one.                                     |

Notes for logging a check:

- `statusId` is a **number**: `1=Healthy, 2=AtRisk, 3=Unhealthy`.
- `expiration` is an ISO 8601 UTC datetime and **must be in the future**.
- `note` is optional, max 1024 characters.
- The body redundantly requires `planningIntervalObjectiveId` in addition to the path `objectiveId` — they must match.
- The objective must belong to the PI in the path `id`; on every health-check tool a mismatch is refused as not found.
- Logging a new check automatically expires the previously active check; only one non-expired check can exist at a time.

### Recommending and logging an objective health check (recipe)

When asked to evaluate an objective, recommend its health, or log a health check, gather evidence across the objective's commitment dates, work items, CFD, forecast, and history before formulating a recommendation.

#### 1. Gather objective context and evidence

Call `PlanningIntervals_GetObjectiveHealthCheckEvidence` with `idOrKey` + `objectiveIdOrKey` (UUIDs or keys) — one read for everything below. It returns the PI and objective; `schedule` with the effective `startDate`/`targetDate` (PI dates filled in, flagged `startDateDefaulted`/`targetDateDefaulted`) and `elapsedShare`; `workItems` and the daily `workItemMetrics` (the CFD); `forecast`, or null with `forecastUnavailableReason` when forecasting is off — say so in the rationale; `healthChecks` (newest first); `sinceLastCheck` with the activity, `progressAtLastCheck` and `progressChange`; and `teamRisks`, the team's open risks. Read each part as the matching step below describes. A 404 means the objective is not in that PI.

Each step names what to look for in its part, and the tool that reads that part alone:

1. **Objective details and timeline:** `PlanningIntervals_GetObjective` with `idOrKey` + `objectiveIdOrKey`.
   - Name (the actual commitment), description, status, current progress percentage.
   - Start and target dates (`startDate`, `targetDate`). If either is unset, call `PlanningIntervals_GetPlanningInterval` and use the PI's `start` and `end` dates as defaults.
2. **Linked work items and progress summary:** `PlanningIntervals_GetObjectiveWorkItems`.
   - Inspect `progressSummary` (`proposed`, `active`, `done`, `total`) and individual item details.
3. **Cumulative Flow Diagram (CFD) metrics:** `PlanningIntervals_GetObjectiveWorkItemMetrics`.
   - Daily snapshots of `proposed`, `active`, and `done` counts.
   - Flow observation: Are items moving steadily from Proposed → Active → Done? Is WIP (`active`) ballooning or flatlining?
4. **Delivery forecast:** `PlanningIntervals_GetObjectiveForecast`.
   - Read the `outcome` first, as the `wayd-teams` skill describes; only `Forecast` carries dates.
   - Check `chanceOfFinishingByTargetDate` and the 85% confidence date against the objective's target date (or PI end).
   - Identify pacing blockers from `dependencies[].shareOfTrialsSettingFinish`.
   - **A 404 on an objective you just read means the `delivery-forecasting` flag is off.** Continue without a forecast, judge on progress and CFD alone, and say in the rationale that no forecast was available.
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

- **Recommended Status:** Healthy / AtRisk / Unhealthy (with statusId)
- **Proposed Expiration:** UTC datetime
- **Proposed Note:** Full drafted text for the note
- **Rationale & Analysis:**
  - **Progress since last check:** Key deltas in work items and progress percentage
  - **CFD & Flow observation:** Trend in Active vs Done items
  - **Forecast & Timeline:** Probability of meeting target date and 85% confidence date, or that no forecast was available
  - **Concerns / Blockers:** Specific dependencies, risks, or slip factors

Ask the user to confirm the recommendation or provide any adjustments (change status, revise expiration, or edit note) before calling `PlanningIntervals_CreateObjectiveHealthCheck`.

### Activity history

- A PI's changes (details, dates, teams, iterations, sprint mappings, objectives locked or unlocked): `PlanningIntervals_GetActivities` with `idOrKey`
- One objective's changes (details, status, progress, order, stretch, timeline, health checks): `PlanningIntervals_GetObjectiveActivities` with `idOrKey` + `objectiveIdOrKey`

Both return entries newest first, paged (50 per page by default, at most 500). Each entry has a `category`, a `summary`, who made the change and when, and a `payload` JSON string holding both the old and new value. A `Baseline` entry marks where tracking began for an objective or PI that already existed; nothing before it is recorded. People inside a payload are employee ids.

### Predictability

- All teams: `PlanningIntervals_GetPredictability` with `idOrKey`
- One team: `PlanningIntervals_GetTeamPredictability` with `idOrKey` + `teamId` (UUID)
- If `teamId` is unknown, call `PlanningIntervals_GetTeams` first to list participating teams.

### Risks

Read a PI's risks with `PlanningIntervals_GetRisks` (`idOrKey`). It returns **open risks only** unless you pass `includeClosed: true`; pass `teamId` to scope to one team. If the user seems to be missing risks, they are probably closed — rerun with `includeClosed: true`.

To create or update a risk, use the `Risks_*` tools; the `wayd-teams` skill covers them.

### Changing a PI

The tools that change a PI cover creating and updating it, setting its dates and iterations, its teams and each team's sprint mappings, and creating, updating, reordering and deleting objectives and their linked work items. Read [reference/changing-a-pi.md](reference/changing-a-pi.md) before calling any of them. The rules that catch agents out:

- **Most of them replace a whole list or record.** `PlanningIntervals_ManageDates` (iterations), `PlanningIntervals_ManageTeams` (teams), `PlanningIntervals_MapTeamSprints` (the team's sprint mappings) and `PlanningIntervals_ManageObjectiveWorkItems` (links) delete or unlink whatever the body leaves out. `PlanningIntervals_UpdatePlanningInterval`, `PlanningIntervals_UpdateObjective` and `PlanningIntervals_UpdateObjectiveHealthCheck` clear every field the body omits. Read the current state first and send back everything that should stay.
- **Locked objectives.** While the PI's `objectivesLocked` is true, objectives cannot be created or deleted, and an update keeps each objective's name and stretch flag whatever you send. Omitting `objectivesLocked` from `PlanningIntervals_UpdatePlanningInterval` unlocks them.
- **Writes take UUIDs**, and most bodies repeat the path's ids, which must match.

### PI health report recipe (compound task)

When the user asks for a comprehensive PI health summary:

1. Run in parallel:
   - `PlanningIntervals_GetPlanningInterval` — PI name, dates, and how far through the PI today is
   - `PlanningIntervals_GetObjectivesHealthReport` — latest health check per objective across teams
   - `PlanningIntervals_GetPredictability` — predictability per team
   - `PlanningIntervals_GetRisks` — open risks
2. Synthesize the report in this shape:
   - **Overall RAG** — one status for the PI with a one-sentence reason, led by the share of objectives AtRisk or Unhealthy.
   - **Objectives by status** — grouped Unhealthy, AtRisk, Healthy, then objectives with no active (non-expired) health check; each with its team and the check's note.
   - **Predictability outliers** — the PI-wide `predictability`, then the teams furthest below it, each with its `predictability` and completed versus regular objective counts.
   - **Top risks** — the few open risks with the highest `exposure`, each with its team, `assignee` and `followUpDate`.
3. Before presenting, check every objective in the health report appears in exactly one status group, and that the overall RAG agrees with the groups.
