---
title: Grid Core Implementation Plan
description: Implementation plan for extracting a shared WaydGrid core and unifying the flat grid and tree grid into one component with a tree mode.
audience: [developer, agent]
---

# Grid Core Implementation Plan

**Status:** Proposed — plan approved in principle; execution not started.
**Date:** 2026-07-02
**Builds on:** [Grid Consolidation Assessment](./grid-consolidation-assessment.md) (the library-choice decision: standardize on TanStack, retire AG Grid).

This document is the *implementation* companion to the assessment. The assessment
answered **"which library and why."** This answers **"what we build, in what
order, keeping both grids green the whole way."**

## What changed since the assessment

The assessment (2026-07-01) predates the maturing of `WaydGrid2`. Since then we
have built, on TanStack, a real:

- Descriptor **filter engine** (`filter-model` + `filter-engine`) and filter UI
  (popup, floating row, Excel-style **set** and **date** panels, combined
  text+set, operator-aware summaries) — ~1,500 lines.
- **Column-type registry** (`yesNo` / `dateOnly` / `dateTime` via `meta.columnType`).
- `meta.hide` column visibility, **actions column** factory, **cell renderers**
  (link builders), and `sortEmptyLast` (empties-last default sort).

Reading both grids end-to-end (`tree-grid.tsx` 1021 lines, `wayd-grid2.tsx` 753
lines, `use-tree-grid-editing.ts` 743 lines, `tree-dnd-utils.ts` 282 lines) also
established two facts that shape this plan:

1. **The inline-editing hook is ~grid-agnostic already.** Its selection / focus /
   keyboard / save / click-outside machinery operates on `rowId` + `columnId` +
   DOM `data-cell-id`. The only "tree" coupling is `T extends TreeNode` (needs
   only `{ id }`) and drafts carrying a `parentId` / expand-on-add-child.
2. **DnD splits cleanly** into shared *mechanics* (dnd-kit sensors,
   `SortableContext`, sortable-row, `onDragEnd` → new order) and a tree-only
   *projection* layer (horizontal offset → indentation depth → parent
   reassignment, circular-ref prevention). Flat DnD is the mechanics with the
   projection removed.

## Decision update: one component, tree as a mode

The assessment sketched **two** components (`WaydDataGrid` + `WaydTreeGrid`).
This plan **supersedes** that with **one** component:

> **`WaydGrid`** — flat by default. Provide `getSubRows` (and enable expansion)
> → **tree mode** turns on: expansion, indentation, `filterFromLeafRows` (a
> matching child keeps its ancestor chain visible), and reparenting DnD.

Rationale: the user's requirement that tree grids get the **same** descriptor
filtering — differing only in that a matching child keeps its ancestors visible —
means "filter a tree" is literally "filter a flat grid + `filterFromLeafRows`".
Combined with grid-agnostic editing and cleanly-layered DnD, there is no
remaining behavior that justifies a second component. The tree is a **mode**
selected by data shape (`getSubRows` present), exactly as TanStack itself models
it. `TreeGrid` is absorbed; no `WaydTreeGrid` name survives.

### Naming / sequencing

- The unified component is **`WaydGrid`**. Today the ag-grid component owns that
  name; while both coexist, the new one lives as `wayd-grid2` / `WaydGrid2` and is
  renamed to `WaydGrid` only when the ag-grid one is retired (assessment's job).
- Shared engine lives in **`components/common/wayd-grid-core/`**.
- Keep the `Wayd` house-component prefix (see house convention).

## Target architecture

```
components/common/wayd-grid-core/          ← universal engine (flat by default)
  use-grid-table.ts        table config + shared state (sorting, filters,
                           sizing, global search, multisort, resize)
  grid-export.ts           CSV export — the FIXED version (visible leaf cols,
                           row.getValue for nested keys)
  grid-filters.ts          moved from tree-grid: stringContains / set /
                           numberRange global+column fns
  grid-sorting.ts          sortEmptyLast (+ dateSortBy)
  grid-header-row.tsx      shared <thead> sort/resize header cell
  use-grid-editing.ts      the editing hook, de-tree-ified ({ id } not TreeNode)
  dnd/
    grid-dnd.ts            shared drag MECHANICS (sensors, sortable ctx, onDragEnd)
    tree-projection.ts     tree-ONLY reparenting projection (moved as-is)
  filters/                 the descriptor filter engine + UI (from wayd-grid2)
  column-types.ts, cell-renderers.ts, actions-column.ts  (from wayd-grid2)
  types.ts                 shared column meta + props

components/common/wayd-grid/                ← the ONE component
  wayd-grid.tsx            flat by default; getSubRows → tree mode
  row-renderer            seam: flat row form vs tree row form (indent + caret +
                           sortable wrapper). Core owns <tbody>; delegates the
                           per-row cell layout to the active form.
```

### The row-renderer seam (the one real design risk)

Flat rows and tree rows differ in *rendering* (indentation, expand caret,
sortable-row wrapper), not in data. Do **not** thread `if (isTree)` through one
render path — that recreates the entanglement we are removing. Instead the core
renders `<tbody>` and the row structure, and delegates the per-row **cell layout**
to a small row-renderer that has a **flat form** and a **tree form**. Tree mode
also wraps rows in the dnd sortable context + adds the caret/indent cell.

## What moves where

| Concern | Destination | Notes |
| --- | --- | --- |
| Table config + 4 state hooks + `getColumnCanGlobalFilter` + multisort/resize | `use-grid-table.ts` | Byte-identical between grids today. |
| CSV export | `grid-export.ts` | **Ship the fixed version** (visible-only + `row.getValue`). TreeGrid currently has the old buggy copy. |
| `stringContains` / `set` / `numberRange` filter fns | `grid-filters.ts` | Move from `tree-grid-filters.ts`; wayd-grid2 already imports these from tree-grid. |
| `sortEmptyLast`, `dateSortBy` | `grid-sorting.ts` | |
| Descriptor filter engine + all filter UI | `wayd-grid-core/filters/` | From wayd-grid2. Becomes the ONE filter system for both modes. |
| `column-types`, `cell-renderers`, `actions-column`, `meta.hide` | `wayd-grid-core/` | From wayd-grid2. |
| Editing hook | `use-grid-editing.ts` | Rename off "tree"; relax `T extends TreeNode` → `T extends { id: string }`. Behavior unchanged. |
| DnD mechanics | `dnd/grid-dnd.ts` | Shared; flat reorder = `arrayMove`, no projection. |
| DnD projection (reparenting) | `dnd/tree-projection.ts` | **Tree mode only.** Moved as-is. |
| Header/sort/resize `<thead>` cell | `grid-header-row.tsx` | Near-identical today. |
| Toolbar | `wayd-grid-core/` toolbar | Reconcile the two toolbars into one. |
| Expansion, `getSubRows`, indentation, caret | tree mode of `WaydGrid` | The genuinely tree-specific rendering. |

## Filtering convergence (per user direction)

Tree grids adopt the **descriptor** filter engine — the same popups, floating
row, set/date panels used by the flat grid — replacing TreeGrid's current inline
`Input`/`Select` filter row. The **only** tree difference is `filterFromLeafRows:
true` (already set in `tree-grid.tsx`), so a matching child keeps its ancestor
chain visible. This is a **UX change** for the 3 tree screens (new filter
affordance), so it is called out as its own migration step, not a silent swap.

## Migration plan — core-first, incremental (green at every commit)

Each step compiles, passes tests, and leaves **both** existing grids working.
No big-bang cutover; the unified `WaydGrid` is assembled only after the core is
proven by having both current grids consume it.

**Phase 0 — Scaffolding**
0.1 Create `wayd-grid-core/`. Move the already-shared, dependency-free utils
    (`grid-filters.ts`, `grid-sorting.ts`) out of `tree-grid`; update both grids'
    imports. Pure move; behavior identical.

**Phase 1 — Fix + share the safe, identical pieces**
1.1 `grid-export.ts`: extract the **fixed** export (visible leaf cols +
    `row.getValue`). Point wayd-grid2 at it (no behavior change — already fixed),
    then point TreeGrid at it (**fixes TreeGrid's nested-key + hidden-column
    export bugs**). Add a shared export test.
1.2 `use-grid-table.ts`: extract table-config/state hook; both grids consume it.
1.3 `grid-header-row.tsx`: extract the header sort/resize cell; both grids use it.

**Phase 2 — Share editing + DnD mechanics**
2.1 Rename/relocate editing hook to `use-grid-editing.ts`; relax the type bound.
    TreeGrid keeps working (it just imports from the new path). Flat grids can now
    opt into editing later.
2.2 Split DnD: `grid-dnd.ts` (mechanics) + `tree-projection.ts` (reparenting).
    TreeGrid composes both, unchanged.

**Phase 3 — Share the filter engine + column model**
3.1 Move the descriptor filter engine, filter UI, column-types, cell-renderers,
    actions-column, `meta.hide` into `wayd-grid-core/`. wayd-grid2 re-points its
    imports (barrel keeps its public surface stable).

**Phase 4 — Assemble the unified `WaydGrid`**
4.1 Build `WaydGrid` on the core with the **row-renderer seam**: flat form first
    (equivalent to today's wayd-grid2). Migrate wayd-grid2's 3 callers; delete
    the old wayd-grid2 shell.
4.2 Add **tree mode** (expansion, indentation, caret, `filterFromLeafRows`, tree
    DnD projection). Migrate TreeGrid's 3 callers onto `WaydGrid` tree mode —
    including the **filter UX change** to descriptor filters. Delete the old
    TreeGrid shell.

**Phase 5 — (separate effort) ag-grid retirement**
Out of scope here; tracked by the assessment. When it lands, `WaydGrid2`/
`wayd-grid2` is renamed to `WaydGrid`/`wayd-grid` and takes the canonical name.

## Caller inventory (migration targets)

**Tree grid (3 screens, become `WaydGrid` tree mode):**
- `app/planning/roadmaps/_components/roadmap-items-grid.tsx` (+ `.columns.tsx`)
- `app/ppm/projects/_components/project-plan-table.tsx` (+ `.columns.tsx`)
- `app/ppm/projects/_components/project-work-items-tree-grid.tsx`
  (+ `project-work-items-view-manager.tsx`)

**WaydGrid2 (3 screens, become `WaydGrid` flat mode):**
- `app/settings/feature-management/feature-flags/page.tsx`
- `app/planning/planning-intervals/_components/planning-interval-objectives-grid2.tsx`
- `app/planning/planning-intervals/[key]/objectives/grid2/page.tsx`

**AG-grid `WaydGrid` (~52 screens):** out of scope; retired by the assessment.

## Testing strategy

- Every moved util keeps its unit tests (relocate alongside).
- New shared `grid-export` gets the nested-key + visible-only tests (already
  written for wayd-grid2; generalize).
- jsdom covers logic; **browser verification (playwright-verifier)** is required
  for interaction/visual behavior that jsdom can't reproduce — the descriptor
  filter popover, DnD, and inline-edit focus flows have all had bugs only a real
  browser caught. Verify at each phase that touches rendering/interaction.
- Tree mode: add tests for `filterFromLeafRows` ancestor-visibility and the
  reparenting projection (the projection already has coverage to relocate).

## Open items to confirm before Phase 4

1. **Toolbar reconciliation.** The two toolbars differ slightly; confirm the
   unified toolbar's prop surface (esp. tree-only affordances like "add child").
2. **Virtualization.** Still deferred (assessment open item #3). Not required for
   the 6 grids in scope here, but the row-renderer seam should not preclude
   adding `@tanstack/react-virtual` later.
3. **State persistence.** Per-user filter/sort/sizing persistence (assessment
   open item #2) — out of scope for this plan; design separately.
```
