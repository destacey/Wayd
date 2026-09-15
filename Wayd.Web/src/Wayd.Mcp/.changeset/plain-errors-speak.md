---
"@wayd/mcp": patch
---

Show the reason in an API error. Error bodies were cut at 200 characters of raw JSON, and the API puts a boilerplate title first, so a validation failure arrived as `See the erro...` and a refusal from a domain rule lost its message. A problem details response is now reported as its title, its detail and each validation error by field.
