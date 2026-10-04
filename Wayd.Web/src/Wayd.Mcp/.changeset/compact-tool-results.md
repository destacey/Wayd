---
"@wayd/mcp": patch
---

Return tool results as compact JSON, cutting the tokens every response costs the model, and stop logging a line to stderr each time the API key is applied. Updates `@modelcontextprotocol/sdk` to 1.32, `axios` to 1.20 and `zod` to 4.6, and the SDK's transitive dependencies, clearing every advisory `npm audit` reported against the installed package.
