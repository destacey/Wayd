---
name: wayd-roadmaps
description: Guides agents reading Wayd Roadmaps — listing roadmaps and exploring their activities, timeboxes, milestones, and item details. Read-only, since the Wayd MCP server has no tools that create or edit roadmaps. Use when finding a roadmap, summarizing what a roadmap plans for a period, or looking up a roadmap item.
---

# Wayd Roadmaps

## When to use

- Finding or listing roadmaps
- Exploring roadmap items (activities, timeboxes, milestones)
- Getting details for a specific roadmap item
- Understanding what's planned or visualized in a roadmap

Every roadmap tool is read-only. To create or change a roadmap, direct the user to the Wayd app.

---

## Entity context

### Roadmap item types

Roadmaps contain three distinct item types:

| Type | Description | Dates |
|---|---|---|
| **Activity** | Work or effort spanning a date range; may contain child items | `start`, `end` |
| **Timebox** | A time-bounded container (e.g. a quarter, a PI) | `start`, `end` |
| **Milestone** | A single point-in-time event | `date` |

`Roadmaps_GetItems` returns all three types. `Roadmaps_GetActivities` returns only activities.

**Both return a tree, not a flat list.** Only top-level items are at the root; anything nested under an activity is in that activity's `children`. Walk `children` recursively or you will miss most of the roadmap.

### Identifiers

- `Roadmaps_GetRoadmap`, `Roadmaps_GetItems` and `Roadmaps_GetActivities` take `idOrKey` — the roadmap's UUID or its integer key.
- `Roadmaps_GetItem` takes `roadmapIdOrKey` (UUID or key) plus `itemId`, which is UUID-only. If you only have the item's name, find its `id` in `Roadmaps_GetItems` first.

### Visibility

A private roadmap is visible only to its roadmap managers. To anyone else it is absent from `Roadmaps_GetRoadmaps` and its items come back empty, so an empty or missing roadmap may be private rather than nonexistent — say so.

---

## Instructions

### Navigating a roadmap

1. List all roadmaps: `Roadmaps_GetRoadmaps`
2. Get roadmap details: `Roadmaps_GetRoadmap` with `idOrKey`
3. Get all items: `Roadmaps_GetItems` with `idOrKey`
4. Get one item's details (adds `description`): `Roadmaps_GetItem` with `roadmapIdOrKey` + `itemId`

### Recipe: what is planned for a period

For a question like "what's planned next quarter on the Platform roadmap?":

1. `Roadmaps_GetRoadmaps` — find the roadmap by name; confirm its `start`/`end` cover the period.
2. `Roadmaps_GetItems` with its `id` or `key`.
3. Flatten the tree through `children`, then keep items that overlap the period: activities and timeboxes where `start` ≤ period end and `end` ≥ period start; milestones whose `date` falls inside it.
4. Present milestones in date order, then activities grouped under their top-level parent activity, sorted by `start`. Name any timebox the period falls in.
