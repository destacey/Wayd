'use client'

// timeline2/render/use-bar-drag.ts
// Pointer-event plumbing for bar move / endpoint-resize. Tracks a live draft
// (so the bar follows the pointer) and commits on release via onCommit. The
// date math itself lives in core/interaction.ts (pure, tested) — this hook only
// captures pointer geometry and drives it.

import { useCallback, useRef, useState } from 'react'
import { applyDrag, type DragMode, type DragResult } from '../core/interaction'
import { suppressNextClick } from './suppress-next-click'
import type { TimelineItem } from '../core/types'

export interface BarDragState {
  id: string
  mode: DragMode
  /** Live, in-progress bounds to render while dragging. */
  draft: DragResult
}

export interface UseBarDragOptions {
  pxPerMs: number
  min?: number
  max?: number
  snap?: boolean
  /** Called once on pointer-up with the final bounds (consumer persists). */
  onCommit: (change: { id: string; start: number; end: number }) => void
}

export interface UseBarDrag {
  /** Current drag, or null when idle. */
  active: BarDragState | null
  /** Begin a drag for `item` in `mode` from a pointer-down event. */
  start: (
    e: React.PointerEvent,
    item: TimelineItem,
    mode: DragMode,
  ) => void
}

export function useBarDrag(options: UseBarDragOptions): UseBarDrag {
  const { pxPerMs, min, max, snap, onCommit } = options
  const [active, setActive] = useState<BarDragState | null>(null)

  // Mutable drag session kept in a ref so the window listeners see fresh values
  // without re-subscribing each render.
  const session = useRef<{
    item: TimelineItem
    mode: DragMode
    startX: number
    moved: boolean
    latest: DragResult
    move: (e: PointerEvent) => void
    up: () => void
  } | null>(null)

  // Pixels the pointer must travel before it counts as a drag (vs. a click).
  const DRAG_THRESHOLD = 3

  // The session owns its own listeners so teardown can reference them without a
  // declaration cycle (React Compiler purity rule).
  const start = useCallback(
    (e: React.PointerEvent, item: TimelineItem, mode: DragMode) => {
      // Don't let the drag also trigger selection or text selection.
      e.preventDefault()
      e.stopPropagation()

      const startX = e.clientX

      const move = (ev: PointerEvent) => {
        const s = session.current
        if (!s) return
        if (!s.moved && Math.abs(ev.clientX - startX) >= DRAG_THRESHOLD) {
          s.moved = true
        }
        const draft = applyDrag({
          mode,
          item,
          pxPerMs,
          deltaPx: ev.clientX - startX,
          min,
          max,
          snap,
        })
        s.latest = draft
        setActive({ id: item.id, mode, draft })
      }

      const up = () => {
        const s = session.current
        window.removeEventListener('pointermove', move)
        window.removeEventListener('pointerup', up)
        session.current = null
        setActive(null)
        if (!s) return
        // If the pointer actually moved, this was a drag — suppress the click
        // that would otherwise open the item drawer.
        if (s.moved) suppressNextClick()
        // Only commit if something actually changed.
        if (s.latest.start !== item.start || s.latest.end !== item.end) {
          onCommit({ id: item.id, start: s.latest.start, end: s.latest.end })
        }
      }

      session.current = {
        item,
        mode,
        startX,
        moved: false,
        latest: { start: item.start, end: item.end },
        move,
        up,
      }
      window.addEventListener('pointermove', move)
      window.addEventListener('pointerup', up)
    },
    [pxPerMs, min, max, snap, onCommit],
  )

  return { active, start }
}
