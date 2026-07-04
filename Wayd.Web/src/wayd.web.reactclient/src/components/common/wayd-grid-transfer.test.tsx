jest.mock('dayjs', () => jest.requireActual('dayjs'))

import { act, fireEvent, render, screen, within } from '@testing-library/react'
import type { ColumnDef } from '@tanstack/react-table'

import WaydGridTransfer, { resolveDragItems } from './wayd-grid-transfer'

interface Item {
  id: string
  name: string
  status: string
}

const LEFT_DATA: Item[] = [
  { id: 'a', name: 'Alpha', status: 'Active' },
  { id: 'b', name: 'Beta', status: 'Active' },
  { id: 'c', name: 'Gamma', status: 'Closed' },
]

const RIGHT_DATA: Item[] = [{ id: 'd', name: 'Delta', status: 'Active' }]

const columns: ColumnDef<Item, any>[] = [
  { id: 'name', accessorKey: 'name', header: 'Name' },
  { id: 'status', accessorKey: 'status', header: 'Status' },
]

type TransferProps = Partial<Parameters<typeof WaydGridTransfer<Item>>[0]>

const renderTransfer = (props: TransferProps = {}) => {
  const onMove = jest.fn()
  const onRemove = jest.fn()
  const utils = render(
    <WaydGridTransfer<Item>
      leftData={LEFT_DATA}
      rightData={RIGHT_DATA}
      columns={columns}
      getRowId={(item) => item.id}
      onMove={onMove}
      onRemove={onRemove}
      {...props}
    />,
  )
  // Each WaydGrid renders a header table (thead) and a body table (tbody):
  // [0]=left header, [1]=left body, [2]=right header, [3]=right body.
  const tables = utils.container.querySelectorAll('table')
  return {
    ...utils,
    onMove,
    onRemove,
    leftHeader: tables[0] as HTMLTableElement,
    leftTable: tables[1] as HTMLTableElement,
    rightHeader: tables[2] as HTMLTableElement,
    rightTable: tables[3] as HTMLTableElement,
  }
}

/** Body cell text for a column id within one of the two grids. */
const bodyCells = (table: HTMLTableElement, columnId: string) =>
  Array.from(
    table.querySelectorAll(`tbody td[data-column-id="${columnId}"]`),
  ).map((c) => c.textContent)

const moveButton = () =>
  screen.getByRole('button', { name: 'Move selected items' })

const rowCheckboxes = (table: HTMLTableElement) =>
  within(table).getAllByRole('checkbox', { name: 'Select row' })

describe('WaydGridTransfer', () => {
  describe('rendering', () => {
    it('renders the left and right data in their own grids', () => {
      // Arrange / Act
      const { leftTable, rightTable } = renderTransfer()

      // Assert
      expect(bodyCells(leftTable, 'name')).toEqual(['Alpha', 'Beta', 'Gamma'])
      expect(bodyCells(rightTable, 'name')).toEqual(['Delta'])
    })

    it('renders a selection checkbox per left row and a remove button per right row', () => {
      // Arrange / Act
      const { leftTable, rightTable } = renderTransfer()

      // Assert
      expect(rowCheckboxes(leftTable)).toHaveLength(3)
      expect(
        within(rightTable).getAllByRole('button', { name: 'Remove row' }),
      ).toHaveLength(1)
    })

    it('renders a drag handle per left row and none on the right', () => {
      // Arrange / Act
      const { leftTable, rightTable } = renderTransfer()

      // Assert
      expect(
        within(leftTable).getAllByRole('button', { name: 'Drag to move' }),
      ).toHaveLength(3)
      expect(
        within(rightTable).queryAllByRole('button', { name: 'Drag to move' }),
      ).toHaveLength(0)
    })
  })

  describe('resolveDragItems (drag → moved rows)', () => {
    const getRowId = (item: Item) => item.id

    it('moves only the dragged row when it is not checked', () => {
      // Arrange
      const selected = new Set(['b'])

      // Act
      const items = resolveDragItems('a', LEFT_DATA, selected, getRowId)

      // Assert
      expect(items).toEqual([LEFT_DATA[0]])
    })

    it('moves every checked row when the dragged row is checked', () => {
      // Arrange
      const selected = new Set(['a', 'c'])

      // Act
      const items = resolveDragItems('a', LEFT_DATA, selected, getRowId)

      // Assert
      expect(items).toEqual([LEFT_DATA[0], LEFT_DATA[2]])
    })

    it('ignores checked ids that are no longer in leftData', () => {
      // Arrange
      const selected = new Set(['a', 'stale-id'])

      // Act
      const items = resolveDragItems('a', LEFT_DATA, selected, getRowId)

      // Assert
      expect(items).toEqual([LEFT_DATA[0]])
    })

    it('returns nothing when the dragged id is not in leftData', () => {
      // Arrange
      const selected = new Set<string>()

      // Act
      const items = resolveDragItems('missing', LEFT_DATA, selected, getRowId)

      // Assert
      expect(items).toEqual([])
    })
  })

  describe('moving rows', () => {
    it('disables the move button while nothing is selected', () => {
      // Arrange / Act
      renderTransfer()

      // Assert
      expect(moveButton()).toBeDisabled()
    })

    it('moves the checked rows and clears the selection', () => {
      // Arrange
      const { leftTable, onMove } = renderTransfer()

      // Act — select Alpha + Gamma, then move. Re-query between clicks:
      // each selection change re-renders the grid rows with fresh DOM nodes.
      fireEvent.click(rowCheckboxes(leftTable)[0])
      fireEvent.click(rowCheckboxes(leftTable)[2])
      fireEvent.click(moveButton())

      // Assert
      expect(onMove).toHaveBeenCalledTimes(1)
      expect(onMove).toHaveBeenCalledWith([LEFT_DATA[0], LEFT_DATA[2]])
      expect(
        rowCheckboxes(leftTable).filter((c) => (c as HTMLInputElement).checked),
      ).toHaveLength(0)
      expect(moveButton()).toBeDisabled()
    })

    it('selects every displayed row via the header checkbox', () => {
      // Arrange
      const { leftHeader, onMove } = renderTransfer()

      // Act
      fireEvent.click(
        within(leftHeader).getByRole('checkbox', { name: 'Select all rows' }),
      )
      fireEvent.click(moveButton())

      // Assert
      expect(onMove).toHaveBeenCalledWith(LEFT_DATA)
    })

    it('scopes the header checkbox to the displayed rows when a filter is active', () => {
      // Arrange — filter the left grid's Name column down to 'Beta'
      jest.useFakeTimers()
      try {
        const { leftHeader, leftTable, onMove } = renderTransfer()
        const floatingRow = leftHeader.querySelector(
          'tr[data-role="floating-filters"]',
        ) as HTMLTableRowElement
        const nameFilterInput = floatingRow.querySelectorAll(
          'input[type="text"]',
        )[0] as HTMLInputElement

        // Act — type in the floating filter (debounced), then select all + move
        fireEvent.change(nameFilterInput, { target: { value: 'Beta' } })
        act(() => {
          jest.advanceTimersByTime(500)
        })
        expect(bodyCells(leftTable, 'name')).toEqual(['Beta'])
        fireEvent.click(
          within(leftHeader).getByRole('checkbox', { name: 'Select all rows' }),
        )
        fireEvent.click(moveButton())

        // Assert — only the displayed (filtered) row moves
        expect(onMove).toHaveBeenCalledWith([LEFT_DATA[1]])
      } finally {
        jest.useRealTimers()
      }
    })

    it('keeps rows checked across filtering and moves only rows still present in leftData', () => {
      // Arrange — select Alpha, then re-render without Alpha in leftData
      const { leftTable, onMove, rerender } = renderTransfer()
      fireEvent.click(rowCheckboxes(leftTable)[0])

      // Act — Alpha leaves the source list (e.g. new search results)
      rerender(
        <WaydGridTransfer<Item>
          leftData={LEFT_DATA.slice(1)}
          rightData={RIGHT_DATA}
          columns={columns}
          getRowId={(item) => item.id}
          onMove={onMove}
          onRemove={jest.fn()}
        />,
      )

      // Assert — the stale selection no longer enables the move button
      expect(moveButton()).toBeDisabled()
    })
  })

  describe('removing rows', () => {
    it('calls onRemove with the row item when its remove button is clicked', () => {
      // Arrange
      const { rightTable, onRemove } = renderTransfer()

      // Act
      fireEvent.click(
        within(rightTable).getByRole('button', { name: 'Remove row' }),
      )

      // Assert
      expect(onRemove).toHaveBeenCalledTimes(1)
      expect(onRemove).toHaveBeenCalledWith(RIGHT_DATA[0])
    })
  })
})
