import {
  exceedsBudgetAtAnyScale,
  fitScaleToBudget,
  MAX_CANVAS_AREA,
  MAX_CANVAS_SIDE,
} from './capture-limits'

describe('fitScaleToBudget', () => {
  test('leaves a modest capture at the requested scale', () => {
    // Arrange
    const width = 1280
    const height = 720

    // Act
    const scale = fitScaleToBudget(width, height, 2)

    // Assert
    expect(scale).toBe(2)
  })

  test('keeps 2x on a 4K-wide timeline of typical height', () => {
    // Arrange — the case that regressed: a maximised 4K chart, ~130 rows.
    const width = 3840
    const height = 5400

    // Act
    const scale = fitScaleToBudget(width, height, 2)

    // Assert — comfortably inside both caps, so no sharpness is given up.
    expect(scale).toBe(2)
  })

  test('reduces scale rather than exceeding the side cap', () => {
    // Arrange — tall enough that 2x would blow the per-side limit.
    const width = 1000
    const height = 12_000

    // Act
    const scale = fitScaleToBudget(width, height, 2)

    // Assert
    expect(height * scale).toBeLessThanOrEqual(MAX_CANVAS_SIDE)
  })

  test('reduces scale rather than exceeding the area cap', () => {
    // Arrange
    const width = 3840
    const height = 14_000

    // Act
    const scale = fitScaleToBudget(width, height, 2)

    // Assert
    expect(width * scale * height * scale).toBeLessThanOrEqual(MAX_CANVAS_AREA)
  })

  test('never drops below 1x, so output stays usable', () => {
    // Arrange — absurdly large; the budget alone would suggest well under 1.
    const width = 20_000
    const height = 60_000

    // Act
    const scale = fitScaleToBudget(width, height, 2)

    // Assert — the caller refuses this case outright rather than shipping an
    // unreadable thumbnail (see exceedsBudgetAtAnyScale).
    expect(scale).toBe(1)
  })

  test('returns the requested scale for a degenerate empty capture', () => {
    // Arrange / Act
    const scale = fitScaleToBudget(0, 0, 2)

    // Assert — no division by zero, no NaN.
    expect(scale).toBe(2)
  })
})

describe('exceedsBudgetAtAnyScale', () => {
  test('is false for a capture that fits comfortably', () => {
    // Arrange / Act / Assert
    expect(exceedsBudgetAtAnyScale(1280, 720)).toBe(false)
  })

  test('is false for a 4K timeline of realistic height', () => {
    // Arrange / Act / Assert — the common case must stay exportable as PNG.
    expect(exceedsBudgetAtAnyScale(3840, 12_000)).toBe(false)
  })

  test('is true when height alone passes the side cap', () => {
    // Arrange / Act — no scale >= 1 can bring this under the limit.
    const exceeds = exceedsBudgetAtAnyScale(1000, 20_000)

    // Assert
    expect(exceeds).toBe(true)
  })

  test('is true when the area passes the cap even within side limits', () => {
    // Arrange / Act / Assert
    expect(exceedsBudgetAtAnyScale(16_000, 16_000)).toBe(true)
  })

  test('is false for a degenerate empty capture', () => {
    // Arrange / Act / Assert — nothing to draw is not an overflow.
    expect(exceedsBudgetAtAnyScale(0, 0)).toBe(false)
  })
})
