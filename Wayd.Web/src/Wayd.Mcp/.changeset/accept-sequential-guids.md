---
'@wayd/mcp': patch
---

Accept every Wayd id in `uuid` parameters. Argument validation enforced the RFC 9562 variant, so ids generated as sequential GUIDs (such as `d54bede6-4b6c-4edc-1e77-08de4e00456e`) were rejected as "Invalid UUID" before reaching the API, blocking tools like `Projects_GetProjectHealthChecks`, `Projects_CreateProjectHealthCheck` and `Projects_GetWorkItems` for those records.
