// @vitest-environment jsdom
import { renderHook, act } from '@testing-library/react'
import { useLocalStorageState } from '.'

describe('useLocalStorageState', () => {
  it('returns the default value if key does not exist', () => {
    const { result } = renderHook(() => useLocalStorageState('key', 'default'))
    expect(result.current[0]).toBe('default')
  })

  it('reads the stored value from localStorage', () => {
    localStorage.setItem('key', JSON.stringify('storedValue'))
    const { result } = renderHook(() => useLocalStorageState('key', 'default'))
    expect(result.current[0]).toBe('storedValue')
  })

  it('updates localStorage when value changes', () => {
    const { result } = renderHook(() => useLocalStorageState('key', 'default'))
    act(() => result.current[1]('newValue'))
    expect(localStorage.getItem('key')).toBe(JSON.stringify('newValue'))
  })

  it('handles invalid JSON gracefully', () => {
    localStorage.setItem('key', 'invalidJSON')
    const { result } = renderHook(() => useLocalStorageState('key', 'default'))
    expect(result.current[0]).toBe('default')
  })

  it('handles stored "undefined" string gracefully', () => {
    localStorage.setItem('key', 'undefined')
    const { result } = renderHook(() => useLocalStorageState('key', 'default'))
    expect(result.current[0]).toBe('default')
  })

  it('does not write undefined to localStorage and clears existing value', () => {
    const { result } = renderHook(() =>
      useLocalStorageState<string | undefined>('key', undefined),
    )
    // undefined is not valid JSON, so it should not be stored
    expect(localStorage.getItem('key')).toBeNull()

    // Set a value, then clear it with undefined
    act(() => result.current[1]('hello'))
    expect(localStorage.getItem('key')).toBe('"hello"')
    act(() => result.current[1](undefined))
    expect(localStorage.getItem('key')).toBeNull()
  })

  it('avoids redundant updates to localStorage', () => {
    localStorage.setItem('key', JSON.stringify('value'))
    const { result } = renderHook(() => useLocalStorageState('key', 'value'))
    const setItemSpy = vi.spyOn(Storage.prototype, 'setItem')

    act(() => result.current[1]('value'))
    expect(setItemSpy).not.toHaveBeenCalled()
  })

  describe('versioning', () => {
    it('uses versioned key when version is provided', () => {
      const { result } = renderHook(() =>
        useLocalStorageState('key', 'default', { version: 1 }),
      )
      act(() => result.current[1]('newValue'))
      expect(localStorage.getItem('key:v1')).toBe(JSON.stringify('newValue'))
    })

    it('reads from versioned key when version is provided', () => {
      localStorage.setItem('key:v1', JSON.stringify('storedValue'))
      const { result } = renderHook(() =>
        useLocalStorageState('key', 'default', { version: 1 }),
      )
      expect(result.current[0]).toBe('storedValue')
    })

    it('returns default value when versioned key does not exist', () => {
      const { result } = renderHook(() =>
        useLocalStorageState('key', 'default', { version: 2 }),
      )
      expect(result.current[0]).toBe('default')
    })

    it('cleans up old versions when version changes', () => {
      // Set up old version
      localStorage.setItem('key:v1', JSON.stringify('oldValue'))
      localStorage.setItem('key:v2', JSON.stringify('olderValue'))

      // Use new version
      renderHook(() => useLocalStorageState('key', 'default', { version: 3 }))

      // Old versions should be cleaned up
      expect(localStorage.getItem('key:v1')).toBeNull()
      expect(localStorage.getItem('key:v2')).toBeNull()
      expect(localStorage.getItem('key:v3')).toBe(JSON.stringify('default'))
    })

    it('maintains backward compatibility without version', () => {
      localStorage.setItem('key', JSON.stringify('storedValue'))
      const { result } = renderHook(() =>
        useLocalStorageState('key', 'default'),
      )
      expect(result.current[0]).toBe('storedValue')
      expect(localStorage.getItem('key')).toBe(JSON.stringify('storedValue'))
    })

    it('does not interfere with non-versioned keys', () => {
      localStorage.setItem('key', JSON.stringify('nonVersioned'))
      localStorage.setItem('key:v1', JSON.stringify('versioned'))

      // Non-versioned usage
      const { result: nonVersioned } = renderHook(() =>
        useLocalStorageState('key', 'default'),
      )
      expect(nonVersioned.current[0]).toBe('nonVersioned')

      // Versioned usage
      const { result: versioned } = renderHook(() =>
        useLocalStorageState('key', 'default', { version: 1 }),
      )
      expect(versioned.current[0]).toBe('versioned')
    })
  })
})
