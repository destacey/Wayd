---
"@wayd/mcp": patch
---

Import formats no longer mark a column required when it is only required in some rows. Deployment imports' `CompletedAt` and `RolledBackAt`, and release imports' `PackageId` and `VersionId`, are now listed as optional; each is still required in the rows the bulk import guide says need it, and the import still refuses a row that lacks it.
