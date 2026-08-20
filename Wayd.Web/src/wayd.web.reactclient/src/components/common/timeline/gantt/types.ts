// timeline/gantt/types.ts
// The accessor seam that makes the Gantt pane domain-agnostic. A consumer
// describes how to read ITS OWN tree node shape (a roadmap item, a project plan
// node, whatever comes next) and the engine handles scale, rollup, bars,
// milestones, gridlines and drag rendering.

import type { BarDragState, DragMode } from '../index'

/** Minimal item shape the shared drag hook (useBarDrag) needs. */
export interface GanttDragItem {
  id: string
  start: number
  end: number
  kind: 'range'
}

/** How a row should be drawn on the chart. */
export type GanttRowKind = 'range' | 'milestone'

/**
 * How to read one node of the consumer's tree. Every accessor is pure and
 * called during the memoized build — no hooks, no side effects.
 */
export interface GanttAccessors<T> {
  /** Stable id, used for rollup keys and drag identity. */
  id: (node: T) => string
  /** Child nodes; empty/undefined for a leaf. */
  children: (node: T) => T[] | undefined
  /** Label drawn on the bar and used in the tooltip. */
  name: (node: T) => string
  /**
   * Whether this row is a point-in-time marker (diamond) or a span (bar).
   * Defaults to 'range' when omitted.
   */
  kind?: (node: T) => GanttRowKind
  /**
   * The node's own [start, end] in epoch ms, or undefined when it has no dates
   * of its own (a container row — it will get a rolled-up summary bar instead).
   * For a milestone, return the same value twice.
   */
  range: (node: T) => [number, number] | undefined
  /** Optional bar fill color (any CSS color). Text contrast is auto-derived. */
  color?: (node: T) => string | undefined
  /** Optional progress 0..100, drawn as a fill overlay on the bar. */
  progress?: (node: T) => number | undefined
  /**
   * Whether THIS row's dates may be dragged/resized, when the pane is editable.
   * Defaults to true. Use it to freeze rows the API can't reschedule.
   */
  editable?: (node: T) => boolean
  /**
   * Optional style variant for a range bar. 'muted' draws the lighter banded
   * treatment (roadmap timeboxes); default draws the solid primary bar.
   */
  variant?: (node: T) => 'default' | 'muted' | undefined
}

export interface GanttPaneOptions<T> {
  /** Pixels per day (zoom level). */
  pxPerDay?: number
  /** When true, range bars get resize handles and are drag-movable. */
  editable?: boolean
  /** The live drag (from the shared useBarDrag hook), rendered as a draft. */
  activeDrag?: BarDragState | null
  /** Begin a drag for a bar (wired to useBarDrag.start by the consumer). */
  onBarPointerDown?: (
    e: React.PointerEvent,
    item: GanttDragItem,
    mode: DragMode,
  ) => void
  /**
   * Pointer offset (px) from the dragged bar's left edge, captured at move-drag
   * start, so the live date label tracks the cursor. Undefined = center.
   */
  moveGrabOffset?: number
  /**
   * Optional extra bounds (epoch ms) folded into the domain — e.g. a roadmap's
   * declared window, or a project's start/end — so the axis covers the planning
   * window even when no item reaches its edges.
   */
  domainHint?: [number | undefined, number | undefined]
  /** Suggested default pane width, px. */
  defaultWidth?: number
}

export interface GanttPaneModel<T> {
  /** Header (date axis) for WaydGrid's rightPane.header. */
  header: React.ReactNode
  /** Per-row bar renderer for WaydGrid's rightPane.renderRow. */
  renderRow: (ctx: {
    row: { original: T }
    top: number
    height: number
  }) => React.ReactNode
  /** Chart-wide gridline layer for WaydGrid's rightPane.renderBackground. */
  renderBackground: (ctx: { totalHeight: number }) => React.ReactNode
  /** Suggested default pane width, px. */
  defaultWidth: number
  /** Pixels per millisecond of the active scale — for the drag hook. */
  pxPerMs: number
  /** Hard domain bounds (epoch ms) — drag clamp range. */
  domainMin: number
  domainMax: number
}
