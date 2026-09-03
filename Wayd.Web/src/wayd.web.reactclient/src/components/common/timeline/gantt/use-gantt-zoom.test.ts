import { fitPxPerDay } from './use-gantt-zoom'
import { MAX_PX_PER_DAY, MIN_PX_PER_DAY, DAY_MS } from './use-gantt-pane'

const days = (n: number): [number, number] => [0, n * DAY_MS]

describe('fitPxPerDay', () => {
  test('returns the px/day at which the domain exactly fills the pane', () => {
    // Arrange — a 100-day domain in a 1000px pane.
    const fit = { domain: days(100), viewportWidth: 1000 }

    // Act
    const min = fitPxPerDay(fit)

    // Assert
    expect(min).toBe(10)
  })

  test('scales the floor with the pane width', () => {
    // Arrange — the bug: a fixed floor ignores the container, so a wide monitor
    // could zoom out until the chart no longer reached the right edge.
    const narrow = fitPxPerDay({ domain: days(365), viewportWidth: 800 })
    const wide = fitPxPerDay({ domain: days(365), viewportWidth: 3400 })

    // Act / Assert
    expect(wide).toBeGreaterThan(narrow)
  })

  test('keeps a three-year domain filling a 4K pane', () => {
    // Arrange — roughly the case in the report: ~3 years on a wide monitor.
    const viewportWidth = 3400
    const domain = days(365 * 3)

    // Act
    const min = fitPxPerDay({ domain, viewportWidth })

    // Assert — at the floor the rendered chart is at least the pane width.
    const renderedWidth = 365 * 3 * min
    expect(renderedWidth).toBeGreaterThanOrEqual(viewportWidth - 1)
  })

  test('never floors above the zoom-in ceiling', () => {
    // Arrange — a very short domain in a very wide pane would otherwise demand
    // more px/day than zooming in allows, stranding the clamp.
    const fit = { domain: days(2), viewportWidth: 4000 }

    // Act
    const min = fitPxPerDay(fit)

    // Assert
    expect(min).toBeLessThanOrEqual(MAX_PX_PER_DAY)
  })

  test('falls back to the fixed minimum before the pane is measured', () => {
    // Arrange / Act / Assert — width 0 on the first render, before the observer.
    expect(fitPxPerDay({ domain: days(100), viewportWidth: 0 })).toBe(
      MIN_PX_PER_DAY,
    )
  })

  test('falls back to the fixed minimum with no fit supplied', () => {
    // Arrange / Act / Assert — a consumer that has not opted in.
    expect(fitPxPerDay(undefined)).toBe(MIN_PX_PER_DAY)
  })

  test('falls back to the fixed minimum for a degenerate domain', () => {
    // Arrange / Act / Assert — zero-length domain must not divide by zero.
    expect(fitPxPerDay({ domain: [0, 0], viewportWidth: 1000 })).toBe(
      MIN_PX_PER_DAY,
    )
  })

  test('falls back to the fixed minimum for an inverted domain', () => {
    // Arrange / Act / Assert — end before start yields negative days.
    expect(fitPxPerDay({ domain: [DAY_MS * 10, 0], viewportWidth: 1000 })).toBe(
      MIN_PX_PER_DAY,
    )
  })
})
