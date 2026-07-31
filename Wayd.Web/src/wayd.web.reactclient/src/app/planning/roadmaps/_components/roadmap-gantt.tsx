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
import type { RoadmapItemTreeNode } from './roadmap-items-grid'
import styles from './roadmap-gantt.module.css'

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
}

/**
 * Build the axis + per-row bar renderers for the roadmap Gantt pane from the
 * roadmap window and the grid's tree data. Returned as a hook so the scale is
 * memoized against the inputs.
 */
export function useRoadmapGantt(
  roadmapStart: Date | string,
  roadmapEnd: Date | string,
  treeData: RoadmapItemTreeNode[],
  pxPerDay: number = DEFAULT_PX_PER_DAY,
): RoadmapGanttModel {
  return useMemo(() => {
    // Domain = roadmap window, padded, and widened to include any item that
    // extends beyond it (so no bar is clipped off the axis).
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

    const domainStart = min - DOMAIN_PAD_DAYS * DAY_MS
    const domainEnd = max + DOMAIN_PAD_DAYS * DAY_MS
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
        const [s, e] = own
        const left = scale.toX(s)
        const w = Math.max(2, scale.toX(e) - left)
        const isTimebox = node.type === 'Timebox'
        // Derive readable text color from the BAR's fill (not the page theme) so
        // a light bar (e.g. yellow) gets dark text in any theme — same contrast
        // logic the timeline uses. Timeboxes keep the theme's secondary text.
        const useCustomBg = !!color && !isTimebox
        return (
          <div
            className={`${styles.bar} ${isTimebox ? styles.timeboxBar : ''}`}
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
          >
            <span className={styles.barLabel}>{node.name}</span>
          </div>
        )
      }

      return null
    }

    return { header, renderRow, renderBackground, defaultWidth: 520 }
  }, [roadmapStart, roadmapEnd, treeData, pxPerDay])
}
