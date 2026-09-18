---
"@wayd/mcp": minor
---

Add product dependency, rollout and delivery overview tools, answering "what relies on this?", "what is running where?" and "how often are we shipping?".

**Product dependencies** — `Products_GetDependencies`, `Products_AddDependency`, `Products_UpdateDependency`, `Products_ChangeDependencyStrength`, `Products_EndDependency` and `Products_RemoveDependency`. Reading is rolled up across everything beneath the product, so a product line's list includes its services' links to anything outside it, and a link inside the line appears in neither list. Strength (Hard or Soft) has no default. A dependency that stopped should be ended, which keeps it; remove is for one that was never true. Changing strength ends the dependency and opens a new one, and returns the new id.

**What is running where** — `DeploymentEnvironments_GetRollout` lists each environment in ring order with the latest deployment of each product that succeeded and was not rolled back, packages expanded into their components, and `hasFailedAttemptSince` where a later attempt failed.

**Version activity** — `DeliveryOverview_GetDeliveryOverview` measures release frequency and cut-to-released time from versions rather than deployments, for a product and everything beneath it; `DeliveryOverview_GetRecentDeliveryEvents` is a feed of the latest status change of each version and package.

Also:

- The environment category accepts **5 Other**, for a sandbox or demo target that is never counted as production.
- Activity entries can be **related**: `isRelated` and `raisedOn` mark an entry raised on another record, such as a child product moving in or out of this one.
- Import definitions and runs can have **PerGroup** atomicity, where rows sharing a group apply together and other groups are kept. The product dependency import applies product by product.
- `Products_Reparent` and `Products_Delete` describe the refusals dependencies add.

The `wayd-products`, `wayd-delivery` and `wayd-imports` skills cover the new tools. The rollout, delivery overview and related-activity tools need Wayd 0.211.0 or later; the dependency tools and the PerGroup import need 0.212.0.
