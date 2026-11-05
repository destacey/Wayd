# Changing a planning interval

Confirm each change with the user first, as SKILL.md describes. Every write takes UUIDs, never keys: resolve them with `PlanningIntervals_GetPlanningInterval`, `PlanningIntervals_GetObjectives` and the other reads first. Most bodies repeat the path's ids (`id`, `planningIntervalId`, `objectiveId`, `teamId`) — they must match, or the call is refused.

## The interval itself

| Goal | Tool | Notes |
| --- | --- | --- |
| Create a PI | `PlanningIntervals_CreatePlanningInterval` | `name`, `start`, `end`, `iterationWeeks`, optional `iterationPrefix` and `description`. Iterations are generated: each `iterationWeeks` long, named prefix + number, the last cut to `end` and made the IP iteration. Returns `id` and `key`. |
| Rename, describe, lock or unlock objectives | `PlanningIntervals_UpdatePlanningInterval` | **Whole-record overwrite** of `name`, `description`, `objectivesLocked` — an omitted `description` is cleared and an omitted `objectivesLocked` *unlocks* the objectives. Read the PI first and send all three. |
| Change dates or iterations | `PlanningIntervals_ManageDates` | Sends `start`, `end` and the **complete** `iterations` list. See below. |
| Change the teams | `PlanningIntervals_ManageTeams` | `teamIds` is the **complete** roster. See below. |
| Map a team's sprints | `PlanningIntervals_MapTeamSprints` | The team's **complete** set of mappings. See below. |

### Dates and iterations (`PlanningIntervals_ManageDates`)

The body is the whole calendar, not a change to it:

- An iteration with its `iterationId` is updated; one without is added; an existing iteration left out is **deleted, with its sprint mappings**.
- Read `PlanningIntervals_GetIterations` first and send every iteration that stays, with its id, even when only the PI's end date changes.
- Each iteration needs a unique `name`, a `categoryId` (`1` Development, `2` Innovation and Planning — `PlanningIntervals_GetIterationCategories`), and `start`/`end` inside the PI's dates, overlapping no other iteration.
- Any failed rule rejects the whole call; nothing changes.

### Teams (`PlanningIntervals_ManageTeams`)

- `teamIds` replaces the roster. Read `PlanningIntervals_GetTeams` and send everyone who stays plus whoever joins.
- A team left out loses its sprint mappings. Its objectives stay.
- A Scrum team that joins has its sprints mapped automatically (see SKILL.md); check `PlanningIntervals_GetIterationSprints` afterwards before mapping by hand.
- A team created moments ago may not have reached planning yet. The call then fails with a "may still be syncing" message; retry shortly.

### Sprint mappings (`PlanningIntervals_MapTeamSprints`)

- `iterationSprintMappings` maps iteration id → sprint id, or → `null` to leave that iteration without a sprint for the team.
- **Every sprint of the team that is mapped now but not named as a value is unmapped, even when its iteration is not in the body.** To change one iteration, read the team's mappings with `PlanningIntervals_GetIterationSprints` and send them all.
- A team has at most one sprint per iteration and a sprint sits in one iteration: mapping a sprint moves it.
- The team must be in the PI, and every sprint must be one of **that team's own** sprints, of type Sprint. A sprint of another team refuses the whole call and nothing changes.

## Objectives

| Goal | Tool | Notes |
| --- | --- | --- |
| Create | `PlanningIntervals_CreateObjective` | `teamId` (a team in the PI — any other team is refused; add it with `PlanningIntervals_ManageTeams` first), `name` (the commitment), optional `description`, `startDate`, `targetDate`, `isStretch`, `order`. Starts Not Started at 0%. |
| Update | `PlanningIntervals_UpdateObjective` | **Whole-record overwrite** — see below. |
| Reorder | `PlanningIntervals_UpdateObjectivesOrder` | `objectives` maps objective id → `order` (or `null`); unnamed objectives keep theirs. |
| Delete | `PlanningIntervals_DeleteObjective` | Refused while objectives are locked. |
| Set linked work items | `PlanningIntervals_ManageObjectiveWorkItems` | `workItemIds` (work item UUIDs) is the **complete** set; anything left out is unlinked, `[]` unlinks all. Read `PlanningIntervals_GetObjectiveWorkItems` first. |

### Updating an objective

`PlanningIntervals_UpdateObjective` sets `name`, `description`, `statusId`, `progress`, `startDate`, `targetDate` and `isStretch` from the body. Read the objective with `PlanningIntervals_GetObjective` and send every field back, changed or not — an omitted date or description is cleared, an omitted `isStretch` becomes false, an omitted `progress` becomes 0.

- `statusId`: `1` Not Started, `2` In Progress, `3` Completed, `4` Canceled, `5` Missed (`PlanningIntervals_GetObjectiveStatuses`). Completed, Canceled and Missed close the objective and stamp its closed date; moving back to Not Started or In Progress reopens it.
- `progress` is a percentage, 0–100.
- When both dates are given, `startDate` must be before `targetDate`.

### Locked objectives

`objectivesLocked` on the PI (from `PlanningIntervals_GetPlanningInterval`) freezes the committed plan:

- Creating or deleting an objective is refused.
- An update **keeps the objective's `name` and `isStretch`**, silently ignoring new values, while description, status, progress and dates still change. If the user asks to rename an objective or change its stretch flag while locked, tell them it needs the objectives unlocked (`PlanningIntervals_UpdatePlanningInterval` with `objectivesLocked: false`) — a change to the PI they should approve on its own.

## Health checks

Logging a check is the recipe in SKILL.md. Beyond that:

- `PlanningIntervals_UpdateObjectiveHealthCheck` — corrects a check that has not expired. **Whole-record overwrite** of `statusId` (`1` Healthy, `2` AtRisk, `3` Unhealthy), `expiration` (future UTC datetime) and `note` (omitted = cleared). The body repeats the objective's id as `planningIntervalObjectiveId` and the check's id as `id`. An expired check cannot be changed; log a new one instead. Returns the updated check; 404 when the check is not on that objective or the objective is not in the PI.
- `PlanningIntervals_DeleteObjectiveHealthCheck` — removes a check from the history, expired or not. Deleting the active check leaves the objective with none; nothing earlier becomes active again. 404 when the check is not on that objective or the objective is not in the PI.
