---
'@wayd/mcp': minor
---

Record how a product reaches one it depends on, and replace `Products_ChangeDependencyStrength` with `Products_ChangeDependencyTerms`.

- `Products_GetDependencies` reports each dependency's `interactionStyles` (`Synchronous`, `Asynchronous`, or both).
- `Products_AddDependency` accepts optional `interactionStyles`.
- `Products_UpdateDependency` accepts `interactionStyles` to record them in place on a dependency where none were recorded.
- `Products_ChangeDependencyTerms` replaces `Products_ChangeDependencyStrength` on `/api/product-management/products/{id}/dependencies/{dependencyId}/terms`, allowing changes to strength, interaction styles, or both.
