// timeline2/core/types.ts
// Shared, variant-agnostic domain types for WaydTimeline2.
// These describe DATA and GEOMETRY only — no React, no app/domain coupling.
// See docs/contributing/specs/custom-timeline-requirements.md.

/** The two layout variants. Only the layout strategy branches on this. */
export type TimelineVariant = 'timeline' | 'gantt'

/** Kind of item drawn on the chart. */
export type ItemKind = 'range' | 'milestone' | 'background'

/**
 * A single chart item, normalized to epoch-millisecond bounds so all core math
 * is plain number arithmetic (no Date/string/dayjs ambiguity — that conversion
 * happens at the adapter boundary, not in core).
 */
export interface TimelineItem<T = unknown> {
  id: string
  kind: ItemKind
  /** Inclusive start, epoch ms. */
  start: number
  /** Exclusive end, epoch ms. For milestones, equals `start`. */
  end: number
  /** Display label shown on the bar (first-class; default renderer uses it). */
  label?: string
  /** Bar background color (any CSS color). Text contrast is auto-derived. */
  color?: string
  /** Group this item belongs to; undefined = ungrouped. */
  groupId?: string
  /**
   * Tree depth of this item (0-based). Drives the timeline drill level: at
   * level N, item-kind nodes at this exact level render as bars; shallower
   * group-kind nodes become group rows. Set by the adapter for hierarchical
   * data; omitted = flat (depth 0).
   */
  treeLevel?: number
  /** Optional deterministic ordering hint (lower = earlier in packing/placement). */
  order?: number
  /** Progress 0..100, if the item supports a progress handle. */
  progress?: number
  /** Opaque payload the render layer / consumer carries through (DTO, etc.). */
  data?: T
}

/** A group (row header / lane container). May nest via `parentId`. */
export interface TimelineGroup<T = unknown> {
  id: string
  parentId?: string
  /** Tree depth of this group (0-based). Set by the adapter; defaults to the
   *  computed parent-chain depth when omitted. */
  treeLevel?: number
  /** Display label shown in the left group column. */
  label?: string
  /** Collapsed groups pack descendants onto the parent lane (timeline variant). */
  collapsed?: boolean
  order?: number
  data?: T
}

/**
 * Result of lane-packing within a single group (or the ungrouped set):
 * each item assigned a 0-based lane index; `laneCount` = lanes needed.
 */
export interface PackResult {
  /** itemId -> lane index (0-based). */
  lanes: Map<string, number>
  /** Number of lanes used (max laneIndex + 1; 0 if no items). */
  laneCount: number
}

/**
 * A resolved, renderable row produced by a layout strategy. The render layer
 * consumes these without knowing which variant produced them.
 */
export interface ResolvedRow {
  /**
   * Stable, unique identity for this row, used as the React key and for
   * label-measurement lookup. In the timeline variant a row IS a group, so this
   * is the group id; in the gantt variant a row is a single record, so it's the
   * item id (groups can hold many item-rows, which would otherwise collide).
   * Falls back to a positional id when neither exists (ungrouped packed row).
   */
  rowKey?: string
  /** Group id this row represents, or a synthetic id for ungrouped/packed rows. */
  groupId?: string
  /** Vertical offset of the row, px from the top of the chart body. */
  top: number
  /** Row height, px (group height = laneCount * laneHeight, min one lane). */
  height: number
  /** Number of stacked lanes within this row. */
  laneCount: number
  /** Items placed in this row, with their lane index. */
  items: Array<{ item: TimelineItem; lane: number }>
  /** Nesting depth for indenting group labels (0 = top level). */
  depth: number
  /**
   * Vertical headroom reserved at the top of the row, px — used so a row-scoped
   * background (timebox) shows its label band above the packed item bars. Item
   * lanes are offset down by this amount.
   */
  topInset?: number
}
