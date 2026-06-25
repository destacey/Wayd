import { clampZoom, maxZoom, anchoredScrollLeft } from './zoom'

describe('clampZoom', () => {
  it('returns the value when within bounds', () => {
    // Arrange
    const bounds = { min: 1, max: 10 }
    // Act
    const result = clampZoom(3, bounds)
    // Assert
    expect(result).toBe(3)
  })

  it('clamps below min up to min', () => {
    // Arrange
    const bounds = { min: 1, max: 10 }
    // Act
    const result = clampZoom(0.2, bounds)
    // Assert
    expect(result).toBe(1)
  })

  it('clamps above max down to max', () => {
    // Arrange
    const bounds = { min: 1, max: 10 }
    // Act
    const result = clampZoom(99, bounds)
    // Assert
    expect(result).toBe(10)
  })

  it('falls back to min for non-finite input', () => {
    // Arrange
    const bounds = { min: 1, max: 10 }
    // Act
    const result = clampZoom(NaN, bounds)
    // Assert
    expect(result).toBe(1)
  })
})

describe('maxZoom', () => {
  it('caps zoom so the viewport spans at least zoomMin', () => {
    // Arrange: base fits a 100-day domain into a 1000px viewport; zoomMin = 1 day.
    const day = 86_400_000
    const domainMs = 100 * day
    const baseWidth = 1000
    const viewportWidth = 1000
    // Act
    const result = maxZoom(baseWidth, viewportWidth, domainMs, day)
    // Assert: at the cap, viewportMs === zoomMin.
    //   f = viewportWidth * domainMs / (zoomMin * baseWidth) = 100.
    expect(result).toBeCloseTo(100, 5)
  })

  it('never returns less than 1 (cannot zoom out past the window)', () => {
    // Arrange: a domain smaller than zoomMin would otherwise give a cap < 1.
    const day = 86_400_000
    // Act
    const result = maxZoom(1000, 1000, day / 2, day)
    // Assert
    expect(result).toBe(1)
  })

  it('returns 1 for degenerate inputs', () => {
    // Arrange / Act / Assert
    expect(maxZoom(0, 1000, 86_400_000, 86_400_000)).toBe(1)
    expect(maxZoom(1000, 0, 86_400_000, 86_400_000)).toBe(1)
    expect(maxZoom(1000, 1000, 86_400_000, 0)).toBe(1)
  })
})

describe('anchoredScrollLeft', () => {
  it('keeps the time under the anchor fixed when zooming in', () => {
    // Arrange: anchor at viewport x=200, currently scrolled 100px into a 1000px
    // chart. The content point under the anchor is 300px (30% of the width).
    // Double the width to 2000px; that point is now at 600px, so to keep it under
    // x=200 the scrollLeft must be 400.
    // Act
    const result = anchoredScrollLeft({
      oldWidth: 1000,
      newWidth: 2000,
      oldScrollLeft: 100,
      anchorX: 200,
      viewportWidth: 500,
    })
    // Assert
    expect(result).toBe(400)
  })

  it('clamps to 0 when the anchored scroll would go negative', () => {
    // Arrange: zooming out near the start.
    // Act
    const result = anchoredScrollLeft({
      oldWidth: 2000,
      newWidth: 1000,
      oldScrollLeft: 50,
      anchorX: 100,
      viewportWidth: 500,
    })
    // Assert: contentX=150 → scaled=75 → 75-100=-25 → clamped to 0.
    expect(result).toBe(0)
  })

  it('clamps to the maximum scrollable extent', () => {
    // Arrange: a huge anchored target beyond the scrollable range.
    // Act
    const result = anchoredScrollLeft({
      oldWidth: 1000,
      newWidth: 2000,
      oldScrollLeft: 900,
      anchorX: 400,
      viewportWidth: 500,
    })
    // Assert: maxScroll = newWidth - viewportWidth = 1500; the computed value is
    //   contentX=1300 → scaled=2600 → 2600-400=2200 → clamped to 1500.
    expect(result).toBe(1500)
  })

  it('returns 0 for a degenerate old width', () => {
    // Arrange / Act
    const result = anchoredScrollLeft({
      oldWidth: 0,
      newWidth: 2000,
      oldScrollLeft: 0,
      anchorX: 100,
      viewportWidth: 500,
    })
    // Assert
    expect(result).toBe(0)
  })
})
