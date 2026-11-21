---
name: wayd-kpis
description: Read before calling the Wayd MCP tools named Kpis_* or KpiCategories_*. Guides agents working with the scorecard of a Wayd team, team of teams, project, program, portfolio or strategic initiative via the Wayd MCP server — reading its KPIs with their baselines, targets, health and trend, reading one KPI's measurement history, recording and correcting measurements, managing a KPI's checkpoint plan, and adding, changing, closing, reordering and removing KPIs. Use when the user asks how a team, a project or a strategic initiative is doing against its KPIs, what a KPI's latest value or trend is, which KPIs are at risk or behind plan, wants to record this period's value for a KPI, wants to set or change a KPI's checkpoints, or wants to set up or change a team's or a project's KPIs.
---

# Wayd KPIs

The KPI tools are in the `kpis` toolset, and need the `kpis` feature flag. They work on any record that carries KPIs, named by two parameters:

| Record | `subjectType` | `subjectId` |
| --- | --- | --- |
| Team or team of teams | `organization.team` | The team's UUID (see the `wayd-teams` skill) |
| Project | `ppm.project` | The project's UUID (see the `wayd-ppm` skill) |
| Program | `ppm.program` | The program's UUID |
| Portfolio | `ppm.portfolio` | The portfolio's UUID |
| Strategic initiative | `ppm.strategic-initiative` | The initiative's UUID (see the `wayd-ppm` skill) |

A `subjectType` that cannot carry KPIs returns 400. A record that does not exist, or that the caller cannot read, returns 404 — never 403 — so treat 404 as "not visible to you" and say so rather than retrying.

## Before any change

Every tool here except the reads changes the record's scorecard and appears in its Activity. Show the user exactly what you will do — the KPI, the value and when it was taken, or the fields you will change — and wait for their go-ahead. Reads need no confirmation. When the user approves, do exactly what they approved.

Only members of the team, members of any team of teams above it, and KPI administrators may change a team's KPIs. A project's, program's or portfolio's KPIs are changed by whoever may change the record: its Update permission plus Owner or Manager on it or on its program or portfolio, or a KPI administrator; this holds after the record closes too. A strategic initiative's KPIs need only the Update Strategic Initiatives permission, or a KPI administrator, with no role on the initiative; they too stay open to change after it closes. `canManage` on the scorecard says whether the caller may; when it is false, tell the user rather than calling a change tool. Every change requires the caller's account to be linked to an employee.

## Reading a scorecard

`Kpis_GetScorecard` returns the record's name, `canManage`, and every active KPI, ordered by category and then position. For each KPI:

- **`goal`** — what it is measured against, as entered and as it resolves today: `direction` (`Increase` or `Decrease`), `baselineValue` and the window it was taken over, the target as `targetKind` (`Absolute` or `RelativeChange`, a percentage from the baseline) with `targetValue`, and an optional at-risk threshold. Use `resolvedTargetValue` and `resolvedAtRiskValue` when stating the target as a number.
- **`window`** — what span of time each value covers (see "Measurement windows" below). Say which when you quote a value: "cycle time since Feb 16", "over the last 90 days", "as of 3 pm".
- **`reading`** — what the latest value means: `value`, `changeFromBaseline` and `changeFromBaselinePercent`, `progressToTarget` (1 is the target reached, above 1 past it), `health` (`Healthy`, `AtRisk` or `Unhealthy`; absent before anything is measured) and `trend` (`Improving`, `Worsening`, `Stable` or `NoData`).
- **`rollingAverageOf`** — when set, the reading's `value` is the average of that many latest measurements, and health and trend judge the average; `latestValue` is the newest single measurement and `averagedCount` how many were averaged (fewer than asked while the KPI is new). Report it as "average of the last 7", not as the latest value.
- **`expressedAs`** — the reading the record leads with: `Value`, `ChangeFromBaseline` or `ProgressToTarget`. Lead with the same reading when summarising the KPI.
- **`reading.checkpoint`** — when set, health was judged against that checkpoint of the plan, not the target: say "against the Q2 plan of 24", not "against the target". Progress and change are still toward the target. See "Checkpoint plans".
- **`checkpoints`** — the KPI's checkpoint plan in date order; empty when it has none.
- **`recentValues`** — up to twelve recent values, oldest first.

Values are raw: a percentage KPI stores 18 for 18%. Add the KPI's `prefix` and `suffix` when quoting a value. A KPI with no baseline has no change or progress reading — say so rather than inventing one.

`Kpis_GetClosed` lists the KPIs taken off the scorecard, kept as history. A closed KPI takes no measurements until `Kpis_ReopenKpi`.

`Kpis_GetKpi` returns one KPI, open or closed, with every measurement, newest first: the value, the period it covers, `measuredAt` for a point-in-time reading, who took it, its sample size when known, and any note.

## Measurement windows

A KPI's `window.kind` decides how a measurement says when it was taken. Give exactly the one it takes; any other is refused with 400. Read the KPI first when you do not know its window.

| `window.kind` | Each value covers | Recording gives | One value per |
| --- | --- | --- | --- |
| `PerPeriod` | Its own period: a sprint, week, month | `periodStart` and `periodEnd` | Period; periods never overlap |
| `Cumulative` | `anchorDate` to the day it is taken | `asOf` | `asOf` day |
| `Rolling` | `rollingCount` `rollingUnit`s ending on that day | `asOf` | `asOf` day |
| `PointInTime` | The state at one moment: open bugs, WIP | `measuredAt`, an ISO instant ending in `Z` | Moment; several a day are fine |

Convert a local time to UTC for `measuredAt` (3 pm in Chicago in March is `21:00Z`). For a cumulative or rolling KPI never give a period, even one that matches: Wayd works it out from `asOf`.

## Recording and correcting

`Kpis_RecordMeasurement` records the raw value on an open KPI, taken by the caller.

- **The value is raw**, never a percentage worked out from it: record 14 for a cycle time of 14 days, even when the KPI is shown as change from baseline.
- **Check for a clash first.** Read the KPI's history: a per-period KPI's new period must start after the latest ends; a cumulative or rolling KPI takes one value per `asOf` day; a point-in-time KPI one per moment.
- Give `sampleSize` when the value is an average or a rate — how many items it was worked out from.

`Kpis_ChangeMeasurement` corrects a measurement: send the value and its time again, the way recording takes them. `Kpis_RemoveMeasurement` removes one for good.

## Checkpoint plans

A KPI may have a plan: dated checkpoints, each with a `label` ("Q1"), a `plannedValue` and an optional `atRiskValue`. A measurement is judged against the first checkpoint on or after the last day it covers (its period's end, its as-of day, or its moment's day) — its planned value, and its at-risk value or, without one, simply on plan or not. The KPI's own at-risk threshold is not used inside the plan. Past the last checkpoint, the target and the KPI's at-risk threshold apply as usual.

- **`Kpis_GetCheckpointPlan`** lists each checkpoint with the latest measurement under it and that measurement's health and trend against it. Use it to answer "are we on plan?" for past checkpoints.
- **`Kpis_ChangeCheckpointPlan` replaces the whole plan.** Read the current plan first and send every checkpoint back with only the user's change applied; a checkpoint left out is removed, and an empty list clears the plan. Checkpoints are matched by `date`, one per day.
- An `atRiskValue` must be short of its `plannedValue` in the KPI's direction: below it for `Increase`, above it for `Decrease`. Planned values may dip for a seasonal plan, and need not end at the target.
- A closed KPI's plan cannot change.
- Reversing a KPI's `direction` fails while its plan holds at-risk values. Tell the user, and with their agreement clear the plan's at-risk values, change the direction, then set them on the new side.

## Setting up and changing KPIs

- **`Kpis_AddKpi`** adds an active KPI, last on the scorecard. Give `window` for anything but `PerPeriod`; it cannot change once the KPI has measurements, so get it right first. `rollingAverageOf` (2 to 12) is only for a per-period KPI whose values add up per period, such as velocity read over the last 7 sprints. `categoryId` must be an active category from `KpiCategories_GetCategories`.
- **`Kpis_UpdateKpi` replaces everything entered** — name, description, category, prefix, suffix, `expressedAs`, baseline, target and `rollingAverageOf`. Read the KPI first and send every field back with only the user's change applied; a field left out is cleared. Leave `window` out to keep it.
- **A window change on a KPI with measurements is refused.** Tell the user, and offer to close the KPI and add a new one with the window they want.
- **`Kpis_CloseKpi`** takes a KPI off the scorecard and keeps its history; prefer it to **`Kpis_RemoveKpi`**, which deletes the KPI and every measurement for good. Name what removal loses before asking.
- **`Kpis_ReorderKpis`** takes every KPI on the record, open and closed, each once, in the new order.

Defining and retiring the organization's KPI categories is done in the app, under Settings.
