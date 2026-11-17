---
name: wayd-kpis
description: Read before calling the Wayd MCP tools named Kpis_*. Guides agents reading a Wayd team's or team of teams' scorecard via the Wayd MCP server — its KPIs with their baselines, targets, health and trend — reading one KPI's measurement history, and recording a new measurement. Use when the user asks how a team is doing against its KPIs, what a KPI's latest value or trend is, which KPIs are at risk, or wants to record this period's value for a KPI.
---

# Wayd KPIs

The KPI tools are in the `kpis` toolset, and need the `kpis` feature flag. They work on any record that carries KPIs, named by two parameters:

| Record | `subjectType` | `subjectId` |
| --- | --- | --- |
| Team or team of teams | `organization.team` | The team's UUID (see the `wayd-teams` skill) |

A `subjectType` that cannot carry KPIs returns 400. A record that does not exist, or that the caller cannot read, returns 404 — never 403 — so treat 404 as "not visible to you" and say so rather than retrying.

## Before any change

Recording a measurement is a change: it appears on the team's scorecard and in its Activity. Show the user the KPI, the period and the value you will record, and wait for their go-ahead. Reads need no confirmation. When the user approves, record exactly what they approved.

## Reading a scorecard

`Kpis_GetScorecard` returns the record's name, whether the caller may manage its KPIs (`canManage`), and every active KPI, ordered by category and then position. For each KPI:

- **`goal`** — what it is measured against, as entered and as it resolves today: `direction` (`Increase` or `Decrease`), `baselineValue` and the window it was taken over, the target as `targetKind` (`Absolute` or `RelativeChange`, a percentage from the baseline) with `targetValue`, and an optional at-risk threshold. Use `resolvedTargetValue` and `resolvedAtRiskValue` when stating the target as a number.
- **`reading`** — what the latest value means: `value`, `changeFromBaseline` and `changeFromBaselinePercent`, `progressToTarget` (1 is the target reached, above 1 past it), `health` (`Healthy`, `AtRisk` or `Unhealthy`; absent before anything is measured) and `trend` against the previous value (`Improving`, `Worsening`, `Stable` or `NoData`).
- **`expressedAs`** — the reading the team leads with: `Value`, `ChangeFromBaseline` or `ProgressToTarget`. Lead with the same reading when summarising the KPI.
- **`recentValues`** — up to twelve recent values, oldest first, each with the period it describes.

Values are raw: a percentage KPI stores 18 for 18%. Add the KPI's `prefix` and `suffix` when quoting a value. A KPI with no baseline has no change or progress reading — say so rather than inventing one.

`Kpis_GetClosed` lists the KPIs taken off the scorecard, kept as history. A closed KPI takes no measurements.

## A KPI's history

`Kpis_GetKpi` returns one KPI, open or closed, with every measurement, newest period first: the value, the period (`periodStart` to `periodEnd`), who took it, the sample size it was worked out from when known, and any note.

## Recording a measurement

`Kpis_RecordMeasurement` records the raw value for a period on an open KPI, taken by the caller.

- **The value is raw**, never a percentage worked out from it: record 14 for a cycle time of 14 days, even when the KPI is shown as change from baseline.
- **Periods on one KPI never overlap.** Read the KPI's history first and choose a period that starts after the latest one ends; a period that overlaps returns 400. To change a value already recorded, the user corrects it in the app.
- Give `sampleSize` when the value is an average or a rate — how many items it was worked out from.
- Only members of the team, members of any team of teams above it, and KPI administrators may record. `canManage` on the scorecard says whether the caller may; when it is false, tell the user rather than calling the tool.
- Requires the caller's account to be linked to an employee.

Adding, editing, closing and removing KPIs, and managing their categories, are done in the app.
