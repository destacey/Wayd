---
name: wayd-roadmaps
description: Read before calling the Wayd MCP tools named Roadmaps_*. Guides agents working with Wayd Roadmaps — listing roadmaps and exploring their activities, timeboxes, milestones and item details, and creating, copying, updating, archiving and deleting roadmaps and their items. Use when finding or listing a roadmap, exploring its activities, timeboxes and milestones, summarizing what it plans for a period, looking up the details of a roadmap item, creating or copying a roadmap, changing its details, managers, visibility or color legend, archiving, reactivating or deleting it, or adding, changing, moving, re-dating or deleting its items.
---

# Wayd Roadmaps

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

Every tool that changes a roadmap is offered by the hosted endpoint (`/mcp`) only; elsewhere the roadmap tools are read-only, so direct the user to the Wayd app for changes.

## Entity context

### Roadmap item types

Roadmaps contain three distinct item types:

| Type | Description | Dates |
|---|---|---|
| **Activity** | Work or effort spanning a date range; may contain child items | `start`, `end` |
| **Timebox** | A time-bounded container (e.g. a quarter, a PI) | `start`, `end` |
| **Milestone** | A single point-in-time event | `date` |

`Roadmaps_GetItems` returns all three types. `Roadmaps_GetActivities` returns only activities (roadmap items, not activity history).

**Both return a tree, not a flat list.** Only top-level items are at the root; anything nested under an activity is in that activity's `children`. Walk `children` recursively or you will miss most of the roadmap. Only an activity can be a parent.

### Identifiers

- `Roadmaps_GetRoadmap`, `Roadmaps_GetItems` and `Roadmaps_GetActivities` take `idOrKey` — the roadmap's UUID or its integer key.
- `Roadmaps_GetItem` takes `roadmapIdOrKey` (UUID or key) plus `itemId`, which is UUID-only. If you only have the item's name, find its `id` in `Roadmaps_GetItems` first.
- **Every tool that changes something takes UUIDs only** — the roadmap's `id` and the item's `id`. Most bodies repeat the path's ids (`id`, `roadmapId`, `itemId`); they must match.

### Visibility, managers and state

- `visibilityId` is `1` Public (everyone sees it) or `2` Private (only its managers do); `Roadmaps_GetVisibilityOptions` lists them.
- A private roadmap is visible only to its roadmap managers. To anyone else it is absent from `Roadmaps_GetRoadmaps` and its items come back empty, so an empty or missing roadmap may be private rather than nonexistent — say so.
- **Only a roadmap manager can change a roadmap or its items.** `roadmapManagerIds` are employee UUIDs, and on create, copy and update they must include the user's own employee — so a user cannot remove themselves as a manager.
- A roadmap is `Active` or `Archived` (`Roadmaps_GetStateOptions`). An archived roadmap is read-only: reactivate it before any change, deleting included.

---

## Instructions

### Navigating a roadmap

1. List all roadmaps: `Roadmaps_GetRoadmaps`
2. Get roadmap details: `Roadmaps_GetRoadmap` with `idOrKey`
3. Get all items: `Roadmaps_GetItems` with `idOrKey`
4. Get one item's details (adds `description`): `Roadmaps_GetItem` with `roadmapIdOrKey` + `itemId`

### Recipe: what is planned for a period

For a question like "what's planned next quarter on the Platform roadmap?":

1. `Roadmaps_GetRoadmaps` with `state: ["Active"]` — find the roadmap by name; confirm its `start`/`end` cover the period. Without the filter, archived roadmaps come back too.
2. `Roadmaps_GetItems` with its `id` or `key`.
3. Flatten the tree through `children`, then keep items that overlap the period: activities and timeboxes where `start` ≤ period end and `end` ≥ period start; milestones whose `date` falls inside it.
4. Present milestones in date order, then activities grouped under their top-level parent activity, sorted by `start`. Name any timebox the period falls in.

### Managing a roadmap

| Goal | Tool | Notes |
|---|---|---|
| Create | `Roadmaps_CreateRoadmap` | `name`, `start`, `end`, `roadmapManagerIds`, `visibilityId`, optional `description`. Starts Active and empty. Returns `id` and `key`. |
| Copy | `Roadmaps_CopyRoadmap` | `sourceRoadmapId`, new `name`, `roadmapManagerIds`, `visibilityId`. Copies the description, dates and every item with its nesting and color; not the color legend. |
| Change details, managers or visibility | `Roadmaps_UpdateRoadmap` | **Whole-record overwrite** — see below. |
| Set the color legend | `Roadmaps_UpdateColors` | `colors` is the **complete** legend: hex `color`, caption `name` (≤ 32 chars), `order`, `isDefault`. At most 30, one default, no repeats. Leaving a color out removes it; items keep their own colors. |
| Archive / reactivate | `Roadmaps_ArchiveRoadmap` / `Roadmaps_ActivateRoadmap` | Archive keeps everything, read-only. Prefer it to deleting. |
| Delete | `Roadmaps_DeleteRoadmap` | Permanent, with all items. |

`Roadmaps_UpdateRoadmap` sets `name`, `description`, `start`, `end`, `roadmapManagerIds` and `visibilityId` from the body. Read the roadmap with `Roadmaps_GetRoadmap` first and send every field back: an omitted `description` is cleared, and a manager missing from `roadmapManagerIds` loses access to change it — and, on a private roadmap, to see it.

### Managing items

| Goal | Tool | Notes |
|---|---|---|
| Add an item | `Roadmaps_CreateItem` | See *Item bodies* below. Returns the new item's id. |
| Change some fields | `Roadmaps_PatchItem` | JSON Patch; fields it does not name keep their value. Preferred for small edits. |
| Rewrite an item | `Roadmaps_UpdateItem` | **Whole-record overwrite** — an omitted `description` or `color` is cleared and an omitted `parentId` moves the item to the top level. Read it with `Roadmaps_GetItem` first. |
| Change only dates | `Roadmaps_UpdateItemDates` | Leaves name, parent and color alone. |
| Reorder or re-parent an activity | `Roadmaps_UpdateActivityPlacement` | `parentId` (an activity, or `null` for top level) and 1-based `order` among its siblings. Activities only. |
| Delete an item | `Roadmaps_DeleteItem` | Deleting an activity deletes **everything nested under it**. |

`Roadmaps_PatchItem` changes one existing item's fields, e.g. `[{ "op": "replace", "path": "/date", "value": "2026-11-02" }]` to move a milestone (`/start` and `/end` for an activity or timebox; `/name`, `/description`, `/color`, `/parentId` for any).

#### Item bodies

`Roadmaps_CreateItem`, `Roadmaps_UpdateItem` and `Roadmaps_UpdateItemDates` take a body whose **`$type` must be its first property** and name the item's type — an update's must match the existing item:

- `"activity"` or `"timebox"` — with `start` and `end` (`YYYY-MM-DD`, start ≤ end)
- `"milestone"` — with `date`

Create and update bodies also take `roadmapId` (and `itemId` on update), `name` (≤ 128 chars), and optional `description`, `color` (hex, e.g. `#4096FF`) and `parentId` (the UUID of an activity on the same roadmap). For example, to add a milestone under an activity:

```json
{ "$type": "milestone", "roadmapId": "<roadmap id>", "name": "Beta launch", "date": "2026-11-02", "parentId": "<activity id>" }
```

### Before deleting

Before `Roadmaps_DeleteRoadmap` or `Roadmaps_DeleteItem`:

1. Read the items with `Roadmaps_GetItems`.
2. Tell the user exactly what will go, counted from that response — e.g. "Deleting activity *Platform rebuild* also deletes 4 child activities and 2 milestones." For a whole roadmap, offer `Roadmaps_ArchiveRoadmap` instead.
3. Get the user's explicit confirmation, then call the tool.
