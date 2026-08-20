// timeline/gantt — the reusable grid-hosted Gantt chart pane.
//
// Consumers supply accessors for their own tree node shape and wire the result
// into WaydGrid's rightPane slot. See use-gantt-pane.tsx for the engine and
// app/planning/roadmaps/_components/roadmap-gantt.tsx for a reference adapter.

export {
  useGanttPane,
  computeGanttDomain,
  pxPerMsFor,
  toMs,
  DEFAULT_PX_PER_DAY,
  MIN_PX_PER_DAY,
  MAX_PX_PER_DAY,
  ZOOM_STEP,
} from './use-gantt-pane'
export type {
  GanttAccessors,
  GanttDragItem,
  GanttPaneModel,
  GanttPaneOptions,
  GanttRowKind,
} from './types'
export { useGanttZoom } from './use-gantt-zoom'
export type { UseGanttZoom } from './use-gantt-zoom'
export { GanttToolbarActions } from './gantt-toolbar-actions'
export type { GanttToolbarActionsProps } from './gantt-toolbar-actions'
