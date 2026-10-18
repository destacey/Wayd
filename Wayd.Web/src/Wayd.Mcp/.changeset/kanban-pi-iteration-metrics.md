---
"@wayd/mcp": patch
---

`PlanningIntervals_GetIterationMetrics` now describes the iteration's Kanban teams: each appears in `dateMeasuredTeamMetrics`, measured by the iteration's dates, with what it completed within them and what it had in progress at their end, and no scope figures. Their completed and in-progress work is in the iteration's totals but not in `totalWorkItems`. A new `sprintSizingMethod` gives the unit of the sprint-only `totalEstimate` and `notStartedEstimate`. The `wayd-pi` skill says how to read them.
