import { packLanes } from './packing'
import type { TimelineItem } from './types'

// Day in ms, for readable fixtures.
const DAY = 86_400_000
const day = (n: number) => n * DAY

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

describe('packLanes', () => {
  it('returns no lanes for an empty set', () => {
    // Arrange
    const items: TimelineItem[] = []
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.laneCount).toBe(0)
    expect(result.lanes.size).toBe(0)
  })

  it('places non-overlapping items on a single lane', () => {
    // Arrange
    const items = [range('a', 0, 2), range('b', 3, 5), range('c', 6, 8)]
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.laneCount).toBe(1)
    expect(result.lanes.get('a')).toBe(0)
    expect(result.lanes.get('b')).toBe(0)
    expect(result.lanes.get('c')).toBe(0)
  })

  it('treats touching intervals (a.end === b.start) as non-overlapping', () => {
    // Arrange — half-open [start, end): b starts exactly when a ends.
    const items = [range('a', 0, 3), range('b', 3, 6)]
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.laneCount).toBe(1)
    expect(result.lanes.get('a')).toBe(0)
    expect(result.lanes.get('b')).toBe(0)
  })

  it('pushes overlapping items onto separate lanes', () => {
    // Arrange
    const items = [range('a', 0, 5), range('b', 2, 7)]
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.laneCount).toBe(2)
    expect(result.lanes.get('a')).toBe(0)
    expect(result.lanes.get('b')).toBe(1)
  })

  it('uses the minimum number of lanes for a stack of overlaps', () => {
    // Arrange — three mutually overlapping items need three lanes.
    const items = [range('a', 0, 10), range('b', 1, 11), range('c', 2, 12)]
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.laneCount).toBe(3)
  })

  it('reuses a freed lane when an earlier item has ended', () => {
    // Arrange — a (0-2) and c (3-5) can share lane 0; b (0-5) needs lane 1.
    const items = [range('a', 0, 2), range('b', 0, 5), range('c', 3, 5)]
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.laneCount).toBe(2)
    expect(result.lanes.get('a')).toBe(0)
    expect(result.lanes.get('b')).toBe(1)
    expect(result.lanes.get('c')).toBe(0)
  })

  it('is deterministic regardless of input order', () => {
    // Arrange
    const forward = [range('a', 0, 5), range('b', 2, 7), range('c', 6, 9)]
    const reversed = [...forward].reverse()
    // Act
    const r1 = packLanes(forward)
    const r2 = packLanes(reversed)
    // Assert
    expect(r2.laneCount).toBe(r1.laneCount)
    expect(r2.lanes.get('a')).toBe(r1.lanes.get('a'))
    expect(r2.lanes.get('b')).toBe(r1.lanes.get('b'))
    expect(r2.lanes.get('c')).toBe(r1.lanes.get('c'))
  })

  it('breaks ties on equal start by `order` then id', () => {
    // Arrange — same start; order decides which takes lane 0.
    const items = [
      range('late', 0, 5, { order: 2 }),
      range('early', 0, 5, { order: 1 }),
    ]
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.lanes.get('early')).toBe(0)
    expect(result.lanes.get('late')).toBe(1)
  })

  it('packs milestones as instants (zero-width), allowing tight sharing', () => {
    // Arrange — a range ending at day 3 and a milestone at day 3 share a lane.
    const items: TimelineItem[] = [
      range('r', 0, 3),
      { id: 'm', kind: 'milestone', start: day(3), end: day(3) },
    ]
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.laneCount).toBe(1)
    expect(result.lanes.get('m')).toBe(0)
  })

  it('uses custom collision ends when provided', () => {
    // Arrange — item a's visible label extends to day 4, so b cannot share lane 0.
    const items = [range('a', 0, 1), range('b', 2, 3)]
    // Act
    const result = packLanes(items, {
      getCollisionEnd: (item) => (item.id === 'a' ? day(4) : item.end),
    })
    // Assert
    expect(result.laneCount).toBe(2)
    expect(result.lanes.get('a')).toBe(0)
    expect(result.lanes.get('b')).toBe(1)
  })

  it('excludes background items from packing', () => {
    // Arrange
    const items: TimelineItem[] = [
      range('a', 0, 5),
      { id: 'bg', kind: 'background', start: day(0), end: day(10) },
    ]
    // Act
    const result = packLanes(items)
    // Assert
    expect(result.lanes.has('bg')).toBe(false)
    expect(result.laneCount).toBe(1)
  })
})
