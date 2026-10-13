---
"@wayd/mcp": minor
---

**Breaking:** the allocation report tools are renamed, because allocation now means the share of a person's time a team has. Call `Teams_GetTeamWorkDistribution` in place of `Teams_GetTeamAllocation`, and `TeamsOfTeams_GetWorkDistribution` in place of `TeamsOfTeams_GetAllocation`. Their arguments and results are unchanged. They call the API's new `{idOrCode}/work-distribution` routes, so this version needs an API that has them. The API no longer serves the old `{idOrCode}/allocation` routes, so on 0.17.0 and earlier these two tools return 404 against an upgraded API until the package is updated.
