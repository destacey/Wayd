// timeline2/core/packing.ts
// HAND-ROLLED interval lane-packing (decision D-2): assign non-overlapping
// items to the fewest horizontal lanes — the defining timeline behavior.
// Pure function, the primary unit-test target (NFR-7).

import type { PackResult, TimelineItem } from './types'

export interface PackOptions {
  /** Optional effective exclusive end for collision detection. */
  getCollisionEnd?: (item: TimelineItem) => number
}

/**
 * Greedy interval-scheduling lane assignment.
 *
 * Items are sorted by (start, then `order`, then id) for determinism, then each
 * item is placed in the first lane whose last item ends at or before this item's
 * start. If none fits, a new lane is opened. This yields the minimum number of
 * lanes for a set of intervals (classic greedy interval-graph colouring).
 *
 * Two items "overlap" when one starts strictly before the other ends. Touching
 * intervals (a.end === b.start) do NOT overlap and may share a lane — matching
 * the half-open [start, end) convention in TimelineItem.
 *
 * Background items are excluded — they are not lane-packed (they render behind).
 */
export function packLanes(
  items: TimelineItem[],
  options: PackOptions = {},
): PackResult {
  const packable = items.filter((i) => i.kind !== 'background')

  const sorted = [...packable].sort((a, b) => {
    if (a.start !== b.start) return a.start - b.start
    const ao = a.order ?? 0
    const bo = b.order ?? 0
    if (ao !== bo) return ao - bo
    return a.id < b.id ? -1 : a.id > b.id ? 1 : 0
  })

  const lanes = new Map<string, number>()
  // laneEnds[i] = exclusive end (epoch ms) of the last item placed in lane i.
  const laneEnds: number[] = []

  for (const item of sorted) {
    // Milestones occupy an instant; treat end as start so they pack tightly.
    const defaultEnd = item.kind === 'milestone' ? item.start : item.end
    const itemEnd = Math.max(
      defaultEnd,
      options.getCollisionEnd?.(item) ?? defaultEnd,
    )

    let placed = -1
    for (let lane = 0; lane < laneEnds.length; lane += 1) {
      if (laneEnds[lane] <= item.start) {
        placed = lane
        break
      }
    }

    if (placed === -1) {
      placed = laneEnds.length
      laneEnds.push(itemEnd)
    } else {
      laneEnds[placed] = itemEnd
    }

    lanes.set(item.id, placed)
  }

  return { lanes, laneCount: laneEnds.length }
}
