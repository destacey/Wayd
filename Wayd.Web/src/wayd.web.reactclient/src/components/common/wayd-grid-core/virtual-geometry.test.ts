import { rowScaleFor, virtualSpacers } from './virtual-geometry'

const ESTIMATE = 28

describe('rowScaleFor', () => {
  test('is 1 before the real row height has been measured', () => {
    // Arrange / Act / Assert
    expect(rowScaleFor(null, ESTIMATE)).toBe(1)
  })

  test('maps the estimate onto the measured height', () => {
    // Arrange / Act — the real case: 29px rows against a 28px estimate.
    const scale = rowScaleFor(29, ESTIMATE)

    // Assert
    expect(scale).toBeCloseTo(29 / 28)
  })

  test('is 1 for a degenerate estimate', () => {
    // Arrange / Act / Assert — never divide by zero.
    expect(rowScaleFor(29, 0)).toBe(1)
  })
})

describe('virtualSpacers', () => {
  test('are zero when nothing is rendered', () => {
    // Arrange / Act
    const spacers = virtualSpacers({
      firstRowStart: 0,
      lastRowEnd: 0,
      totalSize: 0,
      rowScale: 1,
      hasRows: false,
    })

    // Assert
    expect(spacers).toEqual({ top: 0, bottom: 0 })
  })

  test('pass through unchanged when the estimate matches reality', () => {
    // Arrange / Act
    const spacers = virtualSpacers({
      firstRowStart: 280,
      lastRowEnd: 840,
      totalSize: 1260,
      rowScale: 1,
      hasRows: true,
    })

    // Assert
    expect(spacers).toEqual({ top: 280, bottom: 420 })
  })

  test('scale with the measured row height', () => {
    // Arrange — the regression: unscaled spacers put the grid's content height
    // in a different space from the chart pane's bars, so bars drifted further
    // from their rows the further the user scrolled.
    const rowScale = rowScaleFor(29, ESTIMATE)

    // Act
    const spacers = virtualSpacers({
      firstRowStart: 10 * ESTIMATE,
      lastRowEnd: 30 * ESTIMATE,
      totalSize: 45 * ESTIMATE,
      rowScale,
      hasRows: true,
    })

    // Assert — ten unrendered rows above, fifteen below, at the REAL height.
    expect(spacers.top).toBeCloseTo(10 * 29)
    expect(spacers.bottom).toBeCloseTo(15 * 29)
  })

  test('keep total content height equal to the scaled row extent', () => {
    // Arrange — the invariant that was broken: spacers plus rendered rows must
    // add up to the same height the chart canvas is laid out at.
    const rowScale = rowScaleFor(29, ESTIMATE)
    const totalRows = 45
    const renderedRows = 28
    const firstRowStart = 10 * ESTIMATE

    // Act
    const spacers = virtualSpacers({
      firstRowStart,
      lastRowEnd: firstRowStart + renderedRows * ESTIMATE,
      totalSize: totalRows * ESTIMATE,
      rowScale,
      hasRows: true,
    })
    const gridHeight = spacers.top + renderedRows * 29 + spacers.bottom
    const canvasHeight = totalRows * ESTIMATE * rowScale

    // Assert
    expect(gridHeight).toBeCloseTo(canvasHeight)
  })
})
