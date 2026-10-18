---
"@wayd/mcp": patch
---

`Projects_CreateProjectHealthCheck` and `PlanningIntervals_CreateObjectiveHealthCheck` now carry the recipe for recommending a health check in their descriptions: the tools to gather evidence with first (including `PlanningIntervals_GetPlanningInterval` for the PI's dates, and that a 404 from `Projects_GetForecast` means forecasting is off), how to choose Healthy, At Risk or Unhealthy (a project forecast outcome with no dates is left out rather than counted against it), what the note should cite, and that the user confirms before the check is logged. The server instructions now name these two workflows and point to those descriptions, and to the matching health-check evidence tool where a server lists one, so an agent without the Wayd skills installed still finds them.
