---
"@wayd/mcp": minor
---

Ask about another person's projects, not only the caller's.

- `Projects_GetTaskMetrics` returns the overdue, due-this-week and upcoming open-task counts across the projects an employee is involved in. Pass `employeeId` for a colleague or leave it out for the caller; `Projects_GetMyProjectsTaskMetrics` stays as the caller-only shortcut.
- `Projects_GetProjects` accepts `employeeId` beside `role`, so a role filter can describe someone else's projects.
- `Projects_GetProjectsPlanSummaries` accepts `employeeId`, and `allTasks` to count every task on the projects rather than the ones one person can see, for a portfolio or program view.
