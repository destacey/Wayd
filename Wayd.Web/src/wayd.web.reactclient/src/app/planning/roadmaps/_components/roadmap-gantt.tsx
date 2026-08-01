'use client'

// roadmap-gantt.tsx — the Gantt "chart pane" that attaches to the right of the
// roadmap List grid (WaydGrid's rightPane slot). WaydGrid owns the rows + row
// geometry; this only draws, per row, a bar/diamond positioned on a shared time
// axis. Parent (container) rows get a rolled-up summary bar spanning their
// children (see core/rollup). Domain-agnostic grid + roadmap-specific bars.

import { useMemo } from 'react'
import type { Row } from '@tanstack/react-table'
import dayjs from 'dayjs'
import { createTimeScale } from '@/src/components/common/timeline/core/scale'
import { rollupSummaries } from '@/src/components/common/timeline/core/rollup'
import { contrastText } from '@/src/components/common/timeline/core/color'
import type {
  BarDragState,
  DragMode,
} from '@/src/components/common/timeline'
import type { RoadmapItemTreeNode } from './roadmap-items-grid'
import styles from './roadmap-gantt.module.css'

/** Minimal item shape the shared drag hook (useBarDrag) needs. */
export interface GanttDragItem {
  id: string
  start: number
  end: number
  kind: 'range'
}

const DAY_MS = 86_400_000
// Default pixel width of one day on the axis (zoom level). Long roadmaps scroll
// horizontally rather than crushing bars; zoom in/out adjusts this.
export const DEFAULT_PX_PER_DAY = 6
// Zoom clamps: fit ~years across at the low end, day-level detail at the high end.
export const MIN_PX_PER_DAY = 1
export const MAX_PX_PER_DAY = 40
// Multiplier per zoom step (matches the timeline's ZOOM_STEP).
export const ZOOM_STEP = 1.2
const AXIS_HEIGHT = 48
// Padding around the domain so the first/last bars aren't flush to the edges.
const DOMAIN_PAD_DAYS = 14

const toMs = (d: Date | string | null | undefined): number | undefined => {
  if (d == null) return undefined
  const v = dayjs(d).valueOf()
  return Number.isFinite(v) ? v : undefined
}

const fmtDay = (ms: number) => dayjs(ms).format('MMM D, YYYY')

/** The live-drag label + which edge to anchor it to, per drag mode. */
function dragLabelFor(
  mode: DragMode,
  start: number,
  end: number,
): { text: string; anchor: 'start' | 'end' | 'center' } {
  if (mode === 'resize-start') return { text: fmtDay(start), anchor: 'start' }
  if (mode === 'resize-end') return { text: fmtDay(end), anchor: 'end' }
  return { text: `${fmtDay(start)} – ${fmtDay(end)}`, anchor: 'center' }
}

/** The [start, end] a row occupies: a range for activity/timebox, the instant
 *  for a milestone, or undefined when it has no dates. */
function nodeRange(node: RoadmapItemTreeNode): [number, number] | undefined {
  if (node.type === 'Milestone') {
    const d = toMs(node.date)
    return d != null ? [d, d] : undefined
  }
  const s = toMs(node.start)
  const e = toMs(node.end)
  return s != null && e != null ? [s, e] : undefined
}

/** Accessors for the rollup over the roadmap tree. */
const rollupAccessors = {
  id: (n: RoadmapItemTreeNode) => n.id,
  children: (n: RoadmapItemTreeNode) => n.children,
  start: (n: RoadmapItemTreeNode) => nodeRange(n)?.[0],
  end: (n: RoadmapItemTreeNode) => nodeRange(n)?.[1],
}

/**
 * The chart's time domain (epoch ms): the roadmap window, padded, and widened to
 * include any item that extends beyond it (so no bar is clipped off the axis).
 * Exported so the drag hook can be built from the same domain the chart uses.
 */
export function computeGanttDomain(
  roadmapStart: Date | string,
  roadmapEnd: Date | string,
  treeData: RoadmapItemTreeNode[],
): { domainStart: number; domainEnd: number } {
  let min = toMs(roadmapStart) ?? Date.parse('2020-01-01')
  let max = toMs(roadmapEnd) ?? min + 365 * DAY_MS
  const walk = (nodes: RoadmapItemTreeNode[]) => {
    for (const n of nodes) {
      const r = nodeRange(n)
      if (r) {
        min = Math.min(min, r[0])
        max = Math.max(max, r[1])
      }
      if (n.children?.length) walk(n.children)
    }
  }
  walk(treeData)
  return {
    domainStart: min - DOMAIN_PAD_DAYS * DAY_MS,
    domainEnd: max + DOMAIN_PAD_DAYS * DAY_MS,
  }
}

/** Pixels-per-day → pixels-per-millisecond, for the drag hook's scale. */
export const pxPerMsFor = (pxPerDay: number) => pxPerDay / DAY_MS

export interface RoadmapGanttOptions {
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
   * start. Lets the live date label on a MOVE track the cursor rather than
   * centering on the whole bar. Undefined = center fallback.
   */
  moveGrabOffset?: number
}

export interface RoadmapGanttModel {
  /** Header (date axis) for WaydGrid's rightPane.header. */
  header: React.ReactNode
  /** Per-row bar renderer for WaydGrid's rightPane.renderRow. */
  renderRow: (ctx: {
    row: Row<RoadmapItemTreeNode>
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

/**
 * Build the axis + per-row bar renderers for the roadmap Gantt pane from the
 * roadmap window and the grid's tree data. Returned as a hook so the scale is
 * memoized against the inputs. Drag/resize BEHAVIOR is delegated to the shared
 * timeline interaction core (via the consumer's useBarDrag); this only renders
 * the handles and the in-progress draft.
 */
export function useRoadmapGantt(
  roadmapStart: Date | string,
  roadmapEnd: Date | string,
  treeData: RoadmapItemTreeNode[],
  options: RoadmapGanttOptions = {},
): RoadmapGanttModel {
  const {
    pxPerDay = DEFAULT_PX_PER_DAY,
    editable = false,
    activeDrag = null,
    onBarPointerDown,
    moveGrabOffset,
  } = options
  return useMemo(() => {
    const { domainStart, domainEnd } = computeGanttDomain(
      roadmapStart,
      roadmapEnd,
      treeData,
    )
    const days = Math.max(1, Math.ceil((domainEnd - domainStart) / DAY_MS))
    const width = days * pxPerDay
    const scale = createTimeScale(domainStart, domainEnd, width)

    // Summary spans for parent rows (bars derived from descendants).
    const summaries = rollupSummaries(treeData, rollupAccessors)

    const { upper, lower } = scale.tiers()

    // Vertical gridlines at the lower-tier tick boundaries (same positions as the
    // axis ticks — month when zoomed out, week/day when zoomed in), matching the
    // timeline's gridline approach. Rendered full-height behind the bars.
    const gridlineMs = lower.map((s) => s.startMs)
    const renderBackground = ({ totalHeight }: { totalHeight: number }) => (
      <>
        {gridlineMs.map((ms) => (
          <div
            key={`gl-${ms}`}
            className={styles.gridline}
            style={{ left: scale.toX(ms), height: totalHeight }}
          />
        ))}
      </>
    )

    const header = (
      <div className={styles.axis} style={{ width, height: AXIS_HEIGHT }}>
        <div className={styles.axisTier} style={{ top: 0, height: AXIS_HEIGHT / 2 }}>
          {upper.map((seg) => {
            const left = scale.toX(seg.startMs)
            return (
              <div
                key={`u-${seg.startMs}`}
                className={styles.axisCell}
                style={{ left, width: scale.toX(seg.endMs) - left }}
                title={seg.label}
              >
                <span className={styles.axisLabel}>{seg.label}</span>
              </div>
            )
          })}
        </div>
        <div
          className={styles.axisTier}
          style={{ top: AXIS_HEIGHT / 2, height: AXIS_HEIGHT / 2 }}
        >
          {lower.map((seg) => {
            const left = scale.toX(seg.startMs)
            return (
              <div
                key={`l-${seg.startMs}`}
                className={styles.axisCell}
                style={{ left, width: scale.toX(seg.endMs) - left }}
                title={seg.label}
              >
                <span className={styles.axisLabel}>{seg.label}</span>
              </div>
            )
          })}
        </div>
      </div>
    )

    const renderRow = ({
      row,
      top,
      height,
    }: {
      row: Row<RoadmapItemTreeNode>
      top: number
      height: number
    }) => {
      const node = row.original
      const barH = Math.max(8, height - 10)
      // Bars are absolutely placed in the canvas: `top` is the row's offset,
      // centered vertically within the row height.
      const barTop = top + (height - barH) / 2
      const color = node.color ?? undefined

      // Milestone — a diamond at its instant.
      if (node.type === 'Milestone') {
        const d = toMs(node.date)
        if (d == null) return null
        const size = Math.min(barH, 14)
        return (
          <div
            className={styles.milestone}
            style={{
              left: scale.toX(d) - size / 2,
              top: top + (height - size) / 2,
              width: size,
              height: size,
              ...(color ? { backgroundColor: color } : {}),
            }}
            title={`${node.name} · ${dayjs(d).format('MMM D, YYYY')}`}
          />
        )
      }

      // Parent with no own range but descendants → summary bar.
      const own = nodeRange(node)
      const summary = summaries.get(node.id)
      if (!own && summary) {
        const left = scale.toX(summary.start)
        const w = Math.max(2, scale.toX(summary.end) - left)
        return (
          <div
            className={styles.summaryBar}
            style={{ left, top: barTop, width: w, height: barH }}
            title={`${node.name} · ${dayjs(summary.start).format('MMM D')} – ${dayjs(
              summary.end,
            ).format('MMM D, YYYY')}`}
          />
        )
      }

      // Leaf / activity / timebox with its own range → a bar.
      if (own) {
        // While this bar is being dragged, render at the live draft bounds.
        const dragging = activeDrag?.id === node.id
        const s = dragging ? activeDrag!.draft.start : own[0]
        const e = dragging ? activeDrag!.draft.end : own[1]
        const left = scale.toX(s)
        const w = Math.max(2, scale.toX(e) - left)
        const isTimebox = node.type === 'Timebox'
        // Derive readable text color from the BAR's fill (not the page theme) so
        // a light bar (e.g. yellow) gets dark text in any theme — same contrast
        // logic the timeline uses. Timeboxes keep the theme's secondary text.
        const useCustomBg = !!color && !isTimebox
        // Draggable range bars carry a stable item shape for the shared hook.
        const dragItem: GanttDragItem = {
          id: node.id,
          start: own[0],
          end: own[1],
          kind: 'range',
        }
        const barEditable = editable && !!onBarPointerDown

        // Live date indicator shown while THIS bar is being dragged/resized, so
        // the user sees where the endpoint(s) will land.
        let dragLabel: React.ReactNode = null
        if (dragging) {
          const { text, anchor } = dragLabelFor(activeDrag!.mode, s, e)
          // On MOVE, anchor the label to the CURSOR (bar-left + captured grab
          // offset), clamped within the bar; on resize, to the dragged edge.
          const cursorX = left + Math.min(Math.max(moveGrabOffset ?? w / 2, 0), w)
          const anchorX =
            anchor === 'start' ? left : anchor === 'end' ? left + w : cursorX
          const xShift =
            anchor === 'center' ? '-50%' : anchor === 'end' ? '-100%' : '0'
          // Flip the label BELOW the bar when there isn't room above it (top
          // rows), so it's never clipped by the canvas top. ~24px = label height.
          const below = barTop < 24
          dragLabel = (
            <div
              className={styles.dragLabel}
              style={{
                left: anchorX,
                top: below ? barTop + barH + 4 : barTop - 4,
                transform: `translate(${xShift}, ${below ? '0' : '-100%'})`,
              }}
            >
              {text}
            </div>
          )
        }

        return (
          <>
          <div
            className={`${styles.bar} ${isTimebox ? styles.timeboxBar : ''} ${
              barEditable ? styles.barEditable : ''
            }`}
            style={{
              left,
              top: barTop,
              width: w,
              height: barH,
              ...(useCustomBg
                ? { backgroundColor: color, color: contrastText(color) }
                : {}),
            }}
            title={`${node.name} · ${dayjs(s).format('MMM D')} – ${dayjs(e).format(
              'MMM D, YYYY',
            )}`}
            onPointerDown={
              barEditable
                ? (ev) => onBarPointerDown!(ev, dragItem, 'move')
                : undefined
            }
          >
            <span className={styles.barLabel}>{node.name}</span>
            {barEditable && (
              <>
                <span
                  className={`${styles.handle} ${styles.handleStart}`}
                  onPointerDown={(ev) =>
                    onBarPointerDown!(ev, dragItem, 'resize-start')
                  }
                />
                <span
                  className={`${styles.handle} ${styles.handleEnd}`}
                  onPointerDown={(ev) =>
                    onBarPointerDown!(ev, dragItem, 'resize-end')
                  }
                />
              </>
            )}
          </div>
          {dragLabel}
          </>
        )
      }

      return null
    }

    return {
      header,
      renderRow,
      renderBackground,
      defaultWidth: 520,
      pxPerMs: scale.pxPerMs,
      domainMin: domainStart,
      domainMax: domainEnd,
    }
  }, [
    roadmapStart,
    roadmapEnd,
    treeData,
    pxPerDay,
    editable,
    activeDrag,
    onBarPointerDown,
    moveGrabOffset,
  ])
}
