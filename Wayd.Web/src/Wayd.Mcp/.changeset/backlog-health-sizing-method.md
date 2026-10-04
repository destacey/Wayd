---
"@wayd/mcp": patch
---

`Teams_GetBacklogHealth` describes `oversizedPercentile` as a percentile of estimates in the team's sizing method rather than of story points, matching the API, which now reads backlog health in the team's Story Points, Effort or Size and reports it as `totalEstimate`, `oversizedEstimate` and `sizingMethod`.
