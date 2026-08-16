---
'@wayd/mcp': minor
---

Add four PPM read tools and fix array query parameter serialization.

New tools:

- `Projects_GetMyProjectsSummary` — the caller's project involvement as counts per role, answering "what am I on?" without listing and filtering every project.
- `Projects_GetMyProjectsTaskMetrics` — aggregated overdue / due-this-week / upcoming open-task counts across the caller's projects.
- `Projects_GetStatusHistory` — a project's status change history, including who made each change, when, and why. Takes a project UUID; unlike most project endpoints it does not accept a key.
- `Projects_GetProjectsPlanSummaries` — plan summary metrics for many projects in one request. The per-project version was already exposed, so surveying twenty projects previously took twenty calls.

Fixes array query parameters, which were silently ignored server-side. Axios serialises arrays as `status[]=1&status[]=2`, but ASP.NET's model binder reads repeated bare keys (`status=1&status=2`) and binds the bracketed form to nothing. Every array-valued filter — `status` and `role` on the project, program, and portfolio list tools — was therefore dropped, returning unfiltered results rather than an error. The executor now serialises without bracket indexes, matching the generated API client, and a test pins the wire format.
