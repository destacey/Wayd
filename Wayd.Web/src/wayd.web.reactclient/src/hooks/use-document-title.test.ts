// @vitest-environment jsdom
import { renderHook } from '@testing-library/react'
import { useDocumentTitle } from '.'

describe('useDocumentTitle', () => {
  it('sets the document title on mount', () => {
    renderHook(() => useDocumentTitle('Test Title'))
    expect(document.title).toBe('Test Title')
  })

  it('restores the previous title on unmount', () => {
    const previousTitle = document.title
    const { unmount } = renderHook(() => useDocumentTitle('New Title'))
    unmount()
    expect(document.title).toBe(previousTitle)
  })

  it('uses the default title on unmount if provided', () => {
    const { unmount } = renderHook(() =>
      useDocumentTitle('Temporary Title', 'Default Title'),
    )
    unmount()
    expect(document.title).toBe('Default Title')
  })

  it('updates the title when the parameter changes', () => {
    const { rerender } = renderHook(({ title }) => useDocumentTitle(title), {
      initialProps: { title: 'Initial Title' },
    })

    expect(document.title).toBe('Initial Title')

    rerender({ title: 'Updated Title' })
    expect(document.title).toBe('Updated Title')
  })
})
