import { render, screen } from '@testing-library/react'
import { flexRender, type CellContext, type Row } from '@tanstack/react-table'
import type { ItemType } from 'antd/es/menu/interface'

import {
  ACTIONS_COLUMN_SIZE,
  createActionsColumn,
} from './actions-column'

interface Widget {
  id: string
  name: string
}

const WIDGET: Widget = { id: 'w1', name: 'Alpha' }

/** Render a column's `cell` for a given row via flexRender (no full table). */
const renderCell = (
  column: ReturnType<typeof createActionsColumn<Widget>>,
  original: Widget,
) => {
  const ctx = { row: { original } as Row<Widget> } as CellContext<
    Widget,
    unknown
  >
  return render(<>{flexRender(column.cell, ctx)}</>)
}

describe('createActionsColumn', () => {
  describe('column configuration', () => {
    it('applies the standardized defaults', () => {
      // Arrange / Act
      const col = createActionsColumn<Widget>({ getItems: () => [] })

      // Assert
      expect(col.id).toBe('actions')
      expect(col.size).toBe(ACTIONS_COLUMN_SIZE)
      expect(col.enableSorting).toBe(false)
      expect(col.enableColumnFilter).toBe(false)
      expect(col.enableResizing).toBe(false)
      expect(col.header).toBe('')
      // No meta.hide unless the caller opts in.
      expect(col.meta).toBeUndefined()
    })

    it('honors id, size, and hide overrides', () => {
      // Arrange / Act
      const col = createActionsColumn<Widget>({
        getItems: () => [],
        id: 'rowMenu',
        size: 72,
        hide: true,
      })

      // Assert
      expect(col.id).toBe('rowMenu')
      expect(col.size).toBe(72)
      expect(col.meta).toEqual({ hide: true })
    })
  })

  describe('cell rendering', () => {
    it('calls getItems with the row original', () => {
      // Arrange
      const getItems = jest.fn<ItemType[], [Widget]>(() => [
        { key: 'edit', label: 'Edit' },
      ])
      const col = createActionsColumn<Widget>({ getItems })

      // Act
      renderCell(col, WIDGET)

      // Assert
      expect(getItems).toHaveBeenCalledWith(WIDGET)
    })

    it('renders the ⋯ trigger button when there are items', () => {
      // Arrange
      const col = createActionsColumn<Widget>({
        getItems: () => [{ key: 'edit', label: 'Edit' }],
        ariaLabel: 'Widget actions',
      })

      // Act
      renderCell(col, WIDGET)

      // Assert
      expect(
        screen.getByRole('button', { name: 'Widget actions' }),
      ).toBeInTheDocument()
    })

    it('renders nothing when getItems returns an empty array', () => {
      // Arrange
      const col = createActionsColumn<Widget>({ getItems: () => [] })

      // Act
      const { container } = renderCell(col, WIDGET)

      // Assert
      expect(container).toBeEmptyDOMElement()
    })

    it('renders nothing when only dividers are returned (no real items)', () => {
      // Arrange — a divider-only menu has nothing to act on
      const col = createActionsColumn<Widget>({
        getItems: () => [{ key: 'd', type: 'divider' }],
      })

      // Act
      const { container } = renderCell(col, WIDGET)

      // Assert
      expect(container).toBeEmptyDOMElement()
    })
  })
})
