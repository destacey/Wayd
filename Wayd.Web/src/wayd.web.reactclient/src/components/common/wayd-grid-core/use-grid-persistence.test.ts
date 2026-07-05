import { act, renderHook } from '@testing-library/react'

import {
  GRID_PERSISTENCE_ENABLED_KEY,
  clearAllGridColumnState,
  gridStateStorageKey,
  useGridColumnStatePersistence,
} from './use-grid-persistence'
import { useGridState } from './use-grid-table'

/** Composes the state hook with persistence exactly as WaydGridInner does. */
const useHarness = (persistStateKey?: string) => {
  const gridState = useGridState()
  useGridColumnStatePersistence(gridState, persistStateKey)
  return gridState
}

const STORAGE_KEY = gridStateStorageKey('test-grid')

describe('use-grid-persistence', () => {
  beforeAll(() => {
    console.error = jest.fn()
  })

  beforeEach(() => {
    // jest.setup's localStorage stub has no backing store — replace it with a
    // real in-memory implementation (same pattern as use-local-storage-state.test.ts)
    const localStorageMock = (() => {
      let store: Record<string, string> = {}
      return {
        getItem: (key: string) => store[key] ?? null,
        setItem: (key: string, value: string) => {
          store[key] = value
        },
        removeItem: (key: string) => {
          delete store[key]
        },
        clear: () => {
          store = {}
        },
        get length() {
          return Object.keys(store).length
        },
        key: (index: number) => {
          const keys = Object.keys(store)
          return keys[index] ?? null
        },
      }
    })()

    Object.defineProperty(window, 'localStorage', {
      value: localStorageMock,
      writable: true,
    })

    jest.useFakeTimers()
  })

  afterEach(() => {
    jest.useRealTimers()
  })

  describe('useGridColumnStatePersistence', () => {
    it('never touches storage without a persist key', () => {
      // Arrange
      const getItemSpy = jest.spyOn(window.localStorage, 'getItem')
      const setItemSpy = jest.spyOn(window.localStorage, 'setItem')

      // Act
      const { result } = renderHook(() => useHarness(undefined))
      act(() => {
        result.current.setColumnSizing({ name: 240 })
        jest.advanceTimersByTime(1000)
      })

      // Assert
      expect(getItemSpy).not.toHaveBeenCalled()
      expect(setItemSpy).not.toHaveBeenCalled()
    })

    it('keeps defaults and writes nothing when no entry is stored', () => {
      // Arrange
      const setItemSpy = jest.spyOn(window.localStorage, 'setItem')

      // Act
      const { result } = renderHook(() => useHarness('test-grid'))
      act(() => {
        jest.advanceTimersByTime(1000)
      })

      // Assert — pristine grids leave no localStorage residue
      expect(result.current.columnSizing).toEqual({})
      expect(result.current.userColumnVisibility).toEqual({})
      expect(result.current.columnPinning).toEqual({ left: [], right: [] })
      expect(setItemSpy).not.toHaveBeenCalled()
    })

    it('applies a stored entry to all three slices on mount', () => {
      // Arrange
      window.localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          columnSizing: { name: 240 },
          userColumnVisibility: { team: false },
          columnPinning: { left: ['key'], right: [] },
        }),
      )

      // Act
      const { result } = renderHook(() => useHarness('test-grid'))

      // Assert
      expect(result.current.columnSizing).toEqual({ name: 240 })
      expect(result.current.userColumnVisibility).toEqual({ team: false })
      expect(result.current.columnPinning).toEqual({
        left: ['key'],
        right: [],
      })
    })

    it('does not rewrite an unchanged loaded entry', () => {
      // Arrange
      window.localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          columnSizing: { name: 240 },
          userColumnVisibility: {},
          columnPinning: { left: [], right: [] },
        }),
      )
      const setItemSpy = jest.spyOn(window.localStorage, 'setItem')

      // Act
      renderHook(() => useHarness('test-grid'))
      act(() => {
        jest.advanceTimersByTime(1000)
      })

      // Assert — applying the loaded state must not trigger a save
      expect(setItemSpy).not.toHaveBeenCalled()
    })

    it('debounces rapid changes into a single write of the full payload', () => {
      // Arrange
      const setItemSpy = jest.spyOn(window.localStorage, 'setItem')
      const { result } = renderHook(() => useHarness('test-grid'))

      // Act — simulate a resize drag: many sizing updates in quick succession
      act(() => {
        result.current.setColumnSizing({ name: 210 })
      })
      act(() => {
        result.current.setColumnSizing({ name: 230 })
      })
      act(() => {
        result.current.setColumnSizing({ name: 250 })
        result.current.setColumnPinning({ left: ['key'], right: [] })
      })
      act(() => {
        jest.advanceTimersByTime(1000)
      })

      // Assert
      expect(setItemSpy).toHaveBeenCalledTimes(1)
      expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
        columnSizing: { name: 250 },
        userColumnVisibility: {},
        columnPinning: { left: ['key'], right: [] },
      })
    })

    it('persists a visibility-only change (chooser toggle path)', () => {
      // Arrange
      const { result } = renderHook(() => useHarness('test-grid'))

      // Act — exactly what WaydGrid's handleUserToggleColumn does
      act(() => {
        result.current.setUserColumnVisibility((prev) => ({
          ...prev,
          isActive: false,
        }))
      })
      act(() => {
        jest.advanceTimersByTime(1000)
      })

      // Assert
      expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
        columnSizing: {},
        userColumnVisibility: { isActive: false },
        columnPinning: { left: [], right: [] },
      })
    })

    it('removes the entry when resetColumnState restores the defaults', () => {
      // Arrange
      window.localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          columnSizing: { name: 240 },
          userColumnVisibility: { team: false },
          columnPinning: { left: ['key'], right: [] },
        }),
      )
      const { result } = renderHook(() => useHarness('test-grid'))

      // Act
      act(() => {
        result.current.resetColumnState()
      })
      act(() => {
        jest.advanceTimersByTime(1000)
      })

      // Assert — reset doubles as "delete the stored layout"
      expect(window.localStorage.getItem(STORAGE_KEY)).toBeNull()
    })

    it('removes stale versioned and unversioned entries for the grid on load', () => {
      // Arrange
      window.localStorage.setItem('wayd-grid:test-grid', '{"old":true}')
      window.localStorage.setItem('wayd-grid:test-grid:v0', '{"old":true}')
      window.localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          columnSizing: { name: 240 },
          userColumnVisibility: {},
          columnPinning: { left: [], right: [] },
        }),
      )
      window.localStorage.setItem('wayd-grid:other-grid:v1', '{}')

      // Act
      renderHook(() => useHarness('test-grid'))

      // Assert — only this grid's stale versions are cleaned
      expect(window.localStorage.getItem('wayd-grid:test-grid')).toBeNull()
      expect(window.localStorage.getItem('wayd-grid:test-grid:v0')).toBeNull()
      expect(window.localStorage.getItem(STORAGE_KEY)).not.toBeNull()
      expect(
        window.localStorage.getItem('wayd-grid:other-grid:v1'),
      ).not.toBeNull()
    })

    it.each([
      ['malformed JSON', 'not-json{'],
      ['wrong shape', JSON.stringify({ columnSizing: 'nope' })],
      [
        'wrong slice value types',
        JSON.stringify({
          columnSizing: { name: 'wide' },
          userColumnVisibility: {},
          columnPinning: { left: [], right: [] },
        }),
      ],
    ])('ignores a stored entry with %s', (_label, raw) => {
      // Arrange
      window.localStorage.setItem(STORAGE_KEY, raw)

      // Act
      const { result } = renderHook(() => useHarness('test-grid'))

      // Assert — defaults retained, no throw
      expect(result.current.columnSizing).toEqual({})
      expect(result.current.userColumnVisibility).toEqual({})
      expect(result.current.columnPinning).toEqual({ left: [], right: [] })
    })

    it('flushes a pending debounced write on unmount', () => {
      // Arrange
      const { result, unmount } = renderHook(() => useHarness('test-grid'))
      act(() => {
        result.current.setColumnSizing({ name: 300 })
      })

      // Act — unmount before the debounce elapses (navigation away)
      unmount()

      // Assert
      expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
        columnSizing: { name: 300 },
        userColumnVisibility: {},
        columnPinning: { left: [], right: [] },
      })
    })

    it('neither loads nor saves while persistence is disabled, keeping stored entries', () => {
      // Arrange
      window.localStorage.setItem(GRID_PERSISTENCE_ENABLED_KEY, 'false')
      const storedJson = JSON.stringify({
        columnSizing: { name: 240 },
        userColumnVisibility: {},
        columnPinning: { left: [], right: [] },
      })
      window.localStorage.setItem(STORAGE_KEY, storedJson)

      // Act
      const { result, unmount } = renderHook(() => useHarness('test-grid'))
      act(() => {
        result.current.setColumnSizing({ name: 999 })
      })
      act(() => {
        jest.advanceTimersByTime(1000)
      })
      unmount()

      // Assert — defaults were not overwritten in-memory, and the stored
      // entry survives untouched for when the user re-enables the feature
      expect(result.current.columnSizing).toEqual({ name: 999 })
      expect(window.localStorage.getItem(STORAGE_KEY)).toBe(storedJson)
    })
  })

  describe('clearAllGridColumnState', () => {
    it('removes every grid entry but keeps the enabled flag and unrelated keys', () => {
      // Arrange
      window.localStorage.setItem('wayd-grid:one:v1', '{}')
      window.localStorage.setItem('wayd-grid:two:v1', '{}')
      window.localStorage.setItem(GRID_PERSISTENCE_ENABLED_KEY, 'false')
      window.localStorage.setItem('appTheme', '"dark"')

      // Act
      clearAllGridColumnState()

      // Assert
      expect(window.localStorage.getItem('wayd-grid:one:v1')).toBeNull()
      expect(window.localStorage.getItem('wayd-grid:two:v1')).toBeNull()
      expect(window.localStorage.getItem(GRID_PERSISTENCE_ENABLED_KEY)).toBe(
        'false',
      )
      expect(window.localStorage.getItem('appTheme')).toBe('"dark"')
    })
  })
})
