---
"@wayd/mcp": minor
---

`Products_GetStatusOptions` takes an optional `productId` and then returns only the moves that product's workflow allows from its current status. A product workflow can now restrict which status may move to which, and `Products_ChangeStatus` refuses a move outside them, naming both statuses.
