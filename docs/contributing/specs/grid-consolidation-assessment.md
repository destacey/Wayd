---
title: Grid Consolidation Assessment
description: Decision record for consolidating AG Grid Community + TanStack Table onto a single grid foundation.
audience: [developer, agent]
---

# Grid Consolidation Assessment

**Status:** Proposed — spec/design only, no implementation scheduled yet.
**Date:** 2026-07-01
**Decision:** Standardize on **TanStack Table** as the single grid foundation; expose house components over one shared core; retire AG Grid Community.

> **Update (2026-07-02):** The *library* decision below stands. The **component
> shape has since been revised**: instead of *two* components (`WaydDataGrid` +
> `WaydTreeGrid`), we will ship **one** `WaydGrid` with **tree as a mode**
> (`getSubRows` present → hierarchical behavior). See the
> [Grid Core Implementation Plan](./grid-core-implementation-plan.md) for the
> current component shape and the phased, core-first migration. Where this
> document says "two components," read it as superseded on that point only.

## Context

The React client currently ships **two** grid libraries:

| Library | House component | Usage | Role |
| --- | --- | --- | --- |
| `ag-grid-community` 35.3.1 (`ag-grid-react`) | `WaydGrid` (`components/common/wayd-grid.tsx`) | ~52 screens | Flat lists: sorting, floating filters, quick search, CSV export, theming |
| `@tanstack/react-table` 8.21 | `TreeGrid` (`components/common/tree-grid/`) | 3 screens (project plan, roadmap items, project work items) | Hierarchical data with inline editing + drag/drop reorder |

TanStack was introduced because **tree data is AG Grid Enterprise-only** — see [Feature licensing reality](#feature-licensing-reality). Inline cell editing is *not* Enterprise; AG Grid Community supports it.

### The problem to solve

The two libraries have a slightly different look and feel and, more importantly, **different models for column filtering and cell rendering**. Planned work expands advanced column filtering and rendering. Building that twice — once per library — is the cost we want to avoid.

**Goal:** one underlying grid library so filtering, rendering, and look-and-feel are built **once** and shared. We will still expose **two components** (a flat data grid and a tree grid), but both must be backed by the same core. We do **not** want to keep both libraries.

## The deciding question: extensibility, not licensing

The user explicitly set Enterprise aside. The real question is: **can we build tree data + advanced filtering/rendering ourselves on top of AG Grid Community, or does its extensibility model prevent it?**

AG Grid Community has two layers with **opposite** extensibility stories:

### 1. Presentation layer — fully open

Cell renderers, header renderers, **custom filter components**, floating-filter components, cell editors, tooltips, and overlays are all first-class React-component plug-ins in Community. Any advanced filtering **UI** (multi-condition, faceted, date-range, async option lists) and any cell/header rendering is buildable. This layer is not restricted.

### 2. Row model layer — closed

The data pipeline — how rows are grouped, nested, and expand/collapse — is driven by **row models**. Community ships only `ClientSideRowModel`; tree/grouping behavior lives in the Enterprise `TreeDataModule` / `RowGroupingModule`. Verified against the installed package: neither `TreeDataModule` nor `RowGroupingModule` is exported by `ag-grid-community` 35.3.1, and there is **no public API to register a custom row model** or to teach `ClientSideRowModel` to nest rows.

**Conclusion on extensibility:**

- Advanced filtering + rendering on Community — **buildable, not restricted.**
- Tree data on Community — **not buildable *through* the model; only faked *around* it.**

### Why the "fake it around the model" path defeats the goal

You can render a tree in Community by pre-flattening the hierarchy into a flat row array, tracking expand/collapse in your own React state, filtering the flat array to visible descendants yourself, and drawing indentation/chevrons in a custom cell renderer. AG Grid then renders a flat list that *looks* like a tree.

The problem is what that does to **filtering and sorting**, which is exactly what we're trying to unify:

- AG Grid's filter model operates on the synthetic flat rows and has no concept of parent/child. "Show rows matching X **plus their ancestors so the tree stays coherent**" is logic **we** write, outside AG Grid — the same leaf-to-root behavior `TreeGrid` already implements via TanStack's `filterFromLeafRows` (see `tree-grid.tsx`).
- Sorting has the same issue: AG Grid sorts the flat list globally; keeping sort **within each parent's children** is again our code.

So the flat grid would use AG Grid's real filter model while the tree grid uses hand-rolled ancestor-aware filtering on top. **That is two filtering models again** — the split simply moves from "two libraries" into "two models inside one library," which does not satisfy the goal.

### Why TanStack is extensible at the layer we need

TanStack is headless — there is **no** closed row-model wall. Tree data (`getSubRows`, `getExpandedRowModel`), filtering (`getFilteredRowModel` + `filterFromLeafRows`), and sorting are first-class and compose. `TreeGrid` already proves the tree half works. **A flat grid is a strict subset**: a TanStack table with no `getSubRows` and no expansion. The filter model, sort model, column/meta model, and cell rendering become **literally the same code** for both components — which is the unification the goal requires.

## Decision

**Standardize on TanStack Table.** Expose house components over one shared core, and retire AG Grid Community.

> **Superseded (2026-07-02):** the two-component split below was revised to a
> **single `WaydGrid` with a tree mode**. See the
> [Grid Core Implementation Plan](./grid-core-implementation-plan.md). The
> shared-core rationale in this section still holds.

- ~~`WaydDataGrid` — flat lists (replaces `WaydGrid`, ~52 screens).~~
- ~~`WaydTreeGrid` — hierarchical data (the current `TreeGrid`, renamed).~~
- **Now:** one `WaydGrid`, flat by default; `getSubRows` → tree mode.

Both modes sit on a shared core: shared column/meta types, shared filter engine + filter-UI components, shared cell renderers, shared toolbar, shared CSV export. Most of this already exists under `components/common/tree-grid/` and `wayd-grid2/` and is reused, not rewritten.

### Why not the alternatives

- **Keep both libraries** — rejected by the goal; the whole point is a single foundation so filtering/rendering are built once.
- **Consolidate onto AG Grid Community** — its row-model layer is closed, so a shared tree-aware data pipeline is impossible; you re-create the two-model split inside one library (see above).
- **Buy AG Grid Enterprise** — explicitly out of scope for this decision; the question was about building on Community ourselves.

## Feature licensing reality

For the record (informs why the split existed, not the decision):

| Feature | AG Grid edition | Notes |
| --- | --- | --- |
| Sorting / Filtering / Quick filter | Community | |
| Cell editing / Full-row editing | **Community** | Editing was never the blocker |
| Managed row dragging | Community | Flat only |
| CSV export | Community | Excel export is Enterprise |
| **Tree Data** | **Enterprise** | The actual reason TanStack was introduced |
| Row Grouping / Pivoting | Enterprise | |

> Note: AG Grid's own docs are internally inconsistent here — a comparison table has at times listed Tree Data as "Community," but the Tree Data feature page is tagged **Enterprise**, and `TreeDataModule` is confirmed absent from the Community package. Treat the feature-page tag + package exports as authoritative.

## What already exists (reuse inventory)

Under `components/common/tree-grid/`, ready to promote into the shared core:

- **Filter engine** (`tree-grid-filters.ts`): `stringContainsFilter`, `setContainsFilter`, and a `numberRangeFilter` that parses `>=4`, `< 10`, `2-6`, `2..6`, `..6`, `2..`, exact. This is the seed of the "new, better filter model."
- **Column meta** (`types.ts` → `TreeGridColumnMeta`): `filterType`, `filterOptions`, export formatter/header hooks.
- **Toolbar** (`tree-grid-toolbar.tsx`): search, row count, refresh, clear filters, CSV export, slots.
- **Editing** (`use-tree-grid-editing.ts`, 743 lines), **DnD** (`tree-dnd-utils.ts` + `@dnd-kit`), **CSV export**, **auto-height** (`useRemainingHeight`).

Gap to build: the flat `WaydDataGrid` facade + a **shared, richer filter-UI layer** (see next section). AG Grid's `column-types.ts` (`dateOnly` / `dateTime` value formatters + date filters) must be re-expressed as TanStack column meta + a date filter component.

## Direction chosen (from review questions)

- **Filter UX bar:** *New, better filter model* — do **not** replicate AG Grid's floating filters one-for-one. Design the advanced filtering model we actually want as the shared core (per-column filter descriptors, typed operators, faceted/set filters with counts, date ranges, numeric operators), consumed identically by both flat and tree components.
- **Sequencing:** *Spec/design only for now.* This document is that spec. No migration code is scheduled; scope the rollout separately before writing code.

## Open design questions (to resolve before implementation)

1. **Filter model shape.** A per-column `filterDescriptor` (type + operator set + options source) vs. free-form `FilterFn`. Recommend descriptors so filter UI, URL/state persistence, and CSV/export semantics derive from one declaration.
2. **State persistence.** Whether column filters/sort/sizing persist per user (localStorage) — align with existing `useLocalStorageState` versioning conventions.
3. **Virtualization.** AG Grid virtualizes rows out of the box; TanStack does not. The 52 flat grids include some large lists — decide whether to adopt `@tanstack/react-virtual` in the shared core (recommended) before migrating high-row-count screens.
4. **Naming/facade.** `WaydGrid` → `WaydDataGrid`, `TreeGrid` → `WaydTreeGrid`, per the `Wayd*` house-component convention. Keep both as thin facades over the shared core so the swap is localized.
5. **Migration ordering.** Build shared core + one pilot flat grid first; roll out the remaining 51 incrementally; delete `ag-grid-community` / `ag-grid-react` only after the last screen migrates.

## Consequences

- **Positive:** one filtering/rendering model shared by both components; AG Grid dependency retired; ~60% of the hard work (tree, editing, DnD, filter engine) already exists.
- **Cost:** the 52 flat grids lose AG Grid's built-in floating-filter UIs and must adopt the new shared filter components; virtualization must be added deliberately. This is bounded, front-loaded work, and it is the *same category* of effort a Community-only path would demand (hand-building ancestor-aware filtering) — but here it actually achieves the single-foundation goal.
