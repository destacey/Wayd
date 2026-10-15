---
"@wayd/mcp": patch
---

Fields that are only required in some requests are no longer marked required everywhere:

- Import formats: deployment imports' `CompletedAt` and `RolledBackAt`, and release imports' `PackageId` and `VersionId`, are listed as optional. Each is still required in the rows the bulk import guide says need it, and the import still refuses a row that lacks it.
- Tool inputs: creating a project task no longer demands `progress` and `plannedDate` (a task takes progress, a milestone a planned date), and a KPI checkpoint plan item no longer demands `checkpointId`, which is left out for a new checkpoint.
