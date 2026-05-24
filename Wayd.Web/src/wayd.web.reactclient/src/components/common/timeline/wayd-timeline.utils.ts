import {
  WaydDataGroup,
  WaydDataItem,
  WaydTimelineProps,
  TimelineTemplate,
} from '@/src/components/common/timeline/types'
import { timeLineColorsByTheme } from '@/src/config/theme/timeline-colors'
import { DataItemEnhanced } from 'vis-timeline/standalone'
import {
  BackgroundItemTemplate,
  GroupTemplate,
  RangeItemTemplate,
} from '@/src/components/common/timeline/templates'

export const DefaultTimeLineColors = timeLineColorsByTheme

export function getDefaultTemplate<
  TItem extends WaydDataItem,
  TGroup extends WaydDataGroup,
>(
  type: 'group',
  props: WaydTimelineProps<TItem, TGroup>,
): TimelineTemplate<TGroup>

export function getDefaultTemplate<
  TItem extends WaydDataItem,
  TGroup extends WaydDataGroup,
>(
  type: DataItemEnhanced['type'],
  props: WaydTimelineProps<TItem, TGroup>,
): TimelineTemplate<TItem>

export function getDefaultTemplate<
  TItem extends WaydDataItem,
  TGroup extends WaydDataGroup,
>(
  type: DataItemEnhanced['type'] | 'group',
  props: WaydTimelineProps<TItem, TGroup>,
) {
  switch (type) {
    case 'range':
      return props.rangeItemTemplate ?? RangeItemTemplate
    case 'background':
      return BackgroundItemTemplate
    case 'group':
      return props.groupTemplate ?? GroupTemplate
    case 'box':
    case 'point':
    default:
      return undefined
  }
}

// Compares the fields of two data items that affect visible output — the
// fields the item template reads (`content`, `itemColor`) plus the fields
// vis-timeline itself uses to lay out and style the item (`start`, `end`,
// `type`, `group`, `style`, `className`, `title`, `order`). Returns true
// when an update would produce no visible change, so the caller can skip
// the destructive evict + DataSet.update() round-trip (which causes the
// whole timeline to flicker on every refetch).
//
// `start` and `end` are compared by epoch time because the same instant can
// arrive as `Date`, number, or string depending on the source (RTK Query
// payload vs. vis-data's internal representation after a previous update).
export function itemsVisuallyEqual<TItem extends WaydDataItem>(
  a: TItem,
  b: TItem,
): boolean {
  if (toTime(a.start) !== toTime(b.start)) return false
  if (toTime(a.end) !== toTime(b.end)) return false
  if (a.content !== b.content) return false
  if (a.itemColor !== b.itemColor) return false
  if (a.style !== b.style) return false
  if (a.className !== b.className) return false
  if (a.title !== b.title) return false
  if (a.type !== b.type) return false
  if (a.group !== b.group) return false
  if ((a as { order?: unknown }).order !== (b as { order?: unknown }).order)
    return false
  return true
}

function toTime(value: unknown): number | undefined {
  if (value === undefined || value === null) return undefined
  if (value instanceof Date) return value.getTime()
  if (typeof value === 'number') return value
  if (typeof value === 'string') {
    const t = new Date(value).getTime()
    return Number.isNaN(t) ? undefined : t
  }
  // vis-data may store Moment-like objects; fall back to toDate()/valueOf()
  const v = value as { toDate?: () => Date; valueOf?: () => number }
  if (typeof v.toDate === 'function') return v.toDate().getTime()
  if (typeof v.valueOf === 'function') {
    const n = v.valueOf()
    return typeof n === 'number' ? n : undefined
  }
  return undefined
}

// Structural comparison of two group arrays — checks the id set and each
// group's `nestedGroups` membership, which together define the hierarchy
// that vis-timeline tracks internally. Returns true when setGroups() would
// produce no meaningful change. Ordering is irrelevant: vis-timeline uses
// `groupOrder`, not array index, to lay out rows. Label/style-only changes
// should also return true — those are patched in place via DataSet.update().
export function groupsStructurallyEqual<TGroup extends WaydDataGroup>(
  a: TGroup[],
  b: TGroup[],
): boolean {
  if (a.length !== b.length) return false

  const byId = new Map<string | number, TGroup>()
  a.forEach((g) => {
    if (g.id !== undefined) byId.set(g.id, g)
  })

  for (const next of b) {
    if (next.id === undefined) return false
    const prev = byId.get(next.id)
    if (!prev) return false

    const prevNested = prev.nestedGroups ?? []
    const nextNested = next.nestedGroups ?? []
    if (prevNested.length !== nextNested.length) return false
    for (let i = 0; i < prevNested.length; i++) {
      if (prevNested[i] !== nextNested[i]) return false
    }
  }

  return true
}
