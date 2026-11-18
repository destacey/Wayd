---
name: wayd-kpis
description: Read before calling the Wayd MCP tools named Kpis_* or KpiCategories_*. Guides agents working with a Wayd team's or team of teams' scorecard via the Wayd MCP server — reading its KPIs with their baselines, targets, health and trend, reading one KPI's measurement history, recording and correcting measurements, and adding, changing, closing, reordering and removing KPIs. Use when the user asks how a team is doing against its KPIs, what a KPI's latest value or trend is, which KPIs are at risk, wants to record this period's value for a KPI, or wants to set up or change a team's KPIs.
---

# Wayd KPIs

The KPI tools are in the `kpis` toolset, and need the `kpis` feature flag. They work on any record that carries KPIs, named by two parameters:

| Record | `subjectType` | `subjectId` |
| --- | --- | --- |
| Team or team of teams | `organization.team` | The team's UUID (see the `wayd-teams` skill) |

A `subjectType` that cannot carry KPIs returns 400. A record that does not exist, or that the caller cannot read, returns 404 — never 403 — so treat 404 as "not visible to you" and say so rather than retrying.

## Before any change

Every tool here except the reads changes the team's scorecard and appears in its Activity. Show the user exactly what you will do — the KPI, the value and when it was taken, or the fields you will change — and wait for their go-ahead. Reads need no confirmation. When the user approves, do exactly what they approved.

Only members of the team, members of any team of teams above it, and KPI administrators may change a team's KPIs. `canManage` on the scorecard says whether the caller may; when it is false, tell the user rather than calling a change tool. Every change requires the caller's account to be linked to an employee.

## Reading a scorecard

`Kpis_GetScorecard` returns the record's name, `canManage`, and every active KPI, ordered by category and then position. For each KPI:

- **`goal`** — what it is measured against, as entered and as it resolves today: `direction` (`Increase` or `Decrease`), `baselineValue` and the window it was taken over, the target as `targetKind` (`Absolute` or `RelativeChange`, a percentage from the baseline) with `targetValue`, and an optional at-risk threshold. Use `resolvedTargetValue` and `resolvedAtRiskValue` when stating the target as a number.
- **`window`** — what span of time each value covers (see "Measurement windows" below). Say which when you quote a value: "cycle time since Feb 16", "over the last 90 days", "as of 3 pm".
- **`reading`** — what the latest value means: `value`, `changeFromBaseline` and `changeFromBaselinePercent`, `progressToTarget` (1 is the target reached, above 1 past it), `health` (`Healthy`, `AtRisk` or `Unhealthy`; absent before anything is measured) and `trend` (`Improving`, `Worsening`, `Stable` or `NoData`).
- **`rollingAverageOf`** — when set, the reading's `value` is the average of that many latest measurements, and health and trend judge the average; `latestValue` is the newest single measurement and `averagedCount` how many were averaged (fewer than asked while the KPI is new). Report it as "average of the last 7", not as the latest value.
- **`expressedAs`** — the reading the team leads with: `Value`, `ChangeFromBaseline` or `ProgressToTarget`. Lead with the same reading when summarising the KPI.
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

## Setting up and changing KPIs

- **`Kpis_AddKpi`** adds an active KPI, last on the scorecard. Give `window` for anything but `PerPeriod`; it cannot change once the KPI has measurements, so get it right first. `rollingAverageOf` (2 to 12) is only for a per-period KPI whose values add up per period, such as velocity read over the last 7 sprints. `categoryId` must be an active category from `KpiCategories_GetCategories`.
- **`Kpis_UpdateKpi` replaces everything entered** — name, description, category, prefix, suffix, `expressedAs`, baseline, target and `rollingAverageOf`. Read the KPI first and send every field back with only the user's change applied; a field left out is cleared. Leave `window` out to keep it.
- **A window change on a KPI with measurements is refused.** Tell the user, and offer to close the KPI and add a new one with the window they want.
- **`Kpis_CloseKpi`** takes a KPI off the scorecard and keeps its history; prefer it to **`Kpis_RemoveKpi`**, which deletes the KPI and every measurement for good. Name what removal loses before asking.
- **`Kpis_ReorderKpis`** takes every KPI on the record, open and closed, each once, in the new order.

Defining and retiring the organization's KPI categories is done in the app, under Settings.
