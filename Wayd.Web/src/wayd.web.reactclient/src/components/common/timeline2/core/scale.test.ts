// scale.ticks() uses REAL dayjs (.startOf/.add); the global mock only stubs format.
jest.unmock('dayjs')
import { createTimeScale } from './scale'
import dayjs from 'dayjs'

const DAY = 86_400_000
const start = dayjs('2026-01-01').startOf('day').valueOf()

describe('createTimeScale — mapping', () => {
  it('maps domain start to x=0 and end to x=width', () => {
    // Arrange
    const scale = createTimeScale(start, start + 10 * DAY, 1000)
    // Act / Assert
    expect(scale.toX(start)).toBeCloseTo(0)
    expect(scale.toX(start + 10 * DAY)).toBeCloseTo(1000)
  })

  it('round-trips toX/toMs', () => {
    // Arrange
    const scale = createTimeScale(start, start + 10 * DAY, 1000)
    const ms = start + 4 * DAY
    // Act / Assert
    expect(scale.toMs(scale.toX(ms))).toBeCloseTo(ms)
  })

  it('guards a zero/negative width without producing NaN', () => {
    // Arrange
    const scale = createTimeScale(start, start + DAY, 0)
    // Act / Assert
    expect(Number.isFinite(scale.toX(start))).toBe(true)
  })

  it('guards an inverted/degenerate domain', () => {
    // Arrange — end <= start.
    const scale = createTimeScale(start, start, 500)
    // Act / Assert
    expect(scale.domain[1]).toBeGreaterThan(scale.domain[0])
  })
})

describe('createTimeScale — ticks', () => {
  it('returns calendar-aligned ticks within the domain', () => {
    // Arrange — 10-day window, ~10 ticks => daily.
    const scale = createTimeScale(start, start + 10 * DAY, 1000)
    // Act
    const ticks = scale.ticks(10)
    // Assert — all within domain, ascending, day-aligned.
    expect(ticks.length).toBeGreaterThan(0)
    expect(ticks[0]).toBeGreaterThanOrEqual(start)
    expect(ticks[ticks.length - 1]).toBeLessThanOrEqual(start + 10 * DAY)
    for (let i = 1; i < ticks.length; i += 1) {
      expect(ticks[i]).toBeGreaterThan(ticks[i - 1])
    }
    ticks.forEach((t) => expect(t).toBe(dayjs(t).startOf('day').valueOf()))
  })

  it('uses coarser steps for a long domain', () => {
    // Arrange — ~2 year window.
    const scale = createTimeScale(start, start + 730 * DAY, 1000)
    // Act
    const ticks = scale.ticks(10)
    // Assert — spacing is much larger than a day (monthly/quarterly/yearly).
    const gap = ticks[1] - ticks[0]
    expect(gap).toBeGreaterThan(20 * DAY)
  })

  it('never loops unboundedly for a tiny tick count', () => {
    // Arrange
    const scale = createTimeScale(start, start + 365 * DAY, 1000)
    // Act
    const ticks = scale.ticks(1)
    // Assert
    expect(ticks.length).toBeLessThan(1000)
  })
})

describe('createTimeScale — tiers', () => {
  it('builds month-over-week tiers for a multi-month span', () => {
    // Arrange — ~4 month window (Sep–Dec).
    const scale = createTimeScale(start, start + 120 * DAY, 1200)
    // Act
    const { upper, lower } = scale.tiers()
    // Assert — upper tier is months; lower has more cells than upper.
    expect(upper.length).toBeGreaterThanOrEqual(4)
    expect(lower.length).toBeGreaterThan(upper.length)
    expect(/\w+ \d{4}/.test(upper[0].label)).toBe(true) // e.g. "January 2026"
  })

  it('builds year-over-month tiers for a multi-year span', () => {
    // Arrange — ~2 year window.
    const scale = createTimeScale(start, start + 730 * DAY, 1200)
    // Act
    const { upper, lower } = scale.tiers()
    // Assert — upper tier labels are 4-digit years.
    expect(upper.every((s) => /^\d{4}$/.test(s.label))).toBe(true)
    expect(lower.length).toBeGreaterThan(upper.length)
  })

  it('builds month-over-day tiers when zoomed in (high pixel density)', () => {
    // Arrange — same 4-month domain, but a very wide chart (zoomed in) so each
    // day renders >= 25px. Tiers should switch to day-level lower labels.
    const scale = createTimeScale(start, start + 120 * DAY, 120 * 30)
    // Act
    const { upper, lower } = scale.tiers()
    // Assert — upper is months; lower is per-day (one cell per day, ~120).
    expect(/\w+ \d{4}/.test(upper[0].label)).toBe(true)
    expect(lower.length).toBeGreaterThanOrEqual(110)
  })

  it('drives tier granularity by zoom (pixel density), not domain span alone', () => {
    // Arrange — identical domain span, two different widths (zoom levels).
    const zoomedOut = createTimeScale(start, start + 120 * DAY, 200)
    const zoomedIn = createTimeScale(start, start + 120 * DAY, 120 * 30)
    // Act
    const out = zoomedOut.tiers()
    const inn = zoomedIn.tiers()
    // Assert — zoomed out is coarser (fewer lower cells) than zoomed in.
    expect(inn.lower.length).toBeGreaterThan(out.lower.length)
  })

  it('clamps the first and last segments to the domain', () => {
    // Arrange — start mid-month so the first month cell is partial.
    const midMonth = dayjs('2026-01-10').startOf('day').valueOf()
    const scale = createTimeScale(midMonth, midMonth + 90 * DAY, 1000)
    // Act
    const { upper } = scale.tiers()
    // Assert — first segment starts at the domain start, not the month start.
    expect(upper[0].startMs).toBe(midMonth)
    expect(upper[upper.length - 1].endMs).toBe(midMonth + 90 * DAY)
  })
})
