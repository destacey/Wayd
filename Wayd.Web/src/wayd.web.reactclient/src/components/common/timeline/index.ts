// timeline — public surface. Consumers import WaydTimeline directly per page.

export { WaydTimeline, default } from './wayd-timeline'
export type {
  WaydTimelineProps,
  ItemRenderProps,
  GroupRenderProps,
  ItemDateChange,
  ItemProgressChange,
  TimelineItem,
  TimelineGroup,
  TimelineVariant,
} from './types'

// Shared interaction primitives — reused by grid-hosted charts (the Gantt) so
// drag/resize/progress BEHAVIOR is single-sourced, not duplicated.
export { useBarDrag } from './render/use-bar-drag'
export type { UseBarDrag, BarDragState } from './render/use-bar-drag'
export { applyDrag, progressFromX, snapToDay } from './core/interaction'
export type { DragMode, DragResult } from './core/interaction'
export { dragLabel, formatDragDay } from './core/drag-label'
