import { act, renderHook } from '@testing-library/react'

import { mergeColumnVisibility, useGridState } from './use-grid-table'

describe('use-grid-table', () => {
  describe('mergeColumnVisibility', () => {
    it('lets a consumer-hidden column override a user show choice', () => {
      // Arrange / Act
      const merged = mergeColumnVisibility({ secret: false }, { secret: true })

      // Assert
      expect(merged).toEqual({ secret: false })
    })

    it('lets the user hide a column the consumer shows', () => {
      // Arrange / Act
      const merged = mergeColumnVisibility({ name: true }, { name: false })

      // Assert
      expect(merged).toEqual({ name: false })
    })

    it('keeps consumer-shown columns visible absent a user choice', () => {
      // Arrange / Act
      const merged = mergeColumnVisibility(
        { name: true, secret: false },
        { team: false },
      )

      // Assert
      expect(merged).toEqual({ name: true, secret: false, team: false })
    })
  })

  describe('useGridState', () => {
    it('resetColumnState restores sizing, user visibility, and pinning only', () => {
      // Arrange — user-adjusted column state plus an active sort and filter
      const { result } = renderHook(() => useGridState())
      act(() => {
        result.current.setColumnSizing({ name: 240 })
        result.current.setUserColumnVisibility({ team: false })
        result.current.setColumnPinning({ left: ['name'], right: [] })
        result.current.setSorting([{ id: 'name', desc: false }])
        result.current.setColumnFilters([{ id: 'name', value: 'x' }])
      })

      // Act
      act(() => {
        result.current.resetColumnState()
      })

      // Assert — column state cleared; sort/filters untouched (that's the
      // toolbar Clear button's job)
      expect(result.current.columnSizing).toEqual({})
      expect(result.current.userColumnVisibility).toEqual({})
      expect(result.current.columnPinning).toEqual({ left: [], right: [] })
      expect(result.current.sorting).toEqual([{ id: 'name', desc: false }])
      expect(result.current.columnFilters).toEqual([
        { id: 'name', value: 'x' },
      ])
    })
  })
})
