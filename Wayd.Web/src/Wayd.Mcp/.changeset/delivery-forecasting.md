---
'@wayd/mcp': minor
---

Forecast delivery and grade backlog health.

- `Workspaces_GetWorkItemForecast`, `PlanningIntervals_GetObjectiveForecast` and `Projects_GetForecast` forecast when a work item, PI objective or project will be done, with the chance of finishing by a target date. Each accepts optional `targetDate`, `lookbackDays` and `ignoreDependencies`.
- `Teams_GetThroughputForecast` forecasts how many backlog work items a team finishes by a `targetDate`, and which work item that reaches.
- `Teams_GetBacklogHealth` grades a team's open backlog and lists the work items each check flagged, with optional what-if thresholds.

The forecasts require the `delivery-forecasting` feature flag and return 404 when it is off.
