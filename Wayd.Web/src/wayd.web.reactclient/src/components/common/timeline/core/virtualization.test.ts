import { getVisibleRange, getTotalHeight, type RowBounds } from './virtualization'

// 10 rows, 50px tall each, stacked from top: tops 0,50,100,...,450.
const rows: RowBounds[] = Array.from({ length: 10 }, (_, i) => ({
  top: i * 50,
  height: 50,
}))

describe('getVisibleRange', () => {
  it('returns an empty range for no rows', () => {
    // Arrange / Act
    const result = getVisibleRange([], 0, 200)
    // Assert
    expect(result.endIndex).toBe(-1)
  })

  it('returns an empty range for a zero-height viewport', () => {
    // Arrange / Act
    const result = getVisibleRange(rows, 0, 0)
    // Assert
    expect(result.endIndex).toBe(-1)
  })

  it('includes only intersecting rows when overscan is 0', () => {
    // Arrange — viewport [100,200) covers rows 2 and 3.
    // Act
    const result = getVisibleRange(rows, 100, 100, 0)
    // Assert
    expect(result.startIndex).toBe(2)
    expect(result.endIndex).toBe(3)
  })

  it('applies overscan on both sides, clamped to bounds', () => {
    // Arrange — viewport covers rows 2-3; overscan 2 -> rows 0..5.
    // Act
    const result = getVisibleRange(rows, 100, 100, 2)
    // Assert
    expect(result.startIndex).toBe(0)
    expect(result.endIndex).toBe(5)
  })

  it('clamps to the last row at the bottom of the list', () => {
    // Arrange — scrolled to the very bottom.
    // Act
    const result = getVisibleRange(rows, 400, 100, 0)
    // Assert
    expect(result.startIndex).toBe(8)
    expect(result.endIndex).toBe(9)
  })

  it('treats negative scrollTop as zero', () => {
    // Arrange / Act
    const result = getVisibleRange(rows, -50, 100, 0)
    // Assert
    expect(result.startIndex).toBe(0)
    expect(result.endIndex).toBe(1)
  })

  it('counts a partially-visible row as visible', () => {
    // Arrange — viewport [25,75) clips row 0 (0-50) and row 1 (50-100).
    // Act
    const result = getVisibleRange(rows, 25, 50, 0)
    // Assert
    expect(result.startIndex).toBe(0)
    expect(result.endIndex).toBe(1)
  })
})

describe('getTotalHeight', () => {
  it('is zero for no rows', () => {
    // Arrange / Act / Assert
    expect(getTotalHeight([])).toBe(0)
  })

  it('is the bottom edge of the last row', () => {
    // Arrange / Act / Assert — last row top 450 + height 50.
    expect(getTotalHeight(rows)).toBe(500)
  })
})
