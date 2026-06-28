import { timelineLayout } from './timeline-layout'
import { DEFAULT_LAYOUT_CONFIG } from './layout-strategy'
import type { TimelineGroup, TimelineItem } from '../core/types'

const DAY = 86_400_000
const day = (n: number) => n * DAY
const { laneHeight, rowPadding } = DEFAULT_LAYOUT_CONFIG
const oneLane = laneHeight + rowPadding

const range = (
  id: string,
  startDay: number,
  endDay: number,
  extra: Partial<TimelineItem> = {},
): TimelineItem => ({
  id,
  kind: 'range',
  start: day(startDay),
  end: day(endDay),
  ...extra,
})

const group = (
  id: string,
  extra: Partial<TimelineGroup> = {},
): TimelineGroup => ({
  id,
  ...extra,
})

describe('timelineLayout', () => {
  it('returns nothing for empty input', () => {
    // Arrange / Act
    const out = timelineLayout({ items: [] })
    // Assert
    expect(out.rows).toEqual([])
    expect(out.totalHeight).toBe(0)
  })

  it('packs all ungrouped items into a single row', () => {
    // Arrange — two non-overlapping items pack onto one lane.
    const items = [range('a', 0, 2), range('b', 3, 5)]
    // Act
    const out = timelineLayout({ items })
    // Assert
    expect(out.rows).toHaveLength(1)
    expect(out.rows[0].laneCount).toBe(1)
    expect(out.rows[0].groupId).toBeUndefined()
    expect(out.totalHeight).toBe(oneLane)
  })

  it('sizes an ungrouped row by its lane count', () => {
    // Arrange — two overlapping items => two lanes.
    const items = [range('a', 0, 5), range('b', 2, 7)]
    // Act
    const out = timelineLayout({ items })
    // Assert
    expect(out.rows[0].laneCount).toBe(2)
    expect(out.rows[0].height).toBe(2 * laneHeight + rowPadding)
  })

  it('emits one row per group and stacks them top-to-bottom', () => {
    // Arrange
    const groups = [group('g1', { order: 1 }), group('g2', { order: 2 })]
    const items = [
      range('a', 0, 2, { groupId: 'g1' }),
      range('b', 0, 2, { groupId: 'g2' }),
    ]
    // Act
    const out = timelineLayout({ items, groups })
    // Assert
    expect(out.rows.map((r) => r.groupId)).toEqual(['g1', 'g2'])
    expect(out.rows[0].top).toBe(0)
    expect(out.rows[1].top).toBe(out.rows[0].height)
  })

  it('orders sibling groups by order then id', () => {
    // Arrange — declared out of order; expect order-sorted output.
    const groups = [group('b', { order: 2 }), group('a', { order: 1 })]
    const items = [
      range('x', 0, 1, { groupId: 'a' }),
      range('y', 0, 1, { groupId: 'b' }),
    ]
    // Act
    const out = timelineLayout({ items, groups })
    // Assert
    expect(out.rows.map((r) => r.groupId)).toEqual(['a', 'b'])
  })

  it('indents nested groups by depth and shows children as their own rows when expanded', () => {
    // Arrange — parent g1 with child g1a, both expanded (default).
    const groups = [group('g1'), group('g1a', { parentId: 'g1' })]
    const items = [
      range('p', 0, 2, { groupId: 'g1' }),
      range('c', 0, 2, { groupId: 'g1a' }),
    ]
    // Act
    const out = timelineLayout({ items, groups })
    // Assert
    expect(out.rows.map((r) => r.groupId)).toEqual(['g1', 'g1a'])
    expect(out.rows.find((r) => r.groupId === 'g1')!.depth).toBe(0)
    expect(out.rows.find((r) => r.groupId === 'g1a')!.depth).toBe(1)
  })

  it('collapses children onto the parent lane (collapse-to-lane)', () => {
    // Arrange — g1 collapsed; its child g1a's items fold up into g1's row.
    // Parent item p (0-2) and child item c (3-5) don't overlap => 1 lane.
    const groups = [
      group('g1', { collapsed: true }),
      group('g1a', { parentId: 'g1' }),
    ]
    const items = [
      range('p', 0, 2, { groupId: 'g1' }),
      range('c', 3, 5, { groupId: 'g1a' }),
    ]
    // Act
    const out = timelineLayout({ items, groups })
    // Assert — only the parent row exists; it holds BOTH items.
    expect(out.rows).toHaveLength(1)
    expect(out.rows[0].groupId).toBe('g1')
    const ids = out.rows[0].items.map((i) => i.item.id).sort()
    expect(ids).toEqual(['c', 'p'])
    expect(out.rows[0].laneCount).toBe(1)
  })

  it('packs collapsed descendants into multiple lanes when they overlap', () => {
    // Arrange — collapsed parent; parent + child items overlap => 2 lanes.
    const groups = [
      group('g1', { collapsed: true }),
      group('g1a', { parentId: 'g1' }),
    ]
    const items = [
      range('p', 0, 5, { groupId: 'g1' }),
      range('c', 2, 7, { groupId: 'g1a' }),
    ]
    // Act
    const out = timelineLayout({ items, groups })
    // Assert
    expect(out.rows).toHaveLength(1)
    expect(out.rows[0].laneCount).toBe(2)
  })

  it('folds the entire subtree when collapsing a grandparent', () => {
    // Arrange — g1 > g1a > g1aa, all items; g1 collapsed.
    const groups = [
      group('g1', { collapsed: true }),
      group('g1a', { parentId: 'g1' }),
      group('g1aa', { parentId: 'g1a' }),
    ]
    const items = [
      range('p', 0, 1, { groupId: 'g1' }),
      range('c', 2, 3, { groupId: 'g1a' }),
      range('gc', 4, 5, { groupId: 'g1aa' }),
    ]
    // Act
    const out = timelineLayout({ items, groups })
    // Assert
    expect(out.rows).toHaveLength(1)
    expect(out.rows[0].items.map((i) => i.item.id).sort()).toEqual([
      'c',
      'gc',
      'p',
    ])
  })

  it('assigns each placed item a lane index', () => {
    // Arrange — overlapping items in one ungrouped row.
    const items = [range('a', 0, 5), range('b', 2, 7)]
    // Act
    const out = timelineLayout({ items })
    // Assert
    const lanes = out.rows[0].items
      .slice()
      .sort((x, y) => (x.item.id < y.item.id ? -1 : 1))
      .map((i) => i.lane)
    expect(lanes).toEqual([0, 1])
  })

  it('packs items using effective collision ends for outside labels', () => {
    // Arrange — a ends at day 1, but its outside label reserves space to day 4.
    const items = [range('a', 0, 1), range('b', 2, 3)]
    // Act
    const out = timelineLayout({
      items,
      getCollisionEnd: (item) => (item.id === 'a' ? day(4) : item.end),
    })
    // Assert
    expect(out.rows[0].laneCount).toBe(2)
    expect(out.rows[0].items.find((i) => i.item.id === 'a')?.lane).toBe(0)
    expect(out.rows[0].items.find((i) => i.item.id === 'b')?.lane).toBe(1)
  })
})
