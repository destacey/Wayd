---
"@wayd/mcp": minor
---

`Products_ChangeStatus` takes an optional `reason`, recorded on the product's status history. A product workflow can now require a reason for a move: `Products_GetStatusOptions` lists each allowed move's `conditions`, and a move whose conditions include `RequiresReason` is refused without one.
