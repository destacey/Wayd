'use client'

// use-gantt-pane.tsx — the generic Gantt "chart pane" that attaches to the right
// of a WaydGrid (its rightPane slot). WaydGrid owns the rows + row geometry;
// this only draws, per row, a bar/diamond positioned on a shared time axis.
// Parent (container) rows get a rolled-up summary bar spanning their children
// (see core/rollup).
//
// Domain-agnostic: the consumer supplies accessors (see ./types) describing its
// own node shape, so the roadmap, the project plan, and anything after them
// share one engine rather than one copy each.

import { useMemo } from 'react'
import dayjs from 'dayjs'
import { createTimeScale } from '../core/scale'
import { rollupSummaries } from '../core/rollup'
import { contrastText } from '../core/color'
import { dragLabel, formatDragDay } from '../core/drag-label'
import type {
  GanttAccessors,
  GanttDragItem,
  GanttPaneModel,
  GanttPaneOptions,
} from './types'
import styles from './gantt.module.css'

const DAY_MS = 86_400_000
// Default pixel width of one day on the axis (zoom level). Long plans scroll
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
// Fallback window when a tree has no dated rows and no hint, so the axis still
// renders instead of collapsing to zero width. Anchored a month before today
// rather than a fixed date: with the chart on by default, an empty plan would
// otherwise open on a calendar window years away from the user.
const FALLBACK_LEAD_DAYS = 30

export const toMs = (
  d: Date | string | null | undefined,
): number | undefined => {
  if (d == null) return undefined
  const v = dayjs(d).valueOf()
  return Number.isFinite(v) ? v : undefined
}

/** Pixels-per-day → pixels-per-millisecond, for the drag hook's scale. */
export const pxPerMsFor = (pxPerDay: number) => pxPerDay / DAY_MS

/**
 * The chart's time domain (epoch ms): every dated row in the tree, plus any
 * `domainHint` bound (a roadmap window, a project's dates), padded so nothing
 * sits flush against the edge.
 *
 * Exported so the consumer can build its drag hook from the SAME domain the
 * chart uses — the clamp range and the axis must not diverge.
 */
export function computeGanttDomain<T>(
  treeData: T[],
  accessors: GanttAccessors<T>,
  domainHint?: [number | undefined, number | undefined],
  /** "Now", for the empty-tree fallback window. Injectable so tests are stable. */
  now: number = Date.now(),
): { domainStart: number; domainEnd: number } {
  let min: number | undefined = domainHint?.[0]
  let max: number | undefined = domainHint?.[1]

  const walk = (nodes: T[]) => {
    for (const n of nodes) {
      const r = accessors.range(n)
      if (r) {
        min = min == null ? r[0] : Math.min(min, r[0])
        max = max == null ? r[1] : Math.max(max, r[1])
      }
      const kids = accessors.children(n)
      if (kids?.length) walk(kids)
    }
  }
  walk(treeData)

  // Nothing dated anywhere — fall back to a one-year window around today.
  const start = min ?? now - FALLBACK_LEAD_DAYS * DAY_MS
  const end = max ?? start + 365 * DAY_MS
  return {
    domainStart: start - DOMAIN_PAD_DAYS * DAY_MS,
    domainEnd: end + DOMAIN_PAD_DAYS * DAY_MS,
  }
}

/**
 * Build the axis + per-row bar renderers for a Gantt pane from a grid's tree
 * data. Returned as a hook so the scale is memoized against the inputs.
 *
 * Drag/resize BEHAVIOR is delegated to the shared timeline interaction core
 * (via the consumer's useBarDrag); this only renders the handles and the
 * in-progress draft.
 */
export function useGanttPane<T>(
  treeData: T[],
  accessors: GanttAccessors<T>,
  options: GanttPaneOptions<T> = {},
): GanttPaneModel<T> {
  const {
    pxPerDay = DEFAULT_PX_PER_DAY,
    editable = false,
    activeDrag = null,
    onBarPointerDown,
    moveGrabOffset,
    domainHint,
    defaultWidth = 520,
  } = options

  // Accessors are typically defined inline by the consumer, so a new object
  // identity every render would defeat the memo. Depend on the pieces the build
  // actually reads instead of the container object.
  const {
    id: getId,
    children: getChildren,
    name: getName,
    kind: getKind,
    range: getRange,
    color: getColor,
    progress: getProgress,
    editable: getEditable,
    variant: getVariant,
  } = accessors

  const hintStart = domainHint?.[0]
  const hintEnd = domainHint?.[1]

  return useMemo(() => {
    const acc: GanttAccessors<T> = {
      id: getId,
      children: getChildren,
      name: getName,
      kind: getKind,
      range: getRange,
      color: getColor,
      progress: getProgress,
      editable: getEditable,
      variant: getVariant,
    }

    const { domainStart, domainEnd } = computeGanttDomain(treeData, acc, [
      hintStart,
      hintEnd,
    ])
    const days = Math.max(1, Math.ceil((domainEnd - domainStart) / DAY_MS))
    const width = days * pxPerDay
    const scale = createTimeScale(domainStart, domainEnd, width)

    // Summary spans for parent rows (bars derived from descendants).
    const summaries = rollupSummaries(treeData, {
      id: getId,
      children: getChildren,
      start: (n: T) => getRange(n)?.[0],
      end: (n: T) => getRange(n)?.[1],
      ...(getProgress ? { progress: getProgress } : {}),
    })

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

    const renderTier = (
      segs: typeof upper,
      prefix: string,
      top: number,
    ) => (
      <div className={styles.axisTier} style={{ top, height: AXIS_HEIGHT / 2 }}>
        {segs.map((seg) => {
          const left = scale.toX(seg.startMs)
          return (
            <div
              key={`${prefix}-${seg.startMs}`}
              className={styles.axisCell}
              style={{ left, width: scale.toX(seg.endMs) - left }}
              title={seg.label}
            >
              <span className={styles.axisLabel}>{seg.label}</span>
            </div>
          )
        })}
      </div>
    )

    const header = (
      <div className={styles.axis} style={{ width, height: AXIS_HEIGHT }}>
        {renderTier(upper, 'u', 0)}
        {renderTier(lower, 'l', AXIS_HEIGHT / 2)}
      </div>
    )

    const renderRow = ({
      row,
      top,
      height,
    }: {
      row: { original: T }
      top: number
      height: number
    }) => {
      const node = row.original
      const barH = Math.max(8, height - 10)
      // Bars are absolutely placed in the canvas: `top` is the row's offset,
      // centered vertically within the row height.
      const barTop = top + (height - barH) / 2
      const color = getColor?.(node) ?? undefined
      const name = getName(node)
      const own = getRange(node)

      // Milestone — a diamond at its instant.
      if (getKind?.(node) === 'milestone') {
        if (own == null) return null
        // While dragging, render at the live draft instant. A milestone has no
        // width, so move is the only mode — both bounds travel together.
        const dragging = activeDrag?.id === getId(node)
        const d = dragging ? activeDrag!.draft.start : own[0]
        const size = Math.min(barH, 14)
        const complete = (getProgress?.(node) ?? 0) >= 100
        const msEditable =
          editable && !!onBarPointerDown && (getEditable?.(node) ?? true)
        const left = scale.toX(d) - size / 2
        const msTop = top + (height - size) / 2
        const dragItem: GanttDragItem = {
          id: getId(node),
          start: own[0],
          end: own[1],
          kind: 'range',
        }
        return (
          <>
            <div
              className={`${styles.milestone} ${
                complete ? styles.milestoneComplete : ''
              } ${msEditable ? styles.milestoneEditable : ''}`}
              style={{
                left,
                top: msTop,
                width: size,
                height: size,
                ...(color ? { backgroundColor: color } : {}),
              }}
              title={`${name} · ${dayjs(d).format('MMM D, YYYY')}`}
              onPointerDown={
                msEditable
                  ? (ev) => onBarPointerDown!(ev, dragItem, 'move')
                  : undefined
              }
            />
            {dragging && (
              <div
                className={styles.dragLabel}
                style={{
                  left: left + size / 2,
                  top: msTop < 24 ? msTop + size + 4 : msTop - 4,
                  transform: `translate(-50%, ${msTop < 24 ? '0' : '-100%'})`,
                }}
              >
                {formatDragDay(d)}
              </div>
            )}
          </>
        )
      }

      // Parent with no own range but descendants → summary bar.
      const summary = summaries.get(getId(node))
      if (!own && summary) {
        const left = scale.toX(summary.start)
        const w = Math.max(2, scale.toX(summary.end) - left)
        const pct =
          summary.progress == null
            ? undefined
            : Math.min(100, Math.max(0, summary.progress))
        return (
          <div
            className={styles.summaryBar}
            style={{ left, top: barTop, width: w, height: barH }}
            title={`${name} · ${dayjs(summary.start).format('MMM D')} – ${dayjs(
              summary.end,
            ).format('MMM D, YYYY')}${pct == null ? '' : ` · ${Math.round(pct)}%`}`}
          >
            {pct != null && pct > 0 && (
              <div
                className={styles.summaryProgressFill}
                style={{ width: `${pct}%` }}
              />
            )}
          </div>
        )
      }

      // Leaf with its own range → a bar.
      if (own) {
        // While this bar is being dragged, render at the live draft bounds.
        const dragging = activeDrag?.id === getId(node)
        const s = dragging ? activeDrag!.draft.start : own[0]
        const e = dragging ? activeDrag!.draft.end : own[1]
        const left = scale.toX(s)
        const w = Math.max(2, scale.toX(e) - left)
        const muted = getVariant?.(node) === 'muted'
        // Derive readable text color from the BAR's fill (not the page theme) so
        // a light bar (e.g. yellow) gets dark text in any theme — same contrast
        // logic the timeline uses. Muted bars keep the theme's secondary text.
        const useCustomBg = !!color && !muted
        // Draggable range bars carry a stable item shape for the shared hook.
        const dragItem: GanttDragItem = {
          id: getId(node),
          start: own[0],
          end: own[1],
          kind: 'range',
        }
        const barEditable =
          editable && !!onBarPointerDown && (getEditable?.(node) ?? true)
        const pct = getProgress?.(node)
        const progressPct =
          pct == null ? undefined : Math.min(100, Math.max(0, pct))

        // Live date indicator shown while THIS bar is being dragged/resized, so
        // the user sees where the endpoint(s) will land.
        let dragLabelNode: React.ReactNode = null
        if (dragging) {
          const { text, anchor } = dragLabel(activeDrag!.mode, s, e)
          // On MOVE, anchor the label to the CURSOR (bar-left + captured grab
          // offset), clamped within the bar; on resize, to the dragged edge.
          const cursorX = left + Math.min(Math.max(moveGrabOffset ?? w / 2, 0), w)
          const anchorX =
            anchor === 'start' ? left : anchor === 'end' ? left + w : cursorX
          const xShift =
            anchor === 'cursor' ? '-50%' : anchor === 'end' ? '-100%' : '0'
          // Flip the label BELOW the bar when there isn't room above it (top
          // rows), so it's never clipped by the canvas top. ~24px = label height.
          const below = barTop < 24
          dragLabelNode = (
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
              className={`${styles.bar} ${muted ? styles.mutedBar : ''} ${
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
              title={`${name} · ${dayjs(s).format('MMM D')} – ${dayjs(e).format(
                'MMM D, YYYY',
              )}${progressPct == null ? '' : ` · ${Math.round(progressPct)}%`}`}
              onPointerDown={
                barEditable
                  ? (ev) => onBarPointerDown!(ev, dragItem, 'move')
                  : undefined
              }
            >
              {progressPct != null && progressPct > 0 && (
                <div
                  className={styles.progressFill}
                  style={{ width: `${progressPct}%` }}
                />
              )}
              <span className={styles.barLabel}>{name}</span>
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
            {dragLabelNode}
          </>
        )
      }

      return null
    }

    return {
      header,
      renderRow,
      renderBackground,
      defaultWidth,
      pxPerMs: scale.pxPerMs,
      domainMin: domainStart,
      domainMax: domainEnd,
    }
  }, [
    treeData,
    getId,
    getChildren,
    getName,
    getKind,
    getRange,
    getColor,
    getProgress,
    getEditable,
    getVariant,
    hintStart,
    hintEnd,
    pxPerDay,
    editable,
    activeDrag,
    onBarPointerDown,
    moveGrabOffset,
    defaultWidth,
  ])
}
