---
'@wayd/mcp': minor
---

Record when versions and release packages were cut and shipped as instants, matching the API.

- `Versions_Cut` takes `cutAt`, and `Versions_MarkReleased` and `ReleasePackages_MarkReleased` take `releasedAt`, each an ISO 8601 instant with its offset (e.g. `2026-09-18T02:30:00Z`) in place of the `cutDate`/`releasedDate` calendar dates. `Versions_CorrectDates` takes `cutAt` and `releasedAt` the same way; the target date stays a calendar date. Until now these tools dropped `cutAt`/`releasedAt` as unknown properties, so a correction reached the API with no released moment and was refused as an attempt to remove it.
- The `product-management.versions` and `product-management.release-packages` import formats take `CutAt` and `ReleasedAt` columns, ISO 8601 timestamps with an offset, in place of `CutDate` and `ReleasedDate`.
- `ReleasePackages_CorrectDates` fixes a package's target date or released moment without moving its status.
- `DeliveryOverview_GetDeliveryOverview` accepts `timeZone`, the zone `from` and `to` are read as days in, and reports cut-to-release latency as fractional days.
- `Deployments_Succeed` says that a production deployment marks what it shipped as released.
- `Teams_GetActivities` describes the membership and operating-model entries it now returns.
