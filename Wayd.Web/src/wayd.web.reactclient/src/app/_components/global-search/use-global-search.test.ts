// @vitest-environment jsdom
import { act, renderHook } from '@testing-library/react'
import { useGlobalSearch } from './use-global-search'

describe('useGlobalSearch', () => {
  it('starts closed', () => {
    const { result } = renderHook(() => useGlobalSearch())
    expect(result.current.open).toBe(false)
  })

  it('opens via openSearch', () => {
    const { result } = renderHook(() => useGlobalSearch())
    act(() => result.current.openSearch())
    expect(result.current.open).toBe(true)
  })

  it('closes via closeSearch', () => {
    const { result } = renderHook(() => useGlobalSearch())
    act(() => result.current.openSearch())
    act(() => result.current.closeSearch())
    expect(result.current.open).toBe(false)
  })

  it('opens on Ctrl+K and stays open on subsequent Ctrl+K', () => {
    const { result } = renderHook(() => useGlobalSearch())
    act(() => {
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }),
      )
    })
    expect(result.current.open).toBe(true)

    // Second Ctrl+K while open does not close (modal handles it for scope switching)
    act(() => {
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }),
      )
    })
    expect(result.current.open).toBe(true)
  })

  it('toggles open on Cmd+K', () => {
    const { result } = renderHook(() => useGlobalSearch())
    act(() => {
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'k', metaKey: true }),
      )
    })
    expect(result.current.open).toBe(true)
  })

  it('does not toggle on other keys', () => {
    const { result } = renderHook(() => useGlobalSearch())
    act(() => {
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'p', ctrlKey: true }),
      )
    })
    expect(result.current.open).toBe(false)
  })

  it('removes keydown listener on unmount', () => {
    const { result, unmount } = renderHook(() => useGlobalSearch())
    unmount()
    act(() => {
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }),
      )
    })
    expect(result.current.open).toBe(false)
  })
})
