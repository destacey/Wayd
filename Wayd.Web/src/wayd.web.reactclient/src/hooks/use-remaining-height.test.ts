import { renderHook, act } from '@testing-library/react'
import { useRemainingHeight } from './use-remaining-height'

// --- Helpers ---

function createMockElement(top: number): HTMLDivElement {
  const el = document.createElement('div')
  el.getBoundingClientRect = () => ({
    top,
    left: 0,
    right: 0,
    bottom: 0,
    width: 0,
    height: 0,
    x: 0,
    y: 0,
    toJSON: () => {},
  })
  return el
}

function setWindowHeight(height: number) {
  Object.defineProperty(window, 'innerHeight', {
    writable: true,
    configurable: true,
    value: height,
  })
}

// Store original values
const originalInnerHeight = window.innerHeight
const originalResizeObserver = global.ResizeObserver

// Minimal ResizeObserver mock
class MockResizeObserver {
  callback: ResizeObserverCallback
  static instances: MockResizeObserver[] = []
  constructor(callback: ResizeObserverCallback) {
    this.callback = callback
    MockResizeObserver.instances.push(this)
  }
  observe() {}
  unobserve() {}
  disconnect() {}
}

beforeAll(() => {
  global.ResizeObserver = MockResizeObserver as unknown as typeof ResizeObserver
})

afterAll(() => {
  if (originalResizeObserver) {
    global.ResizeObserver = originalResizeObserver
  } else {
    delete (global as any).ResizeObserver
  }
})

afterEach(() => {
  MockResizeObserver.instances = []
  Object.defineProperty(window, 'innerHeight', {
    writable: true,
    configurable: true,
    value: originalInnerHeight,
  })
})

describe('useRemainingHeight', () => {
  it('returns default height of 500 before the ref is attached', () => {
    const { result } = renderHook(() => useRemainingHeight())

    const [, height] = result.current
    expect(height).toBe(500)
  })

  it('calculates remaining height when ref callback is invoked', () => {
    setWindowHeight(1000)
    const el = createMockElement(200)

    const { result } = renderHook(() => useRemainingHeight())

    // Attach the element via the callback ref
    act(() => {
      result.current[0](el)
    })

    // 1000 - 200 - 50 (default offset) = 750
    expect(result.current[1]).toBe(750)
  })

  it('applies custom bottom offset', () => {
    setWindowHeight(1000)
    const el = createMockElement(200)

    const { result } = renderHook(() => useRemainingHeight(50))

    act(() => {
      result.current[0](el)
    })

    // 1000 - 200 - 50 = 750
    expect(result.current[1]).toBe(750)
  })

  it('enforces minimum height of 300', () => {
    setWindowHeight(400)
    const el = createMockElement(250)

    const { result } = renderHook(() => useRemainingHeight())

    act(() => {
      result.current[0](el)
    })

    // 400 - 250 - 50 = 100, but min is 300
    expect(result.current[1]).toBe(300)
  })

  it('recalculates on window resize', () => {
    setWindowHeight(1000)
    const el = createMockElement(100)

    const { result } = renderHook(() => useRemainingHeight())

    act(() => {
      result.current[0](el)
    })

    expect(result.current[1]).toBe(850) // 1000 - 100 - 50

    act(() => {
      setWindowHeight(800)
      window.dispatchEvent(new Event('resize'))
    })

    expect(result.current[1]).toBe(650) // 800 - 100 - 50
  })

  it('cleans up resize listener on unmount', () => {
    const removeSpy = jest.spyOn(window, 'removeEventListener')

    const { unmount } = renderHook(() => useRemainingHeight())
    unmount()

    expect(removeSpy).toHaveBeenCalledWith('resize', expect.any(Function))
    removeSpy.mockRestore()
  })

  it('retains last height when ref is detached', () => {
    setWindowHeight(1000)
    const el = createMockElement(200)

    const { result } = renderHook(() => useRemainingHeight())

    act(() => {
      result.current[0](el)
    })

    expect(result.current[1]).toBe(750)

    // Detach
    act(() => {
      result.current[0](null)
    })

    // Height remains at last calculated value (no reset to default)
    // The important thing is it doesn't crash
    expect(result.current[1]).toBe(750)
  })
})
