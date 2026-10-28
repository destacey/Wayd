---
name: wayd-incidents
description: Read before calling the Wayd MCP tools named IncidentSeveritySets_*. Guides agents configuring incident severity scales under Product Management via the Wayd MCP server — listing seeded and custom scales, selecting which scale the organization uses, and creating, updating, activating, deactivating or deleting custom sets. Use when an admin needs to keep SEV-1–4, switch to Critical / High / Medium / Low, or define another ordered scale. Incident records themselves are not in this skill yet.
---

# Wayd incident severity sets

Tools prefixed **`IncidentSeveritySets_`**. Requires the `product-management` and `incident-management` feature flags.

## Before any change

Confirm every change with the user before you make it. Reads need no confirmation.

## Typical flow

1. `IncidentSeveritySets_GetSets` — see seeded scales, custom scales, levels (most severe first), and which set is `isSelected`.
2. `IncidentSeveritySets_SelectSet` — point new incidents at a different active scale.
3. To add a scale: `IncidentSeveritySets_CreateSet` with at least two `levelNames`, most severe first.

System sets (`isSystem: true`) cannot be updated or deleted. The selected set cannot be deactivated or deleted. Removing a level from a custom set fails while an incident still uses that level id.
