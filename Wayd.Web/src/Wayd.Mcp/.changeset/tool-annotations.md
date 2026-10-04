---
"@wayd/mcp": patch
---

Every tool now advertises a full set of annotations. Each has a human-readable `title`, and all are marked `openWorldHint: false`, since they reach only the Wayd API. Defaults come from the HTTP method: GET tools are read-only and idempotent, PUT and DELETE tools are destructive and idempotent, and POST tools are destructive. A tool's own annotations still override these.

`destructiveHint` now follows a single rule: a write is destructive unless it only adds a record. Creating a record, adding a child (a task, goal, step, dependency, persona, tag, swim lane or checklist item), logging a health check and recording a KPI measurement are no longer marked destructive. These include creating products, product types, tag categories and deployment environments, planning releases and versions, assembling packages and starting deployments. Every update, delete, status change and reversal is still destructive, and so is applying a product tag, which can replace one already on the product. The write tools that previously had no annotations, including the story map edits, are now marked destructive, so clients confirm before running them.
