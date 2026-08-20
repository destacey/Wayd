// timeline/core/rollup.ts
// Summary rollup (pure, generic). For a tree of records where some nodes are
// parents (containers) and some carry a date range, compute each parent's
// rolled-up span (min descendant start → max descendant end) and an aggregated
// progress. Used by a Gantt to draw a summary bar on a parent/stage row.
//
// This is deliberately decoupled from any specific record type: the caller
// supplies accessors, so it works equally for a WaydGrid tree node or a
// timeline item tree. Progress is optional end-to-end — a parent's progress is
// undefined unless at least one descendant carries one.

/** A summarized span for one parent node, derived from its descendants. */
export interface SummarySpan {
  /** Min start of descendant ranges, epoch ms. */
  start: number
  /** Max end of descendant ranges, epoch ms. */
  end: number
  /**
   * Duration-weighted mean progress (0..100) over descendants that carry a
   * progress value, or undefined when none do.
   */
  progress?: number
}

/** Accessors describing how to read a node's shape. */
export interface RollupAccessors<T> {
  /** Stable id for the node. */
  id: (node: T) => string
  /** Child nodes (empty/undefined for a leaf). */
  children: (node: T) => T[] | undefined
  /** Start of this node's own range, epoch ms, or undefined if it has none. */
  start: (node: T) => number | undefined
  /** End of this node's own range, epoch ms, or undefined if it has none. */
  end: (node: T) => number | undefined
  /** This node's own progress (0..100), or undefined. */
  progress?: (node: T) => number | undefined
}

interface Agg {
  start: number
  end: number
  weighted: number // Σ progress * duration, over descendants that HAVE progress
  weight: number // Σ duration, over descendants that HAVE progress
  count: number // number of contributing descendant ranges
}

/**
 * Walk the tree and compute a SummarySpan for every node that has at least one
 * DESCENDANT range (i.e. a parent whose span should bracket its children). A
 * leaf, or a node whose own range already covers it, is not summarized — the
 * caller draws that node's own bar directly. Returns a map keyed by node id.
 */
export function rollupSummaries<T>(
  roots: T[],
  accessors: RollupAccessors<T>,
): Map<string, SummarySpan> {
  const { id, children, start, end, progress } = accessors
  const out = new Map<string, SummarySpan>()

  // Post-order walk: returns the aggregate of a node's OWN range plus all its
  // descendants, so a parent can bracket the whole subtree beneath it.
  const visit = (node: T): Agg | null => {
    const kids = children(node) ?? []
    let agg: Agg | null = null

    const fold = (
      s: number,
      e: number,
      p: number | undefined,
    ) => {
      const duration = Math.max(0, e - s)
      const w = duration || 1
      if (!agg) {
        agg = {
          start: s,
          end: e,
          weighted: p != null ? p * w : 0,
          weight: p != null ? w : 0,
          count: 1,
        }
      } else {
        agg.start = Math.min(agg.start, s)
        agg.end = Math.max(agg.end, e)
        if (p != null) {
          agg.weighted += p * w
          agg.weight += w
        }
        agg.count += 1
      }
    }

    // This node's own range (a parent may or may not have one).
    const os = start(node)
    const oe = end(node)
    if (os != null && oe != null) {
      fold(os, oe, progress?.(node))
    }

    // Descendants.
    let childRanges = 0
    for (const child of kids) {
      const childAgg = visit(child)
      if (childAgg) {
        childRanges += childAgg.count
        fold(
          childAgg.start,
          childAgg.end,
          childAgg.weight > 0 ? childAgg.weighted / childAgg.weight : undefined,
        )
      }
    }

    // Only nodes with at least one DESCENDANT range get a summary entry — a leaf
    // (own range, no children) draws its own bar and needs no rollup.
    if (agg && childRanges > 0) {
      const a = agg as Agg
      out.set(id(node), {
        start: a.start,
        end: a.end,
        progress: a.weight > 0 ? a.weighted / a.weight : undefined,
      })
    }

    return agg
  }

  for (const root of roots) visit(root)
  return out
}
