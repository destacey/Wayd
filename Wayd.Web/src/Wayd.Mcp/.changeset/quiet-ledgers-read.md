---
"@wayd/mcp": minor
---

Add activity history tools, answering "what changed on this, and who changed it?" for the records the server already reaches.

`Portfolios_GetActivities`, `Programs_GetActivities`, `Projects_GetActivities`, `StrategicInitiatives_GetActivities`, `PlanningIntervals_GetActivities`, `PlanningIntervals_GetObjectiveActivities`, `Teams_GetActivities`, `Products_GetActivities`, `Releases_GetActivities`, `Versions_GetActivities`, `ReleasePackages_GetActivities` and `Deployments_GetActivities`. All are read-only, take an ID or key, and return entries newest first, at most 100 per page.

Each entry has a category (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), who acted and when, a one-line summary, and a `payload` holding the event's fields as a JSON string. Three things worth knowing when reading one:

- **A change carries both ends.** The payload holds the value before and after, so one entry answers what moved.
- **People in a payload are employee ids**, not the user ids `Users_GetUsers` returns.
- **A Baseline entry is where tracking began** for a record that existed before its changes were recorded. Nothing earlier is available, so it is not the record's creation.

Each record's history is its own. A project's includes its health checks and scores but not its tasks or stages, and a program's does not include its projects.

The `wayd-ppm`, `wayd-pi`, `wayd-teams`, `wayd-products` and `wayd-delivery` skills explain how to read them. Every tool needs Wayd 0.210.0 or later. Most endpoints arrived in 0.207.0, but the planning interval and objective histories did not, and an older instance answers those with 404.
