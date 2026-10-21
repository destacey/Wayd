import { getDrawerWidthPixels, isFramed } from './window-utils'

describe('getDrawerWidthPixels', () => {
  beforeEach(() => {
    // Reset the window.innerWidth before each test
    Object.defineProperty(window, 'innerWidth', {
      writable: true,
      configurable: true,
      value: 1024,
    })
  })

  it('should return 30% in pixels for window width >= 1500', () => {
    window.innerWidth = 1500
    expect(getDrawerWidthPixels()).toBe(450) // 1500 * 0.3
  })

  it('should return 35% in pixels for window width >= 1300 and < 1500', () => {
    window.innerWidth = 1300
    expect(getDrawerWidthPixels()).toBe(454) // Math.floor(1300 * 0.35) = 454 (due to floating-point precision)
  })

  it('should return 40% in pixels for window width >= 1100 and < 1300', () => {
    window.innerWidth = 1100
    expect(getDrawerWidthPixels()).toBe(440) // 1100 * 0.4
  })

  it('should return 50% in pixels for window width >= 900 and < 1100', () => {
    window.innerWidth = 900
    expect(getDrawerWidthPixels()).toBe(450) // 900 * 0.5
  })

  it('should return 80% in pixels for window width < 900', () => {
    window.innerWidth = 800
    expect(getDrawerWidthPixels()).toBe(640) // 800 * 0.8
  })
})

describe('isFramed', () => {
  it('is false for a page loaded as the top-level document', () => {
    // Act
    const result = isFramed()

    // Assert
    expect(result).toBe(false)
  })
})
