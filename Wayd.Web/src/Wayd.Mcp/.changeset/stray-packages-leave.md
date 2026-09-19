---
"@wayd/mcp": minor
---

Add `ReleasePackages_Delete`, which permanently deletes a package with its manifest, status history and every deployment of it. It is refused while any release lists the package. It needs the delivery Delete permission. Withdrawing is still the way to pull a package while keeping its history.
