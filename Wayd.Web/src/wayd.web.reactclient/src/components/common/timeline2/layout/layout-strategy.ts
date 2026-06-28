// timeline2/layout/layout-strategy.ts
// The ONLY variant-aware seam (per spec architecture). A layout strategy turns
// items + groups into ResolvedRow[] (top/height/lanes). Everything downstream
// (render, virtualization, interaction) is variant-agnostic and consumes rows.

import type { ResolvedRow, TimelineGroup, TimelineItem } from '../core/types'

/** Vertical sizing knobs shared by all strategies. */
export interface LayoutConfig {
  /** Height of a single lane within a row, px. */
  laneHeight: number
  /** Vertical padding added to each row (top+bottom combined), px. */
  rowPadding: number
}

export const DEFAULT_LAYOUT_CONFIG: LayoutConfig = {
  laneHeight: 32,
  rowPadding: 8,
}

export interface LayoutInput<TItem = unknown, TGroup = unknown> {
  items: TimelineItem<TItem>[]
  /** Omitted/empty => ungrouped: a single packed row of all items. */
  groups?: TimelineGroup<TGroup>[]
  /**
   * Group ids that have a row-scoped background (timebox). Those rows reserve a
   * lane of headroom at the top so the timebox label is readable above the bars.
   */
  backgroundGroupIds?: Set<string>
  /**
   * True when there are chart-wide (ungrouped) backgrounds — e.g. at drill
   * level 1 the root timeboxes span the flat chart, so the single ungrouped row
   * reserves top headroom for their labels above the bars.
   */
  hasChartBackground?: boolean
  /** Optional effective exclusive end used only for lane collision detection. */
  getCollisionEnd?: (item: TimelineItem<TItem>) => number
  config?: Partial<LayoutConfig>
}

export interface LayoutOutput {
  rows: ResolvedRow[]
  /** Total stacked height of all rows, px. */
  totalHeight: number
}

/**
 * A pure layout strategy. `timeline` packs lanes + collapses to lane; `gantt`
 * gives one row per record. Both produce the same ResolvedRow[] contract.
 */
export type LayoutStrategy = (input: LayoutInput) => LayoutOutput

/** Merge partial config over defaults. */
export function resolveConfig(config?: Partial<LayoutConfig>): LayoutConfig {
  return { ...DEFAULT_LAYOUT_CONFIG, ...config }
}
