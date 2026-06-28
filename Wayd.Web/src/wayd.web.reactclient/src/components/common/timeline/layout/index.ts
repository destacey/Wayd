// timeline/layout — the variant seam. Pick a strategy by variant.

import type { TimelineVariant } from '../core/types'
import type { LayoutStrategy } from './layout-strategy'
import { timelineLayout } from './timeline-layout'
import { ganttLayout } from './gantt-layout'

export * from './layout-strategy'
export { timelineLayout } from './timeline-layout'
export { ganttLayout } from './gantt-layout'

/** Resolve the layout strategy for a variant. */
export function getLayoutStrategy(variant: TimelineVariant): LayoutStrategy {
  return variant === 'gantt' ? ganttLayout : timelineLayout
}
