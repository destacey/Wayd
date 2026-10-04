import { renderHook, act } from '@testing-library/react'
import { keepValidCodes, useStatusFilter } from './use-status-filter'
import { ProjectStatus } from '@/src/services/wayd-api'

const { Proposed, Approved, Active, Completed } = ProjectStatus

const DEFAULTS = [Approved, Active]
const KEY = 'portfolio:12:projectStatus'
const STORAGE_KEY = `wayd-ppm-filter:${KEY}:v2`

const renderFilter = (key = KEY) =>
  renderHook(() => useStatusFilter(key, DEFAULTS))

describe('useStatusFilter', () => {
  // The global setup stubs localStorage with bare jest.fn()s and no backing
  // store, so nothing written is ever readable. These tests are about what
  // survives a remount, so they need storage that actually stores.
  beforeEach(() => {
    const store = new Map<string, string>()

    Object.defineProperty(window, 'localStorage', {
      value: {
        getItem: (key: string) => store.get(key) ?? null,
        setItem: (key: string, value: string) => void store.set(key, value),
        removeItem: (key: string) => void store.delete(key),
        clear: () => store.clear(),
        get length() {
          return store.size
        },
        key: (index: number) => [...store.keys()][index] ?? null,
      },
      writable: true,
    })
  })

  it('starts at the defaults when nothing is remembered', () => {
    // Arrange / Act
    const { result } = renderFilter()

    // Assert
    expect(result.current.selected).toEqual([Approved, Active])
  })

  it('remembers a selection across mounts, so it survives a refresh', () => {
    // Arrange
    const first = renderFilter()

    // Act
    act(() => first.result.current.setSelected([Proposed, Completed]))
    first.unmount()
    const second = renderFilter()

    // Assert
    expect(second.result.current.selected).toEqual([Proposed, Completed])
  })

  it('keeps an empty selection, which means every status', () => {
    // Arrange — distinct from the defaults, which are a narrower set, so it
    // must not be mistaken for "nothing remembered" on the way back in.
    const first = renderFilter()

    // Act
    act(() => first.result.current.setSelected([]))
    first.unmount()
    const second = renderFilter()

    // Assert
    expect(second.result.current.selected).toEqual([])
  })

  it('remembers each record separately', () => {
    // Arrange
    const portfolioTwelve = renderFilter('portfolio:12:projectStatus')

    // Act
    act(() => portfolioTwelve.result.current.setSelected([Proposed]))
    const portfolioThirteen = renderFilter('portfolio:13:projectStatus')

    // Assert — filtering one portfolio must not change what another opens on.
    expect(portfolioThirteen.result.current.selected).toEqual([
      Approved,
      Active,
    ])
  })

  it('keeps each collection on a record separate', () => {
    // Arrange
    const programs = renderFilter('portfolio:12:programStatus')

    // Act
    act(() => programs.result.current.setSelected([Proposed]))
    const projects = renderFilter('portfolio:12:projectStatus')

    // Assert
    expect(projects.result.current.selected).toEqual([Approved, Active])
  })

  it('namespaces what it stores, so it cannot collide with other state', () => {
    // Arrange
    const { result } = renderFilter()

    // Act
    act(() => result.current.setSelected([Proposed, Completed]))

    // Assert
    expect(window.localStorage.getItem(STORAGE_KEY)).toEqual(
      '["Proposed","Completed"]',
    )
  })

  it('drops a selection stored as numeric ids for the defaults', () => {
    // Arrange — what the filter stored when it held status ids.
    window.localStorage.setItem(`wayd-ppm-filter:${KEY}:v1`, '[1,3]')

    // Act
    const { result } = renderFilter()

    // Assert
    expect(result.current.selected).toEqual([Approved, Active])
    expect(window.localStorage.getItem(`wayd-ppm-filter:${KEY}:v1`)).toBeNull()
  })
})

describe('keepValidCodes', () => {
  it('keeps a selection of valid codes as it is', () => {
    // Arrange / Act
    const kept = keepValidCodes([Proposed, Completed], ProjectStatus, DEFAULTS)

    // Assert
    expect(kept).toEqual([Proposed, Completed])
  })

  it('keeps an empty selection, which means every status', () => {
    // Arrange / Act
    const kept = keepValidCodes([], ProjectStatus, DEFAULTS)

    // Assert
    expect(kept).toEqual([])
  })

  it('falls back to the defaults for a selection stored as numeric ids', () => {
    // Arrange / Act — dropping every id would leave the empty selection, which reads as all.
    const kept = keepValidCodes([5, 2], ProjectStatus, DEFAULTS)

    // Assert
    expect(kept).toEqual([Approved, Active])
  })

  it('drops only the values that are not codes', () => {
    // Arrange / Act
    const kept = keepValidCodes([Active, 3, 'Unknown'], ProjectStatus, DEFAULTS)

    // Assert
    expect(kept).toEqual([Active])
  })
})
