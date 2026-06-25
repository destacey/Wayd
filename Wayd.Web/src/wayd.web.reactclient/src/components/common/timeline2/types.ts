// timeline2/types.ts
// Public, React-facing types for WaydTimeline2 (distinct from core/types.ts,
// which is the pure-math domain model).

import type { FC, ReactNode } from 'react'
import type {
  TimelineGroup,
  TimelineItem,
  TimelineVariant,
} from './core/types'

export type { TimelineItem, TimelineGroup, TimelineVariant } from './core/types'

/** Props passed to a consumer's custom bar renderer. */
export interface ItemRenderProps<T = unknown> {
  item: TimelineItem<T>
  /** Resolved foreground color for text on the bar (contrast-aware). */
  fontColor: string
  /** Resolved background color for the bar. */
  backgroundColor: string
  selected: boolean
}

/** Props passed to a consumer's custom group-label renderer. */
export interface GroupRenderProps<T = unknown> {
  group: TimelineGroup<T>
  depth: number
  collapsed: boolean
}

/** A date-range change emitted by move or endpoint-resize. */
export interface ItemDateChange {
  id: string
  start: number
  end: number
}

/** A progress change emitted by the progress handle. */
export interface ItemProgressChange {
  id: string
  progress: number
}

export interface WaydTimeline2Props<TItem = unknown, TGroup = unknown> {
  variant?: TimelineVariant
  items: TimelineItem<TItem>[]
  groups?: TimelineGroup<TGroup>[]

  /**
   * Default visible window on load, epoch ms — the initial view the user sees.
   * Must fall within [minDate, maxDate]. (vis-timeline's `start`/`end`.)
   */
  windowStart: number
  windowEnd: number
  /**
   * Hard bounds for the rendered time domain AND pan/zoom limits, epoch ms.
   * Items are clamped to this range (anything past it is clipped at the edge),
   * and the user cannot pan/zoom outside it. Defaults to windowStart/windowEnd
   * when omitted. (vis-timeline's `min`/`max`.)
   */
  minDate?: number
  maxDate?: number

  /** Sizing. */
  height?: number
  laneHeight?: number
  /** Default width of the left group-label column (only shown when groups exist).
   *  When `storageKey` is set, the user's resized width is persisted and overrides
   *  this default on subsequent loads. */
  groupColumnWidth?: number
  /**
   * Stable key identifying this timeline instance (e.g. a roadmap id). When set,
   * per-instance UI preferences (group-column width) are persisted to
   * localStorage under this key and restored on the next load.
   */
  storageKey?: string
  /** Initial drill-through level (1 = flat, no groups; 2 = top tier as groups).
   *  Default 2. */
  defaultDrillLevel?: number

  /** Default for the current-time indicator. When `allowToggleCurrentTime` is on,
   *  this is the initial value of the user-togglable setting. Default true. */
  showCurrentTime?: boolean
  /** Show a settings menu with a "Show Current Time" toggle. Default true. */
  allowToggleCurrentTime?: boolean

  /** Toolbar chrome. */
  allowFullScreen?: boolean
  allowSaveAsImage?: boolean
  /** Time-axis zoom controls (Ctrl/Cmd+wheel + toolbar +/- and Reset View).
   *  Default true. */
  allowZoom?: boolean
  /** Base filename (without extension) for save-as-image. */
  saveImageFileName?: string
  /** Toolbar slots — left for controls (e.g. future drill +/-), right for extra actions. */
  toolbarLeftSlot?: ReactNode
  toolbarRightSlot?: ReactNode
  /** Refresh action — shows a Reload toolbar button (like WaydGrid) when provided. */
  onRefresh?: () => void

  /** Custom renderers. */
  itemRenderer?: FC<ItemRenderProps<TItem>>
  groupRenderer?: FC<GroupRenderProps<TGroup>>

  /** Interaction (omit to disable). */
  editable?: boolean
  onItemDateChange?: (change: ItemDateChange) => void
  onItemProgressChange?: (change: ItemProgressChange) => void
  onItemClick?: (item: TimelineItem<TItem>) => void

  /** States. */
  isLoading?: boolean
  emptyMessage?: string
}
