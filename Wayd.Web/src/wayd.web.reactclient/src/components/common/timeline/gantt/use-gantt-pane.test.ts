import { computeGanttDomain, pxPerMsFor, toMs } from './use-gantt-pane'
import type { GanttAccessors } from './types'

const DAY = 86_400_000
const PAD = 14 * DAY
const day = (n: number) => n * DAY

// A minimal tree node for tests — the engine only ever sees it through accessors.
interface Node {
  id: string
  start?: number
  end?: number
  children?: Node[]
}

const accessors: GanttAccessors<Node> = {
  id: (n) => n.id,
  children: (n) => n.children,
  name: (n) => n.id,
  range: (n) =>
    n.start != null && n.end != null ? [n.start, n.end] : undefined,
}

describe('computeGanttDomain', () => {
  it('spans every dated row in the tree, padded on both sides', () => {
    // Arrange — two leaves whose union is day 0 → day 10.
    const roots: Node[] = [
      { id: 'a', start: day(0), end: day(5) },
      { id: 'b', start: day(3), end: day(10) },
    ]
    // Act
    const domain = computeGanttDomain(roots, accessors)
    // Assert
    expect(domain.domainStart).toBe(day(0) - PAD)
    expect(domain.domainEnd).toBe(day(10) + PAD)
  })

  it('descends into children to find dates', () => {
    // Arrange — the parent is undated; only the nested child carries a range.
    const roots: Node[] = [
      { id: 'p', children: [{ id: 'c', start: day(2), end: day(8) }] },
    ]
    // Act
    const domain = computeGanttDomain(roots, accessors)
    // Assert
    expect(domain.domainStart).toBe(day(2) - PAD)
    expect(domain.domainEnd).toBe(day(8) + PAD)
  })

  it('widens past the hint so no bar is clipped off the axis', () => {
    // Arrange — an item extends beyond both ends of the declared window.
    const roots: Node[] = [{ id: 'a', start: day(-5), end: day(40) }]
    // Act — hint is a narrower window (day 0 → day 30).
    const domain = computeGanttDomain(roots, accessors, [day(0), day(30)])
    // Assert — the domain grows to cover the item, not the hint.
    expect(domain.domainStart).toBe(day(-5) - PAD)
    expect(domain.domainEnd).toBe(day(40) + PAD)
  })

  it('honors the hint when it is wider than the items', () => {
    // Arrange — a single short item inside a much wider declared window.
    const roots: Node[] = [{ id: 'a', start: day(10), end: day(12) }]
    // Act
    const domain = computeGanttDomain(roots, accessors, [day(0), day(90)])
    // Assert — the window drives the axis so the planning period stays visible.
    expect(domain.domainStart).toBe(day(0) - PAD)
    expect(domain.domainEnd).toBe(day(90) + PAD)
  })

  it('falls back to a window around today when nothing is dated', () => {
    // Arrange — an undated tree and no hint (a plan with no scheduled work).
    const roots: Node[] = [{ id: 'p', children: [{ id: 'c' }] }]
    const now = Date.parse('2026-08-20T12:00:00.000Z')
    // Act
    const domain = computeGanttDomain(roots, accessors, undefined, now)
    // Assert — the axis opens near today (the chart is on by default, so an
    // empty plan must not land on a calendar window years away), and spans a
    // finite year so it still renders.
    expect(domain.domainStart).toBe(now - day(30) - PAD)
    expect(domain.domainEnd).toBe(now - day(30) + day(365) + PAD)
    expect(domain.domainStart).toBeLessThan(now)
    expect(domain.domainEnd).toBeGreaterThan(now)
  })

  it('treats a milestone (zero-length range) as a single point', () => {
    // Arrange — start === end, the shape an adapter returns for a milestone.
    const roots: Node[] = [{ id: 'm', start: day(7), end: day(7) }]
    // Act
    const domain = computeGanttDomain(roots, accessors)
    // Assert
    expect(domain.domainStart).toBe(day(7) - PAD)
    expect(domain.domainEnd).toBe(day(7) + PAD)
  })
})

describe('pxPerMsFor', () => {
  it('converts pixels-per-day to pixels-per-millisecond', () => {
    // Arrange / Act
    const pxPerMs = pxPerMsFor(6)
    // Assert — the drag hook and the chart scale must agree on this conversion.
    expect(pxPerMs).toBe(6 / DAY)
  })
})

describe('toMs', () => {
  it('returns undefined for null and undefined', () => {
    // Arrange / Act / Assert — an undated row must not become epoch 0.
    expect(toMs(null)).toBeUndefined()
    expect(toMs(undefined)).toBeUndefined()
  })

  it('returns undefined for an unparseable date', () => {
    // Arrange / Act / Assert
    expect(toMs('not-a-date')).toBeUndefined()
  })

  it('parses a Date and an ISO string to the same instant', () => {
    // Arrange
    const iso = '2026-03-15T00:00:00.000Z'
    // Act
    const fromString = toMs(iso)
    const fromDate = toMs(new Date(iso))
    // Assert
    expect(fromString).toBe(Date.parse(iso))
    expect(fromDate).toBe(Date.parse(iso))
  })
})
