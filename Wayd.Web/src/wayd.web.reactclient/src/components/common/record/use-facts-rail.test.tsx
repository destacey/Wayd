import { renderHook, act } from '@testing-library/react'
import { Grid } from 'antd'
import { useFactsRail, FACTS_RAIL_KEY, FACTS_RAIL_WIDTH_KEY } from './use-facts-rail'
import { RecordLayoutConstants } from '@/src/config/theme/theme-constants'

jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  return {
    ...actual,
    Grid: { ...actual.Grid, useBreakpoint: jest.fn() },
  }
})

const mockUseBreakpoint = Grid.useBreakpoint as jest.MockedFunction<
  typeof Grid.useBreakpoint
>

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

// Ant's breakpoints are cumulative, so a width implies every smaller one.
const atWidth = (width: 'xl' | 'lg' | 'md' | 'sm') =>
  mockUseBreakpoint.mockReturnValue({
    xs: true,
    sm: true,
    md: width === 'md' || width === 'lg' || width === 'xl',
    lg: width === 'lg' || width === 'xl',
    xl: width === 'xl',
    xxl: false,
  } as ReturnType<typeof Grid.useBreakpoint>)

describe('useFactsRail', () => {
  beforeEach(() => {
    store.clear()
    jest.clearAllMocks()
  })

  it('reports no panel when the record has no facts', () => {
    // Arrange
    atWidth('xl')

    // Act
    const { result } = renderHook(() => useFactsRail(false))

    // Assert
    expect(result.current.mode).toBe('none')
    expect(result.current.showToggle).toBe(false)
  })

  it('starts closed so the panel costs the content no width', () => {
    // Arrange
    atWidth('xl')

    // Act
    const { result } = renderHook(() => useFactsRail(true))

    // Assert
    expect(result.current.mode).toBe('panel')
    expect(result.current.open).toBe(false)
    expect(result.current.showToggle).toBe(true)
  })

  it('opens on request', () => {
    // Arrange
    atWidth('xl')
    const { result } = renderHook(() => useFactsRail(true))

    // Act
    act(() => result.current.setOpen(true))

    // Assert
    expect(result.current.open).toBe(true)
  })

  it('offers the same panel at tablet width', () => {
    // Arrange — the panel overlays rather than taking a column, so md needs
    // no separate treatment from a wide screen.
    atWidth('md')

    // Act
    const { result } = renderHook(() => useFactsRail(true))

    // Assert
    expect(result.current.mode).toBe('panel')
    expect(result.current.showToggle).toBe(true)
  })

  it('renders inline below md, with no toggle to hide it behind', () => {
    // Arrange — nothing is dropped on mobile, it only moves.
    atWidth('sm')

    // Act
    const { result } = renderHook(() => useFactsRail(true))

    // Assert
    expect(result.current.mode).toBe('inline')
    expect(result.current.open).toBe(true)
    expect(result.current.showToggle).toBe(false)
  })

  it('remembers an open panel across records', () => {
    // Arrange
    atWidth('xl')
    const first = renderHook(() => useFactsRail(true))

    // Act — open on one record, then mount the hook again as another would.
    act(() => first.result.current.setOpen(true))
    const second = renderHook(() => useFactsRail(true))

    // Assert
    expect(second.result.current.open).toBe(true)
  })

  it('stores the preference under the shared key', () => {
    // Arrange
    atWidth('xl')
    const { result } = renderHook(() => useFactsRail(true))

    // Act
    act(() => result.current.setOpen(true))

    // Assert
    expect(window.localStorage.getItem(`${FACTS_RAIL_KEY}:v1`)).toBe('true')
  })
})

describe('useFactsRail width', () => {
  beforeEach(() => {
    store.clear()
    jest.clearAllMocks()
    atWidth('xl')
  })

  it('starts at the default width', () => {
    // Arrange / Act
    const { result } = renderHook(() => useFactsRail(true))

    // Assert
    expect(result.current.width).toBe(RecordLayoutConstants.FACTS_RAIL_WIDTH)
  })

  it('remembers a resize across records', () => {
    // Arrange
    const first = renderHook(() => useFactsRail(true))

    // Act
    act(() => first.result.current.setWidth(420))
    const second = renderHook(() => useFactsRail(true))

    // Assert
    expect(second.result.current.width).toBe(420)
  })

  it('clamps a stored width that is too wide to fit', () => {
    // Arrange — a width saved on a larger monitor, or hand-edited, must not
    // be able to squeeze the content column out.
    store.set(
      `${FACTS_RAIL_WIDTH_KEY}:v1`,
      String(RecordLayoutConstants.FACTS_RAIL_MAX_WIDTH + 400),
    )

    // Act
    const { result } = renderHook(() => useFactsRail(true))

    // Assert
    expect(result.current.width).toBe(
      RecordLayoutConstants.FACTS_RAIL_MAX_WIDTH,
    )
  })

  it('clamps a stored width that is too narrow to read', () => {
    // Arrange
    store.set(`${FACTS_RAIL_WIDTH_KEY}:v1`, '10')

    // Act
    const { result } = renderHook(() => useFactsRail(true))

    // Assert
    expect(result.current.width).toBe(
      RecordLayoutConstants.FACTS_RAIL_MIN_WIDTH,
    )
  })

  it('keeps width and open state on separate keys', () => {
    // Arrange — resizing must not close the panel, nor closing reset a width.
    const { result } = renderHook(() => useFactsRail(true))

    // Act
    act(() => result.current.setOpen(true))
    act(() => result.current.setWidth(380))

    // Assert
    expect(result.current.open).toBe(true)
    expect(result.current.width).toBe(380)
    expect(store.get(`${FACTS_RAIL_KEY}:v1`)).toBe('true')
  })
})
