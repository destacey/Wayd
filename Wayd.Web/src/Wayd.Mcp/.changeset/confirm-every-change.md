---
"@wayd/mcp": patch
---

The server instructions now ask agents to confirm every change, creates and tags included, rather than only destructive calls, and to do only what the user approved. They also say that every list on a write replaces its whole set, not only role lists; that role, assignee and `employeeId` parameters take employee ids rather than user ids; and that a moment is sent with its offset, never converted to a local date. `StoryMaps_AddGoal` no longer claims a new goal starts with a step: it starts empty.
