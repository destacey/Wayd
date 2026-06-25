// timeline2/layout/timeline-layout.ts
// Timeline variant: lane-packing + collapse-to-lane. Pure function.
//
//  - Ungrouped: one packed row containing all items.
//  - Grouped: groups form a tree (parentId). Each VISIBLE group becomes a row.
//    A group's row contains its own direct items PLUS — when the group is
//    collapsed — all items of its hidden descendants, packed together onto that
//    one row's lanes (collapse-to-lane). Expanded groups show descendants as
//    their own rows.

import { packLanes } from '../core/packing'
import type { ResolvedRow, TimelineGroup, TimelineItem } from '../core/types'
import {
  type LayoutInput,
  type LayoutOutput,
  resolveConfig,
} from './layout-strategy'

interface GroupNode {
  group: TimelineGroup
  depth: number
  childIds: string[]
}

/** Build a packed ResolvedRow from a set of items at a given top/depth/groupId.
 *  `topInset` reserves headroom (px) above the bars for a timebox label. */
function buildRow(
  items: TimelineItem[],
  groupId: string | undefined,
  depth: number,
  top: number,
  laneHeight: number,
  rowPadding: number,
  topInset = 0,
): ResolvedRow {
  const packed = packLanes(items)
  const placed = items.map((item) => ({
    item,
    lane: packed.lanes.get(item.id) ?? 0,
  }))
  // Bar area: one lane per packed lane. A normal row needs at least one lane to
  // be visible; but when `topInset` already provides content (a timebox label
  // band), an item-less row doesn't need an extra empty lane.
  const minLanes = topInset > 0 ? 0 : 1
  const barArea = Math.max(minLanes, packed.laneCount) * laneHeight
  return {
    // A timeline row IS a group, so its identity is the group id.
    rowKey: groupId,
    groupId,
    depth,
    top,
    laneCount: packed.laneCount,
    height: topInset + barArea + rowPadding,
    items: placed,
    topInset: topInset || undefined,
  }
}

export function timelineLayout(input: LayoutInput): LayoutOutput {
  const { laneHeight, rowPadding } = resolveConfig(input.config)
  const { items, groups } = input
  const bgGroupIds = input.backgroundGroupIds ?? new Set<string>()
  // Headroom reserved at the top of a row that has a timebox, for its label.
  const insetFor = (id: string) => (bgGroupIds.has(id) ? laneHeight : 0)

  // ── Ungrouped: a single packed row of everything ──────────────────────────
  if (!groups || groups.length === 0) {
    if (items.length === 0) return { rows: [], totalHeight: 0 }
    // Chart-wide timeboxes (e.g. level-1 root timeboxes) reserve top headroom
    // for their labels above the bars.
    const inset = input.hasChartBackground ? laneHeight : 0
    const row = buildRow(items, undefined, 0, 0, laneHeight, rowPadding, inset)
    return { rows: [row], totalHeight: row.height }
  }

  // ── Index groups, items, and parent/child relationships ───────────────────
  const nodes = new Map<string, GroupNode>()
  for (const group of groups) {
    nodes.set(group.id, { group, depth: 0, childIds: [] })
  }
  const roots: string[] = []
  for (const group of groups) {
    const parent = group.parentId ? nodes.get(group.parentId) : undefined
    if (parent) parent.childIds.push(group.id)
    else roots.push(group.id)
  }
  // Compute depth top-down so child indenting is correct.
  const setDepth = (id: string, depth: number) => {
    const node = nodes.get(id)
    if (!node) return
    node.depth = depth
    for (const childId of node.childIds) setDepth(childId, depth + 1)
  }
  roots.forEach((id) => setDepth(id, 0))

  // Items bucketed by their direct group.
  const directItems = new Map<string, TimelineItem[]>()
  for (const item of items) {
    if (item.groupId == null) continue
    const bucket = directItems.get(item.groupId)
    if (bucket) bucket.push(item)
    else directItems.set(item.groupId, [item])
  }

  /** All items of a group and its entire subtree (for collapse-to-lane). */
  const subtreeItems = (id: string): TimelineItem[] => {
    const node = nodes.get(id)
    if (!node) return []
    const own = directItems.get(id) ?? []
    const fromChildren = node.childIds.flatMap((childId) => subtreeItems(childId))
    return [...own, ...fromChildren]
  }

  // Stable ordering for sibling groups: by `order`, then id.
  const orderSiblings = (ids: string[]) =>
    [...ids].sort((a, b) => {
      const ga = nodes.get(a)!.group
      const gb = nodes.get(b)!.group
      const oa = ga.order ?? 0
      const ob = gb.order ?? 0
      if (oa !== ob) return oa - ob
      return ga.id < gb.id ? -1 : ga.id > gb.id ? 1 : 0
    })

  // ── Walk the tree depth-first, emitting a row per visible group ────────────
  const rows: ResolvedRow[] = []
  let top = 0

  const emit = (id: string) => {
    const node = nodes.get(id)
    if (!node) return
    const collapsed = node.group.collapsed === true

    // Collapsed => fold the whole subtree onto this row; expanded => direct only.
    const rowItems = collapsed ? subtreeItems(id) : directItems.get(id) ?? []
    const row = buildRow(
      rowItems,
      id,
      node.depth,
      top,
      laneHeight,
      rowPadding,
      insetFor(id),
    )
    rows.push(row)
    top += row.height

    if (!collapsed) {
      for (const childId of orderSiblings(node.childIds)) emit(childId)
    }
  }

  for (const id of orderSiblings(roots)) emit(id)

  return { rows, totalHeight: top }
}
