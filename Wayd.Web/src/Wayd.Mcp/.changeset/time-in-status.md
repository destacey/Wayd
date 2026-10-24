---
"@wayd/mcp": minor
---

Time in status for products, versions, releases, release packages and deployments. New tools `Products_GetTimeInStatus`, `Versions_GetTimeInStatus`, `Releases_GetTimeInStatus`, `ReleasePackages_GetTimeInStatus` and `Deployments_GetTimeInStatus` return, for each status of the current workflow, the number of stays that ended in a window and their average and median length; stays that began or ended with a workflow reassignment are left out. Each `*_GetStatusHistory` entry now carries `durationSeconds` and `leftOn`, plus `statusVisitCount` and `statusTotalSeconds` for every stay in that status, and each record read carries `statusEnteredOn`.
