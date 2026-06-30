// Core math needs REAL dayjs (the global jest.setup mock only stubs `.format`).
jest.unmock('dayjs')
import { applyDrag, progressFromX, snapToDay } from './interaction'
import dayjs from 'dayjs'

const DAY = 86_400_000
const day = (n: number) => dayjs('2026-01-01').startOf('day').add(n, 'day').valueOf()

// 100px per day => pxPerMs.
const pxPerMs = 100 / DAY

const item = (startDay: number, endDay: number) => ({
  start: day(startDay),
  end: day(endDay),
  kind: 'range' as const,
})

describe('snapToDay', () => {
  it('snaps a mid-day timestamp to the start of that day', () => {
    // Arrange
    const midday = dayjs('2026-03-10').startOf('day').add(13, 'hour').valueOf()
    // Act
    const snapped = snapToDay(midday)
    // Assert
    expect(snapped).toBe(dayjs('2026-03-10').startOf('day').valueOf())
  })
})

describe('applyDrag — move', () => {
  it('shifts both ends by the dragged days, preserving duration', () => {
    // Arrange — drag +200px = +2 days.
    const ctx = { mode: 'move' as const, item: item(0, 3), pxPerMs, deltaPx: 200 }
    // Act
    const r = applyDrag(ctx)
    // Assert
    expect(r.start).toBe(day(2))
    expect(r.end).toBe(day(5))
  })

  it('snaps a fractional-day drag to the nearest day boundary', () => {
    // Arrange — +150px = +1.5 days; snaps start to a day boundary.
    const ctx = { mode: 'move' as const, item: item(0, 3), pxPerMs, deltaPx: 150 }
    // Act
    const r = applyDrag(ctx)
    // Assert — start lands on a clean day start.
    expect(r.start).toBe(dayjs(r.start).startOf('day').valueOf())
    expect(r.end - r.start).toBe(3 * DAY)
  })

  it('preserves matching calendar-day deltas for backend shift detection', () => {
    // Arrange
    const original = item(0, 3)
    const ctx = { mode: 'move' as const, item: original, pxPerMs, deltaPx: 200 }
    // Act
    const r = applyDrag(ctx)
    // Assert
    const startDelta = dayjs(r.start).diff(dayjs(original.start), 'day')
    const endDelta = dayjs(r.end).diff(dayjs(original.end), 'day')
    expect(startDelta).toBe(2)
    expect(endDelta).toBe(startDelta)
  })

  it('clamps move to max without changing duration', () => {
    // Arrange — large positive drag, max at day 4; item is 3 days long.
    const ctx = {
      mode: 'move' as const,
      item: item(0, 3),
      pxPerMs,
      deltaPx: 100000,
      max: day(4),
    }
    // Act
    const r = applyDrag(ctx)
    // Assert — end pinned at max, duration kept.
    expect(r.end).toBe(day(4))
    expect(r.end - r.start).toBe(3 * DAY)
  })
})

describe('applyDrag — resize', () => {
  it('moves only the start on resize-start', () => {
    // Arrange — drag start -100px = -1 day.
    const ctx = {
      mode: 'resize-start' as const,
      item: item(2, 6),
      pxPerMs,
      deltaPx: -100,
    }
    // Act
    const r = applyDrag(ctx)
    // Assert
    expect(r.start).toBe(day(1))
    expect(r.end).toBe(day(6))
  })

  it('does not let start cross end (min one-day span)', () => {
    // Arrange — drag start far right, past the end.
    const ctx = {
      mode: 'resize-start' as const,
      item: item(2, 4),
      pxPerMs,
      deltaPx: 100000,
    }
    // Act
    const r = applyDrag(ctx)
    // Assert
    expect(r.start).toBe(r.end - DAY)
  })

  it('moves only the end on resize-end', () => {
    // Arrange — drag end +200px = +2 days.
    const ctx = {
      mode: 'resize-end' as const,
      item: item(0, 3),
      pxPerMs,
      deltaPx: 200,
    }
    // Act
    const r = applyDrag(ctx)
    // Assert
    expect(r.start).toBe(day(0))
    expect(r.end).toBe(day(5))
  })

  it('does not let end cross start (min one-day span)', () => {
    // Arrange — drag end far left, before the start.
    const ctx = {
      mode: 'resize-end' as const,
      item: item(2, 6),
      pxPerMs,
      deltaPx: -100000,
    }
    // Act
    const r = applyDrag(ctx)
    // Assert
    expect(r.end).toBe(r.start + DAY)
  })
})

describe('progressFromX', () => {
  it('maps pointer position across the bar to 0..100', () => {
    // Arrange — bar at left=100, width=200; pointer at the midpoint.
    // Act / Assert
    expect(progressFromX(200, 100, 200)).toBe(50)
  })

  it('clamps below 0 and above 100', () => {
    // Arrange / Act / Assert
    expect(progressFromX(50, 100, 200)).toBe(0)
    expect(progressFromX(400, 100, 200)).toBe(100)
  })

  it('returns 0 for a zero-width bar', () => {
    // Arrange / Act / Assert
    expect(progressFromX(100, 100, 0)).toBe(0)
  })
})
