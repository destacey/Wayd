// timeline2/layout/gantt-layout.ts
// Gantt variant: ONE row per record, no lane-packing. Groups render as tree
// rows (children are their own rows, indented by depth). Minimal v1 — dependency
// arrows and the multi-column grid-left panel are deferred, additive layers
// (spec non-goal) and are NOT handled here.

import type { ResolvedRow, TimelineItem } from '../core/types'
import {
  type LayoutInput,
  type LayoutOutput,
  resolveConfig,
} from './layout-strategy'

/** One item => one single-lane row. */
function itemRow(
  item: TimelineItem,
  depth: number,
  top: number,
  laneHeight: number,
  rowPadding: number,
): ResolvedRow {
  return {
    // One row per record → identity is the item id (groups hold many item-rows).
    rowKey: item.id,
    groupId: item.groupId,
    depth,
    top,
    laneCount: 1,
    height: laneHeight + rowPadding,
    items: [{ item, lane: 0 }],
  }
}

const itemSort = (a: TimelineItem, b: TimelineItem) => {
  const oa = a.order ?? 0
  const ob = b.order ?? 0
  if (oa !== ob) return oa - ob
  if (a.start !== b.start) return a.start - b.start
  return a.id < b.id ? -1 : a.id > b.id ? 1 : 0
}

export function ganttLayout(input: LayoutInput): LayoutOutput {
  const { laneHeight, rowPadding } = resolveConfig(input.config)
  const { items, groups } = input

  const rows: ResolvedRow[] = []
  let top = 0
  const push = (item: TimelineItem, depth: number) => {
    const row = itemRow(item, depth, top, laneHeight, rowPadding)
    rows.push(row)
    top += row.height
  }

  // ── Ungrouped: each item is its own row, ordered ──────────────────────────
  if (!groups || groups.length === 0) {
    for (const item of [...items].sort(itemSort)) push(item, 0)
    return { rows, totalHeight: top }
  }

  // ── Grouped: walk the group tree; emit a row per item under each group ─────
  const childIds = new Map<string, string[]>()
  const depthOf = new Map<string, number>()
  const roots: string[] = []
  for (const g of groups) {
    if (g.parentId && groups.some((x) => x.id === g.parentId)) {
      const list = childIds.get(g.parentId)
      if (list) list.push(g.id)
      else childIds.set(g.parentId, [g.id])
    } else {
      roots.push(g.id)
    }
  }
  const byId = new Map(groups.map((g) => [g.id, g]))
  const setDepth = (id: string, depth: number) => {
    depthOf.set(id, depth)
    for (const c of childIds.get(id) ?? []) setDepth(c, depth + 1)
  }
  roots.forEach((id) => setDepth(id, 0))

  const orderGroups = (ids: string[]) =>
    [...ids].sort((a, b) => {
      const ga = byId.get(a)!
      const gb = byId.get(b)!
      const oa = ga.order ?? 0
      const ob = gb.order ?? 0
      if (oa !== ob) return oa - ob
      return ga.id < gb.id ? -1 : ga.id > gb.id ? 1 : 0
    })

  const directItems = new Map<string, TimelineItem[]>()
  for (const item of items) {
    if (item.groupId == null) continue
    const list = directItems.get(item.groupId)
    if (list) list.push(item)
    else directItems.set(item.groupId, [item])
  }

  const emit = (id: string) => {
    const depth = depthOf.get(id) ?? 0
    const groupItems = (directItems.get(id) ?? []).sort(itemSort)
    for (const item of groupItems) push(item, depth)
    for (const childId of orderGroups(childIds.get(id) ?? [])) emit(childId)
  }
  for (const id of orderGroups(roots)) emit(id)

  return { rows, totalHeight: top }
}
