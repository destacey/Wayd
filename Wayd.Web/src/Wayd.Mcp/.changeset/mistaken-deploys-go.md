---
"@wayd/mcp": minor
---

Add `Deployments_Delete`, which permanently deletes a deployment and its status history. It needs the new delivery Delete permission. Delivery measures stop counting a deleted deployment, so a real failure or rollback should still be recorded as one.
