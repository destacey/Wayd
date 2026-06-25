// timeline2/core/depth.ts
// Drill-through level helpers (pure). A "drill level" is a global expand depth:
// groups at depth < level stay expanded; groups at depth >= level are collapsed
// (their descendants fold onto their lane). Level 1 = only top-level groups
// expanded; higher = drill deeper. This drives the toolbar -/+ controls while
// the layout still consumes plain per-group `collapsed` flags.

import type { TimelineGroup, TimelineItem } from './types'

/** Depth (0-based) of each group in the parent tree. */
export function groupDepths(groups: TimelineGroup[]): Map<string, number> {
  const byId = new Map(groups.map((g) => [g.id, g]))
  const cache = new Map<string, number>()

  const depthOf = (id: string): number => {
    const cached = cache.get(id)
    if (cached != null) return cached
    const g = byId.get(id)
    const d = g?.parentId && byId.has(g.parentId) ? depthOf(g.parentId) + 1 : 0
    cache.set(id, d)
    return d
  }

  for (const g of groups) depthOf(g.id)
  return cache
}

/** Maximum group depth (0-based). 0 when there are no nested groups. */
export function maxGroupDepth(groups: TimelineGroup[]): number {
  let max = 0
  for (const d of groupDepths(groups).values()) max = Math.max(max, d)
  return max
}

/**
 * Resolve a drill `level` into exactly what renders, mirroring the legacy
 * roadmap timeline's filter. Item/group `treeLevel` is 1-based here (top tier
 * = 1), matching the legacy `treeLevel`. Rules at level L:
 *
 *  - GROUPS: nodes (kind 'range') with `treeLevel < L` become group rows.
 *  - BARS: range items with `treeLevel === L` render in the chart. A node that
 *    is a group (treeLevel < L) does NOT also render its own bar.
 *  - DECORATIONS (milestone/background): render whenever `treeLevel <= L`
 *    (they never become groups — they always show under their nearest group).
 *  - Anything with `treeLevel > L` is hidden.
 *
 * Each surviving bar/decoration is assigned to its nearest ancestor group that
 * survives (treeLevel < L), or undefined (flat) when none do — e.g. at level 1
 * there are no groups, so level-1 bars render flat.
 *
 * `level` is clamped to [1, maxLevel] where maxLevel = deepest treeLevel.
 */
export function resolveLevel<T, G>(
  items: TimelineItem<T>[],
  groups: TimelineGroup<G>[],
  level: number,
): { items: TimelineItem<T>[]; groups: TimelineGroup<G>[] } {
  const byId = new Map(groups.map((g) => [g.id, g]))
  const depths = groupDepths(groups)
  const groupLevel = (g: TimelineGroup<G>) =>
    g.treeLevel ?? (depths.get(g.id) ?? 0) + 1

  // Deepest level present (consider both groups and items).
  let maxLevel = 1
  for (const g of groups) maxLevel = Math.max(maxLevel, groupLevel(g))
  for (const i of items) maxLevel = Math.max(maxLevel, i.treeLevel ?? 1)
  const L = Math.max(1, Math.min(level, maxLevel))

  // Groups that survive at this level: treeLevel < L. At level 1 NOTHING is a
  // group (flat chart) — even a treeLevel-0 synthetic root band — so all items
  // (incl. root timeboxes) fall through to the full-height/ungrouped area.
  const survivingGroups = L <= 1 ? [] : groups.filter((g) => groupLevel(g) < L)
  const survivesId = new Set(survivingGroups.map((g) => g.id))

  // Nearest surviving ancestor group for an arbitrary group id.
  const nearestSurviving = (id: string | undefined): string | undefined => {
    let cur = id
    while (cur) {
      if (survivesId.has(cur)) return cur
      cur = byId.get(cur)?.parentId
    }
    return undefined
  }

  const visibleItems: TimelineItem<T>[] = []
  for (const item of items) {
    const lvl = item.treeLevel ?? 1
    const isDecoration = item.kind !== 'range'
    // Bars: range items exactly at L. Decorations: at or above L. Hide deeper.
    const visible = isDecoration ? lvl <= L : lvl === L
    if (!visible) continue
    const target = nearestSurviving(item.groupId)
    visibleItems.push(target === item.groupId ? item : { ...item, groupId: target })
  }

  return { items: visibleItems, groups: survivingGroups }
}
