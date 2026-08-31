import { renderHook, act } from '@testing-library/react'
import { useGanttVisibility, GANTT_VISIBILITY_KEYS } from './use-gantt-visibility'

// The global setup stubs localStorage with bare jest.fn()s and no backing
// store, so nothing survives a write. Persistence is the point of this hook,
// so give it a real store for this suite only.
const store = new Map<string, string>()

beforeAll(() => {
  Object.defineProperty(window, 'localStorage', {
    configurable: true,
    value: {
      getItem: (k: string) => store.get(k) ?? null,
      setItem: (k: string, v: string) => void store.set(k, v),
      removeItem: (k: string) => void store.delete(k),
      clear: () => store.clear(),
      key: (i: number) => [...store.keys()][i] ?? null,
      get length() {
        return store.size
      },
    },
  })
})

beforeEach(() => store.clear())

// The value the hook actually writes, so assertions cover the stored shape.
const stored = (area: keyof typeof GANTT_VISIBILITY_KEYS) =>
  store.get(`${GANTT_VISIBILITY_KEYS[area]}:v1`)

describe('useGanttVisibility', () => {
  it('defaults to visible when nothing is stored', () => {
    // Arrange / Act
    const { result } = renderHook(() => useGanttVisibility('roadmap'))
    // Assert
    expect(result.current.visible).toBe(true)
  })

  it('persists a hidden preference and restores it on the next mount', () => {
    // Arrange
    const { result, unmount } = renderHook(() => useGanttVisibility('roadmap'))
    // Act
    act(() => result.current.toggle())
    unmount()
    const remounted = renderHook(() => useGanttVisibility('roadmap'))
    // Assert
    expect(stored('roadmap')).toBe('false')
    expect(remounted.result.current.visible).toBe(false)
  })

  it('restores a preference turned back on', () => {
    // Arrange — start from a stored "hidden".
    store.set(`${GANTT_VISIBILITY_KEYS.roadmap}:v1`, 'false')
    const { result, unmount } = renderHook(() => useGanttVisibility('roadmap'))
    // Act
    act(() => result.current.toggle())
    unmount()
    const remounted = renderHook(() => useGanttVisibility('roadmap'))
    // Assert
    expect(remounted.result.current.visible).toBe(true)
  })

  it('keeps the roadmap and project plan preferences separate', () => {
    // Arrange
    const roadmap = renderHook(() => useGanttVisibility('roadmap'))
    const plan = renderHook(() => useGanttVisibility('project-plan'))
    // Act — hide the roadmap chart only.
    act(() => roadmap.result.current.toggle())
    // Assert
    expect(roadmap.result.current.visible).toBe(false)
    expect(plan.result.current.visible).toBe(true)
    expect(stored('roadmap')).toBe('false')
    expect(stored('project-plan')).toBe('true')
  })

  it('shares one preference across every record in an area', () => {
    // Arrange — two grids in the same area, as two roadmaps would mount.
    const first = renderHook(() => useGanttVisibility('roadmap'))
    // Act — hide it on the first, then mount the second.
    act(() => first.result.current.toggle())
    first.unmount()
    const second = renderHook(() => useGanttVisibility('roadmap'))
    // Assert
    expect(second.result.current.visible).toBe(false)
  })
})
