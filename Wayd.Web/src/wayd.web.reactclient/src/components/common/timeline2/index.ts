// timeline2 — public surface. Parallel to the legacy `timeline/` (WaydTimeline)
// during migration; consumers import WaydTimeline2 directly per page.

export { WaydTimeline2, default } from './wayd-timeline2'
export type {
  WaydTimeline2Props,
  ItemRenderProps,
  GroupRenderProps,
  ItemDateChange,
  ItemProgressChange,
  TimelineItem,
  TimelineGroup,
  TimelineVariant,
} from './types'
