import {
  itemBox,
  backgroundBox,
  isHorizontallyVisible,
  growRowsForLabels,
} from './geometry'
import { createTimeScale } from './scale'
import type { ResolvedRow, TimelineItem } from './types'

const DAY = 86_400_000
const day = (n: number) => n * DAY

// Scale: 10 days mapped onto 1000px => 100px/day.
const scale = createTimeScale(day(0), day(10), 1000)
const config = { laneHeight: 40, lanePadding: 4 }

const row = (top: number): ResolvedRow => ({
  groupId: 'g',
  top,
  height: 80,
  laneCount: 2,
  items: [],
  depth: 0,
})

describe('itemBox', () => {
  it('maps a range item to left/width via the scale', () => {
    // Arrange — days 2..5 on lane 0 of a row at top 0.
    const item: TimelineItem = { id: 'a', kind: 'range', start: day(2), end: day(5) }
    // Act
    const box = itemBox(item, 0, row(0), scale, config)
    // Assert — 100px/day: left 200, width 300.
    expect(box.left).toBeCloseTo(200)
    expect(box.width).toBeCloseTo(300)
  })

  it('offsets top by lane index and lane padding', () => {
    // Arrange — lane 1 in a row at top 100.
    const item: TimelineItem = { id: 'a', kind: 'range', start: day(0), end: day(1) }
    // Act
    const box = itemBox(item, 1, row(100), scale, config)
    // Assert — top = 100 + 1*40 + 4; height = 40 - 2*4.
    expect(box.top).toBe(144)
    expect(box.height).toBe(32)
  })

  it('shifts top down by half rowPadding so bars are vertically centred', () => {
    // Arrange — rowPadding = 6 means 3px added above the bar area.
    const item: TimelineItem = { id: 'a', kind: 'range', start: day(0), end: day(1) }
    const configWithPad = { laneHeight: 40, lanePadding: 4, rowPadding: 6 }
    // Act
    const box = itemBox(item, 0, row(0), scale, configWithPad)
    // Assert — top = 0 + 0 (topInset) + 3 (halfRowPad) + 0 (lane 0) + 4 (lanePadding).
    expect(box.top).toBe(7)
    // Height is unaffected by rowPadding.
    expect(box.height).toBe(32)
  })

  it('floors a one-day range to a square when the day is narrower than the bar height', () => {
    // Arrange — a single-day range at a zoomed-OUT scale where one day < bar height.
    // 2px/day scale: one day = 2px, well under the 32px bar height.
    const zoomedOut = createTimeScale(day(0), day(500), 1000)
    const item: TimelineItem = { id: 'a', kind: 'range', start: day(3), end: day(4) }
    // Act
    const box = itemBox(item, 0, row(0), zoomedOut, config)
    // Assert — floored to a square of the bar height (32) so it's an easy target.
    expect(box.width).toBe(box.height)
    expect(box.width).toBe(32)
  })

  it('keeps a one-day range at its natural width when the day is wider than the square', () => {
    // Arrange — at 100px/day, one day (100px) already exceeds the 32px square floor.
    const item: TimelineItem = { id: 'a', kind: 'range', start: day(3), end: day(4) }
    // Act
    const box = itemBox(item, 0, row(0), scale, config)
    // Assert — natural width wins.
    expect(box.width).toBeCloseTo(100)
  })

  it('clamps a range that extends past the domain end to the right edge', () => {
    // Arrange — starts at day 8 but ends at day 20, beyond the 10-day domain.
    const item: TimelineItem = { id: 'a', kind: 'range', start: day(8), end: day(20) }
    // Act
    const box = itemBox(item, 0, row(0), scale, config)
    // Assert — left 800, clamped right edge at 1000 → width 200 (not 1200).
    expect(box.left).toBeCloseTo(800)
    expect(box.width).toBeCloseTo(200)
  })

  it('clamps a range that starts before the domain to the left edge', () => {
    // Arrange — starts at day -5, ends at day 2.
    const item: TimelineItem = { id: 'a', kind: 'range', start: day(-5), end: day(2) }
    // Act
    const box = itemBox(item, 0, row(0), scale, config)
    // Assert — left clamped to 0, right at 200 → width 200.
    expect(box.left).toBeCloseTo(0)
    expect(box.width).toBeCloseTo(200)
  })

  it('gives zero width to a range entirely outside the domain', () => {
    // Arrange — days 12..15, fully past the 10-day domain.
    const item: TimelineItem = { id: 'a', kind: 'range', start: day(12), end: day(15) }
    // Act
    const box = itemBox(item, 0, row(0), scale, config)
    // Assert — nothing to show; no min-width sliver at the edge.
    expect(box.width).toBe(0)
  })

  it('places a milestone at its start with zero width', () => {
    // Arrange
    const item: TimelineItem = { id: 'm', kind: 'milestone', start: day(4), end: day(4) }
    // Act
    const box = itemBox(item, 0, row(0), scale, config)
    // Assert
    expect(box.left).toBeCloseTo(400)
    expect(box.width).toBe(0)
  })
})

describe('backgroundBox', () => {
  it('spans a row when a row is given', () => {
    // Arrange
    const bg: TimelineItem = { id: 'bg', kind: 'background', start: day(1), end: day(4) }
    // Act
    const box = backgroundBox(bg, row(50), scale)
    // Assert — left 100, width 300, full row height/top.
    expect(box.left).toBeCloseTo(100)
    expect(box.width).toBeCloseTo(300)
    expect(box.top).toBe(50)
    expect(box.height).toBe(80)
  })

  it('spans full chart height when no row is given', () => {
    // Arrange
    const bg: TimelineItem = { id: 'bg', kind: 'background', start: day(0), end: day(2) }
    // Act
    const box = backgroundBox(bg, null, scale, 500)
    // Assert
    expect(box.top).toBe(0)
    expect(box.height).toBe(500)
  })
})

describe('isHorizontallyVisible', () => {
  it('is true when the box overlaps the viewport', () => {
    // Arrange / Act / Assert
    expect(isHorizontallyVisible({ left: 100, top: 0, width: 50, height: 10 }, 1000)).toBe(true)
  })

  it('is false when the box is entirely left of the viewport', () => {
    // Arrange / Act / Assert
    expect(isHorizontallyVisible({ left: -200, top: 0, width: 50, height: 10 }, 1000)).toBe(false)
  })

  it('is false when the box is entirely right of the viewport', () => {
    // Arrange / Act / Assert
    expect(isHorizontallyVisible({ left: 1200, top: 0, width: 50, height: 10 }, 1000)).toBe(false)
  })
})

describe('growRowsForLabels', () => {
  const mkRow = (groupId: string, top: number, height: number): ResolvedRow => ({
    groupId,
    top,
    height,
    laneCount: 1,
    items: [],
    depth: 0,
  })

  it('returns the same rows reference when nothing needs growing', () => {
    // Arrange — labels shorter than row heights.
    const rows = [mkRow('a', 0, 40), mkRow('b', 40, 40)]
    const heights = new Map([['a', 30], ['b', 20]])
    // Act
    const out = growRowsForLabels(rows, heights)
    // Assert
    expect(out.rows).toBe(rows)
    expect(out.totalHeight).toBe(80)
  })

  it('grows a row whose label is taller and restacks following rows', () => {
    // Arrange — 'a' label needs 60px (row is 40).
    const rows = [mkRow('a', 0, 40), mkRow('b', 40, 40)]
    const heights = new Map([['a', 60]])
    // Act
    const out = growRowsForLabels(rows, heights)
    // Assert — 'a' grows to 60; 'b' shifts down to top 60.
    expect(out.rows[0].height).toBe(60)
    expect(out.rows[1].top).toBe(60)
    expect(out.totalHeight).toBe(100)
  })

  it('ignores rows without a groupId', () => {
    // Arrange
    const ungrouped: ResolvedRow = {
      top: 0, height: 40, laneCount: 1, items: [], depth: 0,
    }
    // Act
    const out = growRowsForLabels([ungrouped], new Map([['x', 99]]))
    // Assert — unchanged.
    expect(out.rows[0].height).toBe(40)
  })
})
