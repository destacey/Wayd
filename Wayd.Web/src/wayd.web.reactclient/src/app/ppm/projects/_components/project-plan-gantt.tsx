'use client'

// project-plan-gantt.tsx — the project plan's adapter over the shared Gantt pane
// engine (components/common/timeline/gantt). All the chart mechanics — axis,
// scale, rollup, bars, diamonds, drag rendering — live in the engine; this file
// only describes how to read a ProjectPlanNodeDto.
//
// The plan tree is Stage → Task → (nested Task), where a task whose type is
// "Milestone" is a single-date marker on `plannedDate` rather than a span.

import type { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import {
  toMs,
  useGanttPane,
  computeGanttDomain as computeGenericDomain,
  type GanttAccessors,
  type GanttPaneModel,
  type GanttPaneOptions,
} from '@/src/components/common/timeline'

export {
  pxPerMsFor,
  DEFAULT_PX_PER_DAY,
  MIN_PX_PER_DAY,
  MAX_PX_PER_DAY,
  ZOOM_STEP,
} from '@/src/components/common/timeline'
export type { GanttDragItem } from '@/src/components/common/timeline'

/** A milestone is a task typed "Milestone" — it sits on a single planned date. */
export const isMilestoneNode = (node: ProjectPlanNodeDto): boolean =>
  node.type?.name === 'Milestone'

/** Stage rows are containers; their span is rolled up from their tasks. */
export const isStageNode = (node: ProjectPlanNodeDto): boolean =>
  node.nodeType === 'Stage'

/**
 * The [start, end] a row occupies: the planned instant for a milestone, the
 * planned range for a task or stage, or undefined when it has no dates (a
 * container row, which gets a rolled-up summary bar instead).
 */
function nodeRange(node: ProjectPlanNodeDto): [number, number] | undefined {
  if (isMilestoneNode(node)) {
    const d = toMs(node.plannedDate)
    return d != null ? [d, d] : undefined
  }
  const s = toMs(node.start)
  const e = toMs(node.end)
  return s != null && e != null ? [s, e] : undefined
}

/** How the shared engine reads a project plan node. */
const projectPlanAccessors: GanttAccessors<ProjectPlanNodeDto> = {
  id: (n) => n.id,
  children: (n) => n.children,
  name: (n) => n.name,
  kind: (n) => (isMilestoneNode(n) ? 'milestone' : 'range'),
  range: nodeRange,
  progress: (n) => n.progress,
  // A stage's own bar reads as a container summary even when it carries its own
  // dates, so it gets the muted treatment rather than a solid task bar.
  variant: (n) => (isStageNode(n) ? 'muted' : 'default'),
}

/**
 * Write dragged dates into a cached plan tree in place, so a dropped bar holds
 * its position until the refetch lands (without this the bar visibly snaps back
 * to where it started, then jumps to the new dates). Mirrors applyOptimisticDates
 * in the roadmap API. Returns whether the node was found.
 *
 * The DTO fields are typed `Date` but hold ISO strings at runtime; we store
 * YYYY-MM-DD to match the post-refetch shape (dayjs parses both, and storing real
 * Dates would trip Redux's serializability check).
 */
export function applyOptimisticPlanDates(
  nodes: ProjectPlanNodeDto[] | undefined,
  id: string,
  isMilestone: boolean,
  start: string,
  end: string,
): boolean {
  if (!nodes) return false
  for (const node of nodes) {
    if (node.id === id) {
      if (isMilestone) {
        node.plannedDate = start as unknown as Date
      } else {
        node.start = start as unknown as Date
        node.end = end as unknown as Date
      }
      return true
    }
    if (applyOptimisticPlanDates(node.children, id, isMilestone, start, end)) {
      return true
    }
  }
  return false
}

/**
 * The chart's time domain (epoch ms): every dated stage/task/milestone, plus the
 * project's own window when supplied, padded so nothing sits flush to the edge.
 * Exported so the drag hook is built from the same domain the chart uses.
 */
export function computeProjectPlanGanttDomain(
  treeData: ProjectPlanNodeDto[],
  projectStart?: Date | string,
  projectEnd?: Date | string,
): { domainStart: number; domainEnd: number } {
  return computeGenericDomain(treeData, projectPlanAccessors, [
    toMs(projectStart),
    toMs(projectEnd),
  ])
}

export type ProjectPlanGanttOptions = Omit<
  GanttPaneOptions<ProjectPlanNodeDto>,
  'domainHint'
> & {
  projectStart?: Date | string
  projectEnd?: Date | string
}

export type ProjectPlanGanttModel = GanttPaneModel<ProjectPlanNodeDto>

/**
 * Build the axis + per-row bar renderers for the project plan Gantt pane from
 * the plan tree.
 */
export function useProjectPlanGantt(
  treeData: ProjectPlanNodeDto[],
  options: ProjectPlanGanttOptions = {},
): ProjectPlanGanttModel {
  const { projectStart, projectEnd, ...paneOptions } = options
  return useGanttPane(treeData, projectPlanAccessors, {
    ...paneOptions,
    domainHint: [toMs(projectStart), toMs(projectEnd)],
  })
}
