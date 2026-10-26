---
"@wayd/mcp": patch
---

The activity history tools now accept a `pageSize` of up to 500, the limit every paged list in the API shares; it still defaults to 50. A page size outside 1 to 500 is refused with a validation error, where the API used to cap it quietly at 100.
