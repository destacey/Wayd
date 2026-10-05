---
"@wayd/mcp": patch
---

Correct the deployment outcome tool descriptions: they said no outcome tool can be called once an outcome is recorded, but `Deployments_RollBack` is offered only for a deployment that succeeded. `Deployments_Succeed` and `Deployments_Fail` now say a success can still be rolled back, and `Deployments_RollBack` says it refuses a deployment still in flight, failed or already rolled back.
