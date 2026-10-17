---
"@wayd/mcp": minor
---

Adds `Teams_GetCadenceMetrics`: a Kanban team's velocity over its reporting cadence, with what it completed in each recent period and the average and spread across ended, whole periods. It calls the API's new `{id}/cadence-metrics` route, so this version needs an API that has it.
