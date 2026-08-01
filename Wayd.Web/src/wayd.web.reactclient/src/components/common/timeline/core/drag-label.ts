// timeline/core/drag-label.ts
// Shared live-drag date label logic (pure). Both the timeline chart and the
// grid-hosted Gantt show a floating date indicator while a bar is dragged, so
// the user sees where the endpoint(s) will land. This keeps the TEXT + which
// edge to anchor to single-sourced across both hosts.

import dayjs from 'dayjs'
import type { DragMode } from './interaction'

/** Format an epoch-ms value as a calendar day. */
export function formatDragDay(ms: number): string {
  return dayjs(ms).format('MMM D, YYYY')
}

/**
 * The label text + anchor edge for a live drag, by mode:
 *  - resize-start → the start date, anchored to the left edge
 *  - resize-end   → the end date, anchored to the right edge
 *  - move         → "start – end", anchored to the cursor (consumer positions)
 */
export function dragLabel(
  mode: DragMode,
  start: number,
  end: number,
): { text: string; anchor: 'start' | 'end' | 'cursor' } {
  if (mode === 'resize-start') {
    return { text: formatDragDay(start), anchor: 'start' }
  }
  if (mode === 'resize-end') {
    return { text: formatDragDay(end), anchor: 'end' }
  }
  return {
    text: `${formatDragDay(start)} – ${formatDragDay(end)}`,
    anchor: 'cursor',
  }
}
