---
"@wayd/mcp": minor
---

Add `Versions_Delete`, which permanently deletes a version with its status history and every deployment of it. It is refused while a release lists the version or a package manifest names it, and needs the delivery Delete permission.
