'use client'

// timeline2/render/chart-canvas.tsx — the variant-agnostic chart body.
// Consumes ResolvedRow[] (from any layout strategy) + a TimeScale and draws
// backgrounds, rows, and item bars. Variant never reaches here. Owns the drag
// interaction (move/resize via useBarDrag; progress via a pointer handler),
// applying the live draft to the dragged bar and committing on release.

import { FC, useCallback, useRef, useState } from 'react'
import type { TimeScale } from '../core/scale'
import type { ResolvedRow, TimelineItem } from '../core/types'
import { itemBox, backgroundBox, type GeometryConfig } from '../core/geometry'
import { progressFromX } from '../core/interaction'
import { ItemBar } from './item-bar'
import { useBarDrag } from './use-bar-drag'
import { suppressNextClick } from './suppress-next-click'
import type {
  ItemRenderProps,
  ItemDateChange,
  ItemProgressChange,
} from '../types'
import styles from './timeline.module.css'

export interface ChartCanvasProps {
  /** Visible (virtualized) rows to render. */
  rows: ResolvedRow[]
  /** All rows — used to resolve row-scoped background geometry even when the
   *  owning row is scrolled out of view. Defaults to `rows`. */
  allRows?: ResolvedRow[]
  /** Absolute index of the first visible row (for stable stripe alternation). */
  rowIndexOffset?: number
  totalHeight: number
  scale: TimeScale
  geometry: GeometryConfig
  /** Chart-wide background regions (span full height). */
  chartBackgrounds?: TimelineItem[]
  selectedId?: string
  editable?: boolean
  /** Current horizontal scroll offset (px) — bar labels stick to this edge. */
  viewportLeft?: number
  itemRenderer?: FC<ItemRenderProps>
  onItemClick?: (item: TimelineItem) => void
  onItemDateChange?: (change: ItemDateChange) => void
  onItemProgressChange?: (change: ItemProgressChange) => void
  showCurrentTime?: boolean
  nowMs?: number
  /** When true, whitespace shows the grab cursor (content overflows → pannable). */
  canPan?: boolean
  /** Hard bounds for dragging items (epoch ms). Defaults to the full scale
   *  domain when omitted. */
  dragMin?: number
  dragMax?: number
  /** When false, vertical gridlines are hidden. Default true. */
  showGridlines?: boolean
  /** When true, Saturday and Sunday columns are shaded. Default false. */
  showWeekends?: boolean
}

export const ChartCanvas: FC<ChartCanvasProps> = ({
  rows,
  allRows,
  rowIndexOffset = 0,
  totalHeight,
  scale,
  geometry,
  chartBackgrounds,
  selectedId,
  editable,
  viewportLeft = 0,
  itemRenderer,
  onItemClick,
  onItemDateChange,
  onItemProgressChange,
  showCurrentTime,
  nowMs,
  canPan,
  dragMin,
  dragMax,
  showGridlines = true,
  showWeekends = false,
}) => {
  // Use the lower-tier axis segments as gridline positions so lines align with
  // every visible axis tick (month when zoomed out, week/day when zoomed in).
  const tickLines = scale.tiers().lower.map((s) => s.startMs)
  const weekendBoxes = showWeekends ? scale.weekends() : []
  // `Date.now()` is impure; read it once on mount (a current-time line that's
  // accurate to mount time is fine — re-render with a fresh `nowMs` prop to move
  // it). Lazy initializer keeps render pure.
  const [mountNow] = useState(() => Date.now())
  const now = nowMs ?? mountNow
  const nowVisible =
    showCurrentTime && now >= scale.domain[0] && now <= scale.domain[1]

  // Map groupId -> row, so row-scoped backgrounds can find their row geometry.
  // Use the full row set (not just the visible slice) so a timebox whose owning
  // row is scrolled out of view still resolves to correct geometry.
  const rowsForLookup = allRows ?? rows
  const rowsByGroupId = new Map(
    rowsForLookup.filter((r) => r.groupId).map((r) => [r.groupId as string, r]),
  )

  // Move / resize drag (date change). Bounds are the EDITABLE window (dragMin/Max),
  // not the rendered domain — the domain may be wider to fit out-of-window items.
  const { active, start: startDrag } = useBarDrag({
    pxPerMs: scale.pxPerMs,
    min: dragMin ?? scale.domain[0],
    max: dragMax ?? scale.domain[1],
    snap: true,
    onCommit: (change) => onItemDateChange?.(change),
  })

  // Progress drag — lighter weight, tracked on the canvas element. The pointer
  // listeners are stored on the session ref so they can reference each other
  // for teardown without a declaration cycle.
  const canvasRef = useRef<HTMLDivElement>(null)
  const [progressDraft, setProgressDraft] = useState<{
    id: string
    progress: number
  } | null>(null)
  const progressSession = useRef<{
    item: TimelineItem
    box: { left: number; width: number }
    moved: boolean
    move: (e: PointerEvent) => void
    up: () => void
  } | null>(null)

  const beginProgressDrag = useCallback(
    (item: TimelineItem, box: { left: number; width: number }) => {
      const move = (e: PointerEvent) => {
        const canvas = canvasRef.current
        const s = progressSession.current
        if (!canvas || !s) return
        s.moved = true
        const x = e.clientX - canvas.getBoundingClientRect().left
        setProgressDraft({ id: item.id, progress: progressFromX(x, box.left, box.width) })
      }
      const up = () => {
        const s = progressSession.current
        window.removeEventListener('pointermove', s!.move)
        window.removeEventListener('pointerup', s!.up)
        // Dragging the progress handle ends with a synthetic click — swallow it
        // so it doesn't open the drawer.
        if (s?.moved) suppressNextClick()
        progressSession.current = null
        setProgressDraft((draft) => {
          if (draft && draft.progress !== (item.progress ?? 0)) {
            onItemProgressChange?.({ id: item.id, progress: draft.progress })
          }
          return null
        })
      }
      progressSession.current = { item, box, moved: false, move, up }
      setProgressDraft({ id: item.id, progress: item.progress ?? 0 })
      window.addEventListener('pointermove', move)
      window.addEventListener('pointerup', up)
    },
    [onItemProgressChange],
  )

  return (
    <div
      ref={canvasRef}
      className={`${styles.canvas} ${canPan ? styles.canvasPannable : ''}`}
      style={{ width: scale.width, height: totalHeight }}
    >
      {/* Weekend shading — behind gridlines and rows */}
      {weekendBoxes.map((box, i) => (
        <div
          key={`we-${i}`}
          className={styles.weekend}
          style={{ left: box.left, width: box.width }}
        />
      ))}

      {/* Vertical gridlines */}
      {showGridlines && tickLines.map((ms) => (
        <div key={`gl-${ms}`} className={styles.gridline} style={{ left: scale.toX(ms) }} />
      ))}

      {/* Row stripes — alternation keyed off the ABSOLUTE row index so it stays
          stable as rows scroll in/out of the virtualized window. */}
      {rows.map((row, i) => {
        const rowIndex = rowIndexOffset + i
        return (
          <div
            key={row.rowKey ?? row.groupId ?? `row-${rowIndex}`}
            className={`${styles.row} ${rowIndex % 2 === 1 ? styles.rowAlt : ''}`}
            style={{ top: row.top, height: row.height }}
          />
        )
      })}

      {/* Backgrounds (timeboxes): drawn AFTER row stripes so they're visible,
          BEFORE bars so items sit on top. A row-scoped background (groupId
          matching a row) sits behind that row; a root/ungrouped one spans the
          full chart height. Labeled like the legacy timeline. */}
      {chartBackgrounds?.map((bg) => {
        const row = bg.groupId ? rowsByGroupId.get(bg.groupId) : undefined
        const box = backgroundBox(bg, row ?? null, scale, totalHeight)
        return (
          <div
            key={`bg-${bg.id}`}
            className={styles.background}
            style={{
              left: box.left,
              top: box.top,
              width: box.width,
              height: box.height,
              ...(bg.color ? { backgroundColor: bg.color } : {}),
            }}
            title={bg.label}
          >
            {bg.label && <span className={styles.backgroundLabel}>{bg.label}</span>}
          </div>
        )
      })}

      {/* Item bars */}
      {rows.flatMap((row) =>
        row.items.map(({ item, lane }) => {
          // While dragging this item, render the live draft bounds instead.
          const dragged =
            active?.id === item.id
              ? { ...item, start: active.draft.start, end: active.draft.end }
              : item
          const withProgress =
            progressDraft?.id === item.id
              ? { ...dragged, progress: progressDraft.progress }
              : dragged
          const box = itemBox(withProgress, lane, row, scale, geometry)
          // Push the label right so it stays visible when the bar starts before
          // the viewport's left edge (clamped so it never leaves the bar).
          const labelOffset = Math.max(0, Math.min(viewportLeft - box.left, box.width))
          return (
            <ItemBar
              key={item.id}
              box={box}
              labelOffset={labelOffset}
              selected={item.id === selectedId}
              editable={editable}
              itemRenderer={itemRenderer}
              onClick={onItemClick}
              onDragStart={startDrag}
              onProgressDragStart={(e, dragItem) => {
                e.preventDefault()
                e.stopPropagation()
                beginProgressDrag(dragItem, { left: box.left, width: box.width })
              }}
            />
          )
        }),
      )}

      {/* Current-time line */}
      {nowVisible && (
        <div className={styles.nowLine} style={{ left: scale.toX(now) }} />
      )}
    </div>
  )
}
