jest.mock('dayjs', () => jest.requireActual('dayjs'))

import { createRef } from 'react'
import { render, screen, fireEvent, act } from '@testing-library/react'
import type { ColumnDef } from '../wayd-grid-core'
import WaydGrid from './wayd-grid'
import type { WaydGridColumnMeta, WaydGridHandle, WaydGridProps } from './types'
import { textEditor } from '../wayd-grid-core/cell-editing/editors'

interface Node {
  id: string
  name: string
  children: Node[]
}

const node = (id: string, name: string, children: Node[] = []): Node => ({
  id,
  name,
  children,
})

// Alpha
//   Alpha 1
//   Alpha 2
//   Alpha 3
// Beta
const TREE: Node[] = [
  node('a', 'Alpha', [
    node('a1', 'Alpha 1'),
    node('a2', 'Alpha 2', [node('a2x', 'Alpha 2 child')]),
    node('a3', 'Alpha 3'),
  ]),
  node('b', 'Beta'),
]

const columns: ColumnDef<Node, unknown>[] = [
  {
    id: 'name',
    accessorKey: 'name',
    header: 'Name',
    meta: {
      treeColumn: true,
      editor: textEditor<Node>({
        field: 'name',
        get: (row) => row.name,
        apply: (row, name) => ({ ...row, name }),
      }),
    } satisfies WaydGridColumnMeta,
  },
]

const renderTree = (
  props: Partial<WaydGridProps<Node>> & {
    ref?: React.Ref<WaydGridHandle>
  } = {},
) =>
  render(
    <WaydGrid<Node>
      data={TREE}
      columns={columns}
      includeIdColumn={false}
      getSubRows={(row) => row.children}
      onCommit={jest.fn(() => Promise.resolve())}
      {...props}
    />,
  )

const nameCells = () =>
  Array.from(
    document.querySelectorAll('tbody td[data-column-id="name"]'),
  ) as HTMLTableCellElement[]

const cellNamed = (name: string) =>
  nameCells().find((cell) => cell.textContent?.trim() === name)!

const focus = (el: HTMLElement) => act(() => el.focus())

const press = (key: string, options: Partial<KeyboardEventInit> = {}) =>
  fireEvent.keyDown(document.activeElement!, { key, ...options })

const flush = () => act(async () => {})

const draftProps = (onCreate = jest.fn(() => Promise.resolve('new-1'))) => ({
  createDraftNode: (draft: { id: string }) => node(draft.id, ''),
  onCreate,
  rowActions: {},
})

describe('WaydGrid row actions', () => {
  describe('inserting a row', () => {
    it('adds a draft sibling directly below the row with Insert', () => {
      // Arrange
      renderTree(draftProps())
      focus(cellNamed('Alpha 1'))

      // Act
      press('Insert')

      // Assert
      const rows = nameCells()
      const draftIndex = rows.findIndex((cell) =>
        cell.querySelector('[data-grid-cell-editor]'),
      )
      expect(rows[draftIndex - 1].textContent?.trim()).toBe('Alpha 1')
      expect(rows[draftIndex + 1].textContent?.trim()).toBe('Alpha 2')
    })

    it('adds the draft below with Ctrl+Enter too, opening its Name', () => {
      // Arrange
      renderTree(draftProps())
      focus(cellNamed('Alpha 3'))

      // Act
      press('Enter', { ctrlKey: true })

      // Assert
      const rows = nameCells()
      const draftIndex = rows.findIndex((cell) =>
        cell.querySelector('[data-grid-cell-editor]'),
      )
      expect(rows[draftIndex - 1].textContent?.trim()).toBe('Alpha 3')
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveFocus()
    })

    it('creates the row with its position among its siblings', async () => {
      // Arrange
      const onCreate = jest.fn(() => Promise.resolve('new-1'))
      renderTree(draftProps(onCreate))
      focus(cellNamed('Alpha 1'))
      press('Insert')
      fireEvent.change(document.activeElement!, { target: { value: 'New' } })

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(onCreate.mock.calls).toStrictEqual([
        ['a', { name: 'New' }, { order: 2 }],
      ])
    })

    it('adds nothing where the grid does not allow a row under that parent', () => {
      // Arrange
      const canInsertUnder = jest.fn((parent: Node | null) => parent !== null)
      renderTree({ ...draftProps(), rowActions: { canInsertUnder } })
      const before = nameCells().length
      focus(cellNamed('Beta'))

      // Act
      press('Insert')

      // Assert
      expect(canInsertUnder).toHaveBeenCalledWith(null)
      expect(nameCells()).toHaveLength(before)
    })

    it('does nothing without drafts', () => {
      // Arrange
      renderTree()
      const before = nameCells().length
      focus(cellNamed('Alpha 1'))

      // Act
      press('Insert')

      // Assert
      expect(nameCells()).toHaveLength(before)
    })
  })

  describe('indenting and outdenting', () => {
    const moveProps = () => {
      const onNodeMove = jest.fn(() => Promise.resolve())
      return { onNodeMove, enableDragAndDrop: true, rowActions: {} }
    }

    it('indents a row under its previous sibling, as its last child', async () => {
      // Arrange
      const props = moveProps()
      renderTree(props)
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(props.onNodeMove.mock.calls).toStrictEqual([['a3', 'a2', 2]])
    })

    it('does not indent a row with no sibling above it', async () => {
      // Arrange
      const props = moveProps()
      renderTree(props)
      focus(cellNamed('Alpha 1'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(props.onNodeMove).not.toHaveBeenCalled()
    })

    it('outdents a row to just after its parent', async () => {
      // Arrange
      const props = moveProps()
      renderTree(props)
      focus(cellNamed('Alpha 2 child'))

      // Act
      press('ArrowLeft', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(props.onNodeMove.mock.calls).toStrictEqual([['a2x', 'a', 3]])
    })

    it('outdents a child of a root row to the root', async () => {
      // Arrange
      const props = moveProps()
      renderTree(props)
      focus(cellNamed('Alpha 1'))

      // Act
      press('ArrowLeft', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(props.onNodeMove.mock.calls).toStrictEqual([['a1', null, 2]])
    })

    it('does not outdent a root row', async () => {
      // Arrange
      const props = moveProps()
      renderTree(props)
      focus(cellNamed('Beta'))

      // Act
      press('ArrowLeft', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(props.onNodeMove).not.toHaveBeenCalled()
    })

    it('reports a move the validator rejects and does not make it', async () => {
      // Arrange
      const props = moveProps()
      const onMoveRejected = jest.fn()
      renderTree({
        ...props,
        onMoveRejected,
        moveValidator: () => ({ canMove: false, reason: 'Not here' }),
      })
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(props.onNodeMove).not.toHaveBeenCalled()
      expect(onMoveRejected).toHaveBeenCalledWith('Not here')
      // The consumer's toast is already an alert; the live region stays quiet
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).not.toContain('Not here')
    })

    it('reports a failed move once, through onMoveRejected', async () => {
      // Arrange
      const onMoveRejected = jest.fn()
      renderTree({
        onNodeMove: jest.fn(() => Promise.reject(new Error('Server said no'))),
        enableDragAndDrop: true,
        rowActions: {},
        onMoveRejected,
      })
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(onMoveRejected.mock.calls).toStrictEqual([['Server said no']])
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).not.toContain('Server said no')
    })

    it('hands the validator the target parent', async () => {
      // Arrange
      const props = moveProps()
      const moveValidator = jest.fn(() => ({ canMove: true }))
      renderTree({ ...props, moveValidator })
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      const [active, parent, parentId] = moveValidator.mock.calls[0] as never[]
      expect((active as { node: Node }).node.id).toBe('a3')
      expect((parent as Node).id).toBe('a2')
      expect(parentId).toBe('a2')
    })

    it('takes no row keys from a grid without rowActions', async () => {
      // Arrange
      const onNodeMove = jest.fn(() => Promise.resolve())
      const onCreate = jest.fn(() => Promise.resolve('new-1'))
      renderTree({
        onNodeMove,
        enableDragAndDrop: true,
        createDraftNode: (draft) => node(draft.id, ''),
        onCreate,
      })
      const before = nameCells().length
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      press('Insert')
      await flush()

      // Assert
      expect(onNodeMove).not.toHaveBeenCalled()
      expect(nameCells()).toHaveLength(before)
    })

    it('does not move rows without onNodeMove', () => {
      // Arrange
      renderTree()
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })

      // Assert — the cell still has focus and no expansion key ran
      expect(cellNamed('Alpha 3')).toHaveFocus()
    })

    it('waits for the data to show the move before following the row', async () => {
      // Arrange — the move resolves while the data still has the old tree
      const moved: Node[] = [
        node('a', 'Alpha', [
          node('a2', 'Alpha 2', [node('a2x', 'Alpha 2 child')]),
          node('a3', 'Alpha 3'),
        ]),
        node('a1', 'Alpha 1'),
        node('b', 'Beta'),
      ]
      const onNodeMove = jest.fn(() => Promise.resolve())
      const props = {
        columns,
        includeIdColumn: false,
        getSubRows: (row: Node) => row.children,
        onCommit: jest.fn(() => Promise.resolve()),
        onNodeMove,
        enableDragAndDrop: true,
        rowActions: {},
      }
      const { rerender } = render(<WaydGrid<Node> data={TREE} {...props} />)
      focus(cellNamed('Alpha 1'))
      press('ArrowLeft', { altKey: true, shiftKey: true })
      await flush()

      // Act
      rerender(<WaydGrid<Node> data={moved} {...props} />)
      await flush()

      // Assert
      expect(cellNamed('Alpha 1')).toHaveFocus()
    })

    it('keeps focus on the moved row once the data shows it moved', async () => {
      // Arrange
      const moved: Node[] = [
        node('a', 'Alpha', [
          node('a1', 'Alpha 1'),
          node('a2', 'Alpha 2', [
            node('a2x', 'Alpha 2 child'),
            node('a3', 'Alpha 3'),
          ]),
        ]),
        node('b', 'Beta'),
      ]
      const onNodeMove = jest.fn(() => Promise.resolve())
      const { rerender } = renderTree({
        onNodeMove,
        enableDragAndDrop: true,
        rowActions: {},
      })
      focus(cellNamed('Alpha 3'))
      press('ArrowRight', { altKey: true, shiftKey: true })

      // Act
      rerender(
        <WaydGrid<Node>
          data={moved}
          columns={columns}
          includeIdColumn={false}
          getSubRows={(row) => row.children}
          onCommit={jest.fn(() => Promise.resolve())}
          onNodeMove={onNodeMove}
          enableDragAndDrop
          rowActions={{}}
        />,
      )
      await flush()

      // Assert
      expect(cellNamed('Alpha 3')).toHaveFocus()
    })
  })

  describe('saying why a key did nothing', () => {
    const liveText = () =>
      screen.getAllByRole('status').map((region) => region.textContent?.trim())

    it('says a row cannot be added under that parent', () => {
      // Arrange
      renderTree({
        ...draftProps(),
        rowActions: { canInsertUnder: (parent) => parent !== null },
      })
      focus(cellNamed('Beta'))

      // Act
      press('Insert')

      // Assert
      expect(liveText()).toContain('A row cannot be added here.')
    })

    it('says it again when the same key is refused twice', () => {
      // Arrange
      renderTree({
        ...draftProps(),
        rowActions: { canInsertUnder: (parent) => parent !== null },
      })
      focus(cellNamed('Beta'))
      // Untrimmed: the change is a trailing no-break space
      const rawLiveText = () =>
        screen.getAllByRole('status').map((region) => region.textContent)
      press('Insert')
      const first = rawLiveText()

      // Act
      press('Insert')

      // Assert — the region's text changes, so a screen reader reads it again
      expect(rawLiveText()).not.toStrictEqual(first)
      expect(liveText()).toContain('A row cannot be added here.')
    })

    it('says to finish the new row before adding another', () => {
      // Arrange
      renderTree(draftProps())
      focus(cellNamed('Alpha 1'))
      press('Insert')
      press('Tab') // leaves the draft's Name empty, the draft kept
      focus(cellNamed('Alpha 3'))

      // Act
      press('Insert')

      // Assert
      expect(liveText()).toContain('Finish the new row first.')
    })

    it('says there is nothing above to indent under', () => {
      // Arrange
      renderTree({
        onNodeMove: jest.fn(() => Promise.resolve()),
        enableDragAndDrop: true,
        rowActions: {},
      })
      focus(cellNamed('Alpha 1'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })

      // Assert
      expect(liveText()).toContain(
        'There is no row above at this level to indent under.',
      )
    })

    it('says a root row is already at the top level', () => {
      // Arrange
      renderTree({
        onNodeMove: jest.fn(() => Promise.resolve()),
        enableDragAndDrop: true,
        rowActions: {},
      })
      focus(cellNamed('Beta'))

      // Act
      press('ArrowLeft', { altKey: true, shiftKey: true })

      // Assert
      expect(liveText()).toContain('The row is already at the top level.')
    })

    it('reads out the validator’s reason', () => {
      // Arrange
      renderTree({
        onNodeMove: jest.fn(() => Promise.resolve()),
        enableDragAndDrop: true,
        rowActions: {},
        moveValidator: () => ({ canMove: false, reason: 'Not here' }),
      })
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })

      // Assert
      expect(liveText()).toContain('Not here')
    })

    it('refuses a second move while the first is saving', async () => {
      // Arrange
      let finish!: () => void
      const onNodeMove = jest.fn(
        () => new Promise<void>((resolve) => (finish = resolve)),
      )
      renderTree({ onNodeMove, enableDragAndDrop: true, rowActions: {} })
      focus(cellNamed('Alpha 3'))
      press('ArrowRight', { altKey: true, shiftKey: true })

      // Act
      press('ArrowLeft', { altKey: true, shiftKey: true })

      // Assert
      expect(onNodeMove).toHaveBeenCalledTimes(1)
      expect(liveText()).toContain('Wait for the last move to finish.')
      await act(async () => finish())
    })

    it('moves again once the first move has landed', async () => {
      // Arrange
      const onNodeMove = jest.fn(() => Promise.resolve())
      renderTree({ onNodeMove, enableDragAndDrop: true, rowActions: {} })
      focus(cellNamed('Alpha 3'))
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Act
      focus(cellNamed('Alpha 3'))
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(onNodeMove).toHaveBeenCalledTimes(2)
    })

    it('says the row moved', async () => {
      // Arrange
      renderTree({
        onNodeMove: jest.fn(() => Promise.resolve()),
        enableDragAndDrop: true,
        rowActions: {},
      })
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(liveText()).toContain('Indented.')
    })
  })

  describe('indenting under a collapsed row', () => {
    it('expands the new parent, so the row stays in view', async () => {
      // Arrange
      renderTree({
        onNodeMove: jest.fn(() => Promise.resolve()),
        enableDragAndDrop: true,
        rowActions: {},
      })
      focus(cellNamed('Alpha 2'))
      press('ArrowLeft') // collapses it
      const collapsed = !nameCells().some(
        (cell) => cell.textContent?.trim() === 'Alpha 2 child',
      )
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(collapsed).toBe(true)
      expect(cellNamed('Alpha 2 child')).toBeDefined()
    })
  })

  describe('while the rows are filtered', () => {
    it('does not add a row, and says why', async () => {
      // Arrange
      renderTree(draftProps())
      const before = nameCells().length
      fireEvent.change(screen.getByPlaceholderText('Search'), {
        target: { value: 'Alpha' },
      })
      await flush()
      focus(cellNamed('Alpha 3'))

      // Act
      press('Insert')

      // Assert
      expect(nameCells().length).toBeLessThan(before)
      expect(
        nameCells().some((cell) =>
          cell.querySelector('[data-grid-cell-editor]'),
        ),
      ).toBe(false)
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).toContain('Clear the search, filters and sorting to add a row.')
    })

    it('does not move rows, as dragging does not', async () => {
      // Arrange
      const onNodeMove = jest.fn(() => Promise.resolve())
      renderTree({ onNodeMove, enableDragAndDrop: true, rowActions: {} })
      fireEvent.change(screen.getByPlaceholderText('Search'), {
        target: { value: 'Alpha' },
      })
      await flush()
      focus(cellNamed('Alpha 3'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(onNodeMove).not.toHaveBeenCalled()
    })
  })

  describe('deleting and opening', () => {
    it('says why when the row declines the key, and does nothing else', () => {
      // Arrange
      const onDelete = jest.fn(() => false)
      renderTree({ rowActions: { onDelete } })
      focus(cellNamed('Beta'))

      // Act
      press('Delete', { ctrlKey: true })

      // Assert
      expect(onDelete).toHaveBeenCalled()
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).toContain('This row cannot be deleted.')
      expect(cellNamed('Beta')).toHaveFocus()
    })

    it('does not let a move key the grid cannot do act as a plain arrow', () => {
      // Arrange — no onNodeMove, so Alt+Shift+Right moves nothing
      renderTree({ rowActions: { onOpen: jest.fn() } })
      focus(cellNamed('Alpha 2'))
      press('ArrowLeft') // collapses it

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })

      // Assert — a plain Right would have expanded it again
      expect(
        nameCells().some(
          (cell) => cell.textContent?.trim() === 'Alpha 2 child',
        ),
      ).toBe(false)
    })

    it('does not let Ctrl+Enter edit the cell when it adds nothing', () => {
      // Arrange — no drafts, so Ctrl+Enter has no row to add
      renderTree({ rowActions: { onOpen: jest.fn() } })
      focus(cellNamed('Beta'))

      // Act
      press('Enter', { ctrlKey: true })

      // Assert
      expect(document.querySelector('[data-grid-cell-editor]')).toBeNull()
    })

    it('takes the key when the row is deleted', () => {
      // Arrange
      renderTree({ rowActions: { onDelete: jest.fn() } })
      focus(cellNamed('Beta'))

      // Act
      const notPrevented = fireEvent.keyDown(document.activeElement!, {
        key: 'Delete',
        ctrlKey: true,
      })

      // Assert
      expect(notPrevented).toBe(false)
    })

    it('asks to delete the row with Ctrl+Delete', () => {
      // Arrange
      const onDelete = jest.fn()
      renderTree({ rowActions: { onDelete } })
      focus(cellNamed('Alpha 2'))

      // Act
      press('Delete', { ctrlKey: true })

      // Assert
      expect(onDelete).toHaveBeenCalledWith(TREE[0].children[1])
    })

    it('opens the row with Ctrl+Shift+Enter', () => {
      // Arrange
      const onOpen = jest.fn()
      renderTree({ rowActions: { onOpen } })
      focus(cellNamed('Beta'))

      // Act
      press('Enter', { ctrlKey: true, shiftKey: true })

      // Assert
      expect(onOpen).toHaveBeenCalledWith(TREE[1])
    })

    it('leaves the keys to an open editor', () => {
      // Arrange
      const onDelete = jest.fn()
      renderTree({ rowActions: { onDelete } })
      focus(cellNamed('Beta'))
      press('Enter')

      // Act
      press('Delete', { ctrlKey: true })

      // Assert
      expect(onDelete).not.toHaveBeenCalled()
      expect(screen.getByRole('textbox', { name: 'Name' })).toBeInTheDocument()
    })

    it('does not delete on a plain Delete', () => {
      // Arrange
      const onDelete = jest.fn()
      renderTree({ rowActions: { onDelete } })
      focus(cellNamed('Beta'))

      // Act
      press('Delete')

      // Assert
      expect(onDelete).not.toHaveBeenCalled()
    })
  })

  describe('help', () => {
    it('shows the row keys in the grid’s help panel', () => {
      // Arrange
      renderTree({
        ...draftProps(),
        onNodeMove: jest.fn(() => Promise.resolve()),
        enableDragAndDrop: true,
        rowActions: { onDelete: jest.fn(), onOpen: jest.fn() },
      })

      // Act
      fireEvent.click(screen.getByRole('button', { name: /help/i }))

      // Assert
      expect(screen.getByText('Insert / Ctrl + Enter')).toBeInTheDocument()
      expect(screen.getByText('Alt + Shift + Right / Left')).toBeInTheDocument()
      expect(screen.getByText('Ctrl + Delete')).toBeInTheDocument()
      expect(screen.getByText('Ctrl + Shift + Enter')).toBeInTheDocument()
    })

    it('lists no Insert key for a grid without new rows', () => {
      // Arrange
      renderTree({ rowActions: { onOpen: jest.fn() } })

      // Act
      fireEvent.click(screen.getByRole('button', { name: /help/i }))

      // Assert
      expect(screen.getByText('Ctrl + Shift + Enter')).toBeInTheDocument()
      expect(
        screen.queryByText('Insert / Ctrl + Enter'),
      ).not.toBeInTheDocument()
    })

    it('adds the grid’s own notes after the keys', () => {
      // Arrange
      renderTree({ helpNotes: <p>Drag a bar to reschedule</p> })

      // Act
      fireEvent.click(screen.getByRole('button', { name: /help/i }))

      // Assert
      expect(screen.getByText('Drag a bar to reschedule')).toBeInTheDocument()
      expect(screen.getByText(/^Save and move down/)).toBeInTheDocument()
    })
  })

  describe('focusing a row from outside (focusRow)', () => {
    it('focuses the row once the data has it', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const { rerender } = renderTree({ ref })
      focus(cellNamed('Alpha 1'))

      // Act
      act(() => ref.current!.focusRow('c', 'name'))
      rerender(
        <WaydGrid<Node>
          ref={ref}
          data={[...TREE, node('c', 'Gamma')]}
          columns={columns}
          includeIdColumn={false}
          getSubRows={(row) => row.children}
          onCommit={jest.fn(() => Promise.resolve())}
        />,
      )
      await flush()

      // Assert
      expect(cellNamed('Gamma')).toHaveFocus()
    })
  })
})
