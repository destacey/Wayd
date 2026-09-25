---
'@wayd/mcp': minor
---

Add team and team of teams allocation reports.

- `Teams_GetTeamAllocation` reports where a team's completed Requirement-tier work went over a date range (max 366 days), grouped by portfolio, program, project, strategic theme, or work type.
- `TeamsOfTeams_GetList` lists teams of teams in the organization.
- `TeamsOfTeams_GetAllocation` reports completed work rolled up across a team of teams hierarchy, supporting the `TeamEffort` measure to combine teams without mixing differing estimation scales.
