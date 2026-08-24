'use client'

// roadmap-gantt.tsx — the roadmap's adapter over the shared Gantt pane engine
// (components/common/timeline/gantt). All the chart mechanics — axis, scale,
// rollup, bars, diamonds, drag rendering — live in the engine; this file only
// describes how to read a RoadmapItemTreeNode.

import {
  toMs,
  useGanttPane,
  computeGanttDomain as computeGenericDomain,
  type GanttAccessors,
  type GanttPaneModel,
  type GanttPaneOptions,
} from '@/src/components/common/timeline'
import type { RoadmapItemTreeNode } from './roadmap-items-grid'

export {
  pxPerMsFor,
  DEFAULT_PX_PER_DAY,
  MIN_PX_PER_DAY,
  MAX_PX_PER_DAY,
  ZOOM_STEP,
} from '@/src/components/common/timeline'
export type { GanttDragItem } from '@/src/components/common/timeline'

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

/** How the shared engine reads a roadmap tree node. */
const roadmapAccessors: GanttAccessors<RoadmapItemTreeNode> = {
  id: (n) => n.id,
  children: (n) => n.children,
  name: (n) => n.name,
  kind: (n) => (n.type === 'Milestone' ? 'milestone' : 'range'),
  range: nodeRange,
  color: (n) => n.color ?? undefined,
  variant: (n) => (n.type === 'Timebox' ? 'muted' : 'default'),
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
  return computeGenericDomain(treeData, roadmapAccessors, [
    toMs(roadmapStart),
    toMs(roadmapEnd),
  ])
}

export type RoadmapGanttOptions = Omit<
  GanttPaneOptions<RoadmapItemTreeNode>,
  'domainHint'
>
export type RoadmapGanttModel = GanttPaneModel<RoadmapItemTreeNode>

/**
 * Build the axis + per-row bar renderers for the roadmap Gantt pane from the
 * roadmap window and the grid's tree data.
 */
export function useRoadmapGantt(
  roadmapStart: Date | string,
  roadmapEnd: Date | string,
  treeData: RoadmapItemTreeNode[],
  options: RoadmapGanttOptions = {},
): RoadmapGanttModel {
  return useGanttPane(treeData, roadmapAccessors, {
    ...options,
    domainHint: [toMs(roadmapStart), toMs(roadmapEnd)],
  })
}
