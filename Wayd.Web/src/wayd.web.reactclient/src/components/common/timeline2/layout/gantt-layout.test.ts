import { ganttLayout } from './gantt-layout'
import { getLayoutStrategy } from './index'
import { DEFAULT_LAYOUT_CONFIG } from './layout-strategy'
import type { TimelineGroup, TimelineItem } from '../core/types'

const DAY = 86_400_000
const day = (n: number) => n * DAY
const { laneHeight, rowPadding } = DEFAULT_LAYOUT_CONFIG
const oneRow = laneHeight + rowPadding

const range = (
  id: string,
  startDay: number,
  endDay: number,
  extra: Partial<TimelineItem> = {},
): TimelineItem => ({ id, kind: 'range', start: day(startDay), end: day(endDay), ...extra })

const group = (id: string, extra: Partial<TimelineGroup> = {}): TimelineGroup => ({
  id,
  ...extra,
})

describe('ganttLayout', () => {
  it('gives one single-lane row per item, even when they overlap', () => {
    // Arrange — overlapping items would pack in timeline, but Gantt never packs.
    const items = [range('a', 0, 5), range('b', 2, 7)]
    // Act
    const out = ganttLayout({ items })
    // Assert
    expect(out.rows).toHaveLength(2)
    expect(out.rows.every((r) => r.laneCount === 1)).toBe(true)
    expect(out.rows[0].top).toBe(0)
    expect(out.rows[1].top).toBe(oneRow)
    expect(out.totalHeight).toBe(2 * oneRow)
  })

  it('orders items by order then start then id', () => {
    // Arrange
    const items = [
      range('late', 0, 1, { order: 2 }),
      range('early', 5, 6, { order: 1 }),
    ]
    // Act
    const out = ganttLayout({ items })
    // Assert
    expect(out.rows.map((r) => r.items[0].item.id)).toEqual(['early', 'late'])
  })

  it('gives each item-row a unique rowKey even within one group', () => {
    // Arrange — two items in the SAME group would collide if rows keyed on
    // groupId; rowKey must be the item id so React keys / measurement IDs differ.
    const groups = [group('g1')]
    const items = [
      range('a', 0, 1, { groupId: 'g1' }),
      range('b', 2, 3, { groupId: 'g1' }),
    ]
    // Act
    const out = ganttLayout({ items, groups })
    // Assert — both rows share a groupId but have distinct, item-based rowKeys.
    expect(out.rows.map((r) => r.groupId)).toEqual(['g1', 'g1'])
    expect(out.rows.map((r) => r.rowKey)).toEqual(['a', 'b'])
    expect(new Set(out.rows.map((r) => r.rowKey)).size).toBe(out.rows.length)
  })

  it('walks the group tree, indenting child items by depth', () => {
    // Arrange — parent group g1 with child g1a; one item each.
    const groups = [group('g1'), group('g1a', { parentId: 'g1' })]
    const items = [
      range('c', 0, 1, { groupId: 'g1a' }),
      range('p', 0, 1, { groupId: 'g1' }),
    ]
    // Act
    const out = ganttLayout({ items, groups })
    // Assert — parent item first (depth 0), then child item (depth 1).
    expect(out.rows.map((r) => r.items[0].item.id)).toEqual(['p', 'c'])
    expect(out.rows[0].depth).toBe(0)
    expect(out.rows[1].depth).toBe(1)
  })
})

describe('getLayoutStrategy', () => {
  it('returns the gantt strategy for variant "gantt"', () => {
    // Arrange / Act / Assert
    expect(getLayoutStrategy('gantt')).toBe(ganttLayout)
  })

  it('returns a strategy that does not pack for gantt', () => {
    // Arrange — overlapping items.
    const strategy = getLayoutStrategy('gantt')
    // Act
    const out = strategy({ items: [range('a', 0, 5), range('b', 1, 6)] })
    // Assert — one row each, not packed.
    expect(out.rows).toHaveLength(2)
  })
})
