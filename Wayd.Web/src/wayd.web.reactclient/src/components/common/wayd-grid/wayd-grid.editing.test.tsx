jest.mock('dayjs', () => jest.requireActual('dayjs'))

import { createRef } from 'react'
import { Button } from 'antd'
import { render, screen, fireEvent, within, act } from '@testing-library/react'
import type { ColumnDef } from '../wayd-grid-core'
import WaydGrid from './wayd-grid'
import { MessageProvider } from '@/src/components/contexts/messaging/messaging-context'
import type { WaydGridHandle, WaydGridColumnMeta } from './types'
import type { DraftItem } from '../wayd-grid-core/draft-utils'
import { useGridDragHandle } from '../wayd-grid-core/dnd/grid-dnd'
import {
  dateEditor,
  pairedEditors,
  selectEditor,
  textEditor,
} from '../wayd-grid-core/cell-editing/editors'

interface Item {
  id: string
  code: string
  name: string
  note: string
  owner: string | null
  start: string | null
  end: string | null
  children?: Item[]
}

const item = (id: string, name: string, extra: Partial<Item> = {}): Item => ({
  id,
  code: `C-${id}`,
  name,
  note: '',
  owner: null,
  start: '2026-03-01',
  end: '2026-03-31',
  ...extra,
})

const ITEMS: Item[] = [
  item('1', 'Alpha', { owner: 'ada' }),
  item('2', 'Beta'),
  item('3', 'Gamma'),
]

const OWNERS = [
  { label: 'Ada', value: 'ada' },
  { label: 'Bo', value: 'bo' },
]

const [startEditor, endEditor] = pairedEditors<
  Item,
  string | null,
  string | null
>({
  start: dateEditor<Item>({
    field: 'start',
    get: (row) => row.start,
    apply: (row, start) => ({ ...row, start }),
  }),
  end: dateEditor<Item>({
    field: 'end',
    get: (row) => row.end,
    apply: (row, end) => ({ ...row, end }),
  }),
  validate: ({ start, end }) =>
    start && end && end < start
      ? { end: 'End cannot be before Start.' }
      : undefined,
})

const editableColumns: ColumnDef<Item, unknown>[] = [
  { id: 'code', accessorKey: 'code', header: 'Code' },
  {
    id: 'name',
    accessorKey: 'name',
    header: 'Name',
    meta: {
      treeColumn: true,
      editor: textEditor<Item>({
        field: 'name',
        get: (row) => row.name,
        apply: (row, name) => ({ ...row, name }),
        validate: (name) => (name.trim() ? undefined : 'Name is required.'),
      }),
    } satisfies WaydGridColumnMeta,
  },
  {
    id: 'note',
    accessorKey: 'note',
    header: 'Note',
    meta: {
      editor: textEditor<Item>({
        field: 'note',
        get: (row) => row.note,
        apply: (row, note) => ({ ...row, note }),
      }),
    } satisfies WaydGridColumnMeta,
  },
  {
    id: 'owner',
    accessorFn: (row) => OWNERS.find((o) => o.value === row.owner)?.label ?? '',
    header: 'Owner',
    meta: {
      editor: selectEditor<Item, string | null>({
        field: 'ownerId',
        get: (row) => row.owner,
        apply: (row, owner) => ({ ...row, owner }),
        options: OWNERS,
      }),
    } satisfies WaydGridColumnMeta,
  },
  {
    id: 'start',
    accessorKey: 'start',
    header: 'Start',
    meta: { editor: startEditor } satisfies WaydGridColumnMeta,
  },
  {
    id: 'end',
    accessorKey: 'end',
    header: 'End',
    meta: { editor: endEditor } satisfies WaydGridColumnMeta,
  },
]

type GridProps = Partial<Parameters<typeof WaydGrid<Item>>[0]> & {
  ref?: React.Ref<WaydGridHandle>
}

const renderEditable = ({ ref, ...props }: GridProps = {}) => {
  const onCommit = jest.fn<Promise<void>, [string, Record<string, unknown>]>(
    () => Promise.resolve(),
  )
  const utils = render(
    <WaydGrid<Item>
      ref={ref}
      data={ITEMS}
      columns={editableColumns}
      includeIdColumn={false}
      onCommit={onCommit}
      {...props}
    />,
  )
  return { ...utils, onCommit: (props.onCommit as jest.Mock) ?? onCommit }
}

const bodyCells = (columnId: string) =>
  Array.from(
    document.querySelectorAll(`tbody td[data-column-id="${columnId}"]`),
  ) as HTMLTableCellElement[]

const cellAt = (rowIndex: number, columnId: string) =>
  bodyCells(columnId)[rowIndex]

const focus = (el: HTMLElement) => act(() => el.focus())

const press = (key: string, options: Partial<KeyboardEventInit> = {}) =>
  fireEvent.keyDown(document.activeElement!, { key, ...options })

/** Replaces the open text editor's value, as typing would. */
const typeInEditor = (value: string) =>
  fireEvent.change(document.activeElement!, { target: { value } })

/** The control of the open editor, if any. */
const openEditor = () =>
  document.querySelector<HTMLElement>('tbody [data-grid-cell-editor]')

/** Settles queued commits and their state updates. */
const flush = () => act(async () => {})

/** A promise the test resolves or rejects by hand. */
const deferred = () => {
  let resolve!: () => void
  let reject!: (error: unknown) => void
  const promise = new Promise<void>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

/** Opens the name cell of a row and replaces its text. */
const editName = (rowIndex: number, value: string) => {
  focus(cellAt(rowIndex, 'name'))
  press('Enter')
  typeInEditor(value)
}

describe('WaydGrid cell editing', () => {
  afterEach(() => jest.restoreAllMocks())

  describe('opening and closing a cell', () => {
    it('opens the focused cell on its current value with Enter', () => {
      // Arrange
      renderEditable()
      focus(cellAt(0, 'name'))

      // Act
      press('Enter')

      // Assert
      const input = screen.getByRole('textbox', { name: 'Name' })
      expect(input).toHaveValue('Alpha')
      expect(document.activeElement).toBe(input)
      expect(cellAt(0, 'name')).toContainElement(input)
    })

    it('opens with F2 too', () => {
      // Arrange
      renderEditable()
      focus(cellAt(1, 'note'))

      // Act
      press('F2')

      // Assert
      expect(screen.getByRole('textbox', { name: 'Note' })).toHaveFocus()
    })

    it('replaces the value with a typed character', () => {
      // Arrange
      renderEditable()
      focus(cellAt(0, 'name'))

      // Act
      press('Z')

      // Assert
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue('Z')
    })

    it('selects a cell on the first click and opens it on the second', () => {
      // Arrange
      renderEditable()
      const target = cellAt(1, 'name')

      // Act — a click focuses the cell, as a browser does on mousedown
      fireEvent.pointerDown(target)
      focus(target)
      fireEvent.click(target)
      const afterFirst = openEditor()
      fireEvent.pointerDown(target)
      fireEvent.click(target)

      // Assert
      expect(afterFirst).toBeNull()
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue('Beta')
    })

    it('opens a cell on double-click', () => {
      // Arrange
      renderEditable()

      // Act
      fireEvent.doubleClick(cellAt(2, 'note'))

      // Assert
      expect(screen.getByRole('textbox', { name: 'Note' })).toBeInTheDocument()
    })

    it('does not open a column without an editor', () => {
      // Arrange
      renderEditable()
      focus(cellAt(0, 'code'))

      // Act
      press('Enter')
      press('X')

      // Assert
      expect(openEditor()).toBeNull()
    })

    it('reverts with Escape and returns focus to the cell', () => {
      // Arrange
      const { onCommit } = renderEditable()
      editName(0, 'Changed')

      // Act
      press('Escape')

      // Assert
      expect(openEditor()).toBeNull()
      expect(document.activeElement).toBe(cellAt(0, 'name'))
      expect(cellAt(0, 'name')).toHaveTextContent('Alpha')
      expect(onCommit).not.toHaveBeenCalled()
    })
  })

  describe('committing', () => {
    it('sends only the edited field with Enter and moves to the cell below', async () => {
      // Arrange
      const { onCommit } = renderEditable()
      editName(0, 'Alpha 2')

      // Act
      press('Enter')
      await flush()

      // Assert — strict, so an `undefined` for another field would fail
      expect(onCommit).toHaveBeenCalledTimes(1)
      expect(onCommit.mock.calls[0]).toStrictEqual(['1', { name: 'Alpha 2' }])
      expect(openEditor()).toBeNull()
      expect(document.activeElement).toBe(cellAt(1, 'name'))
    })

    it('sends nothing when the value is unchanged', async () => {
      // Arrange
      const { onCommit } = renderEditable()
      focus(cellAt(0, 'name'))
      press('Enter')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(onCommit).not.toHaveBeenCalled()
    })

    it('commits with Tab and opens the next editable cell, Shift+Tab the previous', async () => {
      // Arrange
      const { onCommit } = renderEditable()
      editName(0, 'Alpha 2')

      // Act
      press('Tab')
      const afterTab = document.activeElement
      const noteInput = cellAt(0, 'note').querySelector('input')
      press('Tab', { shiftKey: true })
      await flush()

      // Assert — Code has no editor, so Shift+Tab from Note lands on Name
      expect(noteInput).not.toBeNull()
      expect(afterTab).toBe(noteInput)
      expect(document.activeElement).toBe(
        screen.getByRole('textbox', { name: 'Name' }),
      )
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue(
        'Alpha 2',
      )
      expect(onCommit).toHaveBeenCalledTimes(1)
      expect(onCommit).toHaveBeenCalledWith('1', { name: 'Alpha 2' })
    })

    it('wraps Tab from the last editable cell of a row to the next row', () => {
      // Arrange
      renderEditable()
      focus(cellAt(0, 'end'))
      press('Enter')

      // Act
      press('Tab')

      // Assert — Code has no editor, so the next row starts at Name
      expect(cellAt(1, 'name')).toContainElement(
        document.activeElement as HTMLElement,
      )
    })

    it('leaves the grid with Tab from the last editable cell', async () => {
      // Arrange
      const { onCommit } = renderEditable()
      focus(cellAt(2, 'note'))
      press('Enter')
      typeInEditor('Last')
      focus(cellAt(2, 'end'))
      press('Enter')

      // Act — the browser's own Tab step must run on from the sentinel
      const allowed = press('Tab')
      await flush()

      // Assert
      expect(allowed).toBe(true)
      expect(openEditor()).toBeNull()
      expect(document.activeElement).toHaveAttribute('aria-hidden', 'true')
      expect(onCommit).toHaveBeenCalledWith('3', { note: 'Last' })
    })

    it('keeps an invalid value on the cell, marks it and sends nothing', async () => {
      // Arrange
      const { onCommit } = renderEditable()
      editName(0, '   ')

      // Act
      press('Enter')
      await flush()

      // Assert
      const cell = cellAt(0, 'name')
      expect(onCommit).not.toHaveBeenCalled()
      expect(cell).toHaveAttribute('data-edit-state', 'error')
      expect(cell).toHaveAttribute('aria-invalid', 'true')
      expect(cell).toHaveAccessibleDescription('Name: Name is required.')
    })

    it('commits the choice picked from a list', async () => {
      // Arrange
      const { onCommit } = renderEditable()
      focus(cellAt(1, 'owner'))
      press('Enter')
      const host = document.querySelector<HTMLElement>(
        '[data-grid-editor-owner]',
      )!

      // Act
      const option = within(host).getByTitle('Bo')
      fireEvent.pointerDown(option)
      fireEvent.click(option)
      await flush()

      // Assert
      expect(onCommit).toHaveBeenCalledTimes(1)
      expect(onCommit).toHaveBeenCalledWith('2', { ownerId: 'bo' })
      expect(document.activeElement).toBe(cellAt(1, 'owner'))
    })
  })

  describe('saving', () => {
    it('shows the new value marked as saving until the commit settles', async () => {
      // Arrange
      const pending = deferred()
      const onCommit = jest.fn(() => pending.promise)
      renderEditable({ onCommit })
      editName(0, 'Alpha 2')

      // Act
      press('Enter')
      const whileSaving = cellAt(0, 'name').getAttribute('data-edit-state')
      const busy = cellAt(0, 'name').getAttribute('aria-busy')
      await act(async () => pending.resolve())

      // Assert
      expect(whileSaving).toBe('saving')
      expect(busy).toBe('true')
      expect(cellAt(0, 'name')).not.toHaveAttribute('data-edit-state')
      expect(cellAt(0, 'name')).toHaveTextContent('Alpha 2')
    })

    it('runs one commit at a time for a row', async () => {
      // Arrange
      const first = deferred()
      const onCommit = jest
        .fn()
        .mockImplementationOnce(() => first.promise)
        .mockImplementation(() => Promise.resolve())
      renderEditable({ onCommit })
      editName(0, 'Alpha 2')
      press('Tab')
      typeInEditor('A note')

      // Act
      press('Enter')
      await flush()
      const callsWhileFirstRuns = onCommit.mock.calls.length
      await act(async () => first.resolve())

      // Assert
      expect(callsWhileFirstRuns).toBe(1)
      expect(onCommit).toHaveBeenNthCalledWith(2, '1', { note: 'A note' })
    })

    it('prompts before leaving the page while a change is saving', async () => {
      // Arrange
      const pending = deferred()
      renderEditable({ onCommit: jest.fn(() => pending.promise) })
      editName(0, 'Alpha 2')
      press('Enter')
      const leave = () => {
        const event = new Event('beforeunload', { cancelable: true })
        window.dispatchEvent(event)
        return event.defaultPrevented
      }

      // Act
      const whileSaving = leave()
      await act(async () => pending.resolve())
      const afterSaving = leave()

      // Assert
      expect(whileSaving).toBe(true)
      expect(afterSaving).toBe(false)
    })
  })

  describe('failed commits', () => {
    const rejectWith = (error: unknown) => jest.fn(() => Promise.reject(error))

    it("keeps the user's value, marks the cell and focuses it", async () => {
      // Arrange
      renderEditable({ onCommit: rejectWith({ status: 500, detail: 'Boom.' }) })
      editName(0, 'Alpha 2')

      // Act
      press('Enter')
      await flush()

      // Assert
      const cell = cellAt(0, 'name')
      expect(cell).toHaveTextContent('Alpha 2')
      expect(cell).toHaveAttribute('data-edit-state', 'error')
      expect(cell).toHaveAccessibleDescription('Name: Boom.')
      expect(document.activeElement).toBe(cell)
    })

    it('shows and announces the message, outside the cell text', async () => {
      // Arrange
      renderEditable({ onCommit: rejectWith({ status: 500, detail: 'Boom.' }) })
      editName(0, 'Alpha 2')

      // Act
      press('Enter')
      await flush()

      // Assert — the failed cell has focus, so its message shows
      expect(screen.getByRole('tooltip')).toHaveTextContent('Name: Boom.')
      // dnd-kit keeps a status region of its own
      expect(
        screen.getAllByRole('status').map((region) => region.textContent),
      ).toContain('Not saved. Name: Boom.')
      expect(cellAt(0, 'name')).not.toHaveTextContent('Boom.')
    })

    it('marks the reopened editor invalid with its message', async () => {
      // Arrange
      renderEditable({ onCommit: rejectWith({ status: 500, detail: 'Boom.' }) })
      editName(0, 'Alpha 2')
      press('Enter')
      await flush()

      // Act
      press('Enter')

      // Assert
      const input = screen.getByRole('textbox', { name: 'Name' })
      expect(input).toHaveAttribute('aria-invalid', 'true')
      expect(input).toHaveAccessibleDescription('Name: Boom.')
    })

    it('reopens a failed cell on the value the user typed', async () => {
      // Arrange
      renderEditable({ onCommit: rejectWith({ status: 500, detail: 'Boom.' }) })
      editName(0, 'Alpha 2')
      press('Enter')
      await flush()

      // Act
      press('Enter')

      // Assert
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue(
        'Alpha 2',
      )
    })

    it('places server validation errors on cells by column label', async () => {
      // Arrange
      renderEditable({
        onCommit: rejectWith({
          status: 422,
          errors: { Name: ['Name is too long.'], OwnerId: ['Owner left.'] },
        }),
      })
      editName(0, 'Alpha 2')

      // Act
      press('Enter')
      await flush()

      // Assert — OwnerId reaches the Owner column through its editor's field
      expect(cellAt(0, 'name')).toHaveAccessibleDescription(
        'Name: Name is too long.',
      )
      expect(cellAt(0, 'owner')).toHaveAccessibleDescription(
        'Owner: Owner left.',
      )
    })

    it('reads the errors of an RTK Query rejection, in its data', async () => {
      // Arrange
      renderEditable({
        onCommit: rejectWith({
          status: 422,
          data: { status: 422, errors: { OwnerId: ['Owner left.'] } },
        }),
      })
      editName(0, 'Alpha 2')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(cellAt(0, 'owner')).toHaveAccessibleDescription(
        'Owner: Owner left.',
      )
    })

    it('maps an error key through fieldToColumn', async () => {
      // Arrange
      renderEditable({
        fieldToColumn: { assigneeId: 'owner' },
        onCommit: rejectWith({
          status: 422,
          errors: { AssigneeId: ['Not a member.'] },
        }),
      })
      editName(0, 'Alpha 2')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(cellAt(0, 'owner')).toHaveAccessibleDescription(
        'Owner: Not a member.',
      )
    })

    it('discards a failed change with Escape', async () => {
      // Arrange
      renderEditable({ onCommit: rejectWith({ status: 500, detail: 'Boom.' }) })
      editName(0, 'Alpha 2')
      press('Enter')
      await flush()

      // Act
      press('Escape')

      // Assert
      expect(cellAt(0, 'name')).toHaveTextContent('Alpha')
      expect(cellAt(0, 'name')).not.toHaveTextContent('Alpha 2')
      expect(cellAt(0, 'name')).not.toHaveAttribute('data-edit-state')
    })
  })

  describe('edits that must not be lost', () => {
    it('never sends a hidden column or one whose row scrolled away', async () => {
      // Arrange — Note hidden; 200 rows, so row 1 leaves the window
      const ref = createRef<WaydGridHandle>()
      const many = Array.from({ length: 200 }, (_, i) =>
        item(String(i + 1), `Item ${i + 1}`, { note: `note ${i + 1}` }),
      )
      const { onCommit } = renderEditable({ ref, data: many })
      act(() => ref.current!.table.getColumn('note').toggleVisibility(false))
      editName(0, 'Renamed')

      // Act
      const viewport = document.querySelector<HTMLElement>(
        '[data-grid-body-viewport]',
      )!
      act(() => {
        viewport.scrollTop = 100 * 28
        fireEvent.scroll(viewport)
      })
      const editorAfterScroll = openEditor()
      press('Enter')
      await flush()

      // Assert
      expect(editorAfterScroll).not.toBeNull()
      expect(onCommit).toHaveBeenCalledTimes(1)
      // Strict: a hidden field sent as `undefined` would clear it.
      expect(onCommit.mock.calls[0]).toStrictEqual(['1', { name: 'Renamed' }])
    })

    it('keeps the open cell rendered while focus is in its popup', () => {
      // Arrange — focus moves into the editor's popup, outside the body
      const many = Array.from({ length: 200 }, (_, i) =>
        item(String(i + 1), `Item ${i + 1}`),
      )
      // jsdom reports the window unfocused, which the body reads as Alt+Tab
      // and so keeps the focused row pinned anyway.
      jest.spyOn(document, 'hasFocus').mockReturnValue(true)
      renderEditable({ data: many })
      editName(0, 'Renamed')
      const popupControl = document.createElement('button')
      document
        .querySelector('[data-grid-editor-owner]')!
        .appendChild(popupControl)
      focus(popupControl)

      // Act
      const viewport = document.querySelector<HTMLElement>(
        '[data-grid-body-viewport]',
      )!
      act(() => {
        viewport.scrollTop = 100 * 28
        fireEvent.scroll(viewport)
      })

      // Assert
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue(
        'Renamed',
      )
    })

    it('keeps typed and failed values through a refetch', async () => {
      // Arrange
      const onCommit = jest
        .fn()
        .mockImplementationOnce(() =>
          Promise.reject({ status: 500, detail: 'x' }),
        )
      const { rerender } = renderEditable({ onCommit })
      editName(1, 'Beta failed')
      press('Enter')
      await flush()
      editName(0, 'Typing')
      const refetched = ITEMS.map((row) => ({ ...row, name: `${row.name}!` }))

      // Act
      rerender(
        <WaydGrid<Item>
          data={refetched}
          columns={editableColumns}
          includeIdColumn={false}
          onCommit={onCommit}
        />,
      )

      // Assert — the open and failed cells keep the user's values; others follow
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue(
        'Typing',
      )
      expect(cellAt(1, 'name')).toHaveTextContent('Beta failed')
      expect(cellAt(2, 'name')).toHaveTextContent('Gamma!')
    })

    it('keeps a failed value after moving on past the row', async () => {
      // Arrange
      const failure = deferred()
      renderEditable({ onCommit: jest.fn(() => failure.promise) })
      editName(0, 'Alpha 2')

      // Act — Tab on, leave the next cell, move down a row; then it fails
      press('Tab')
      press('Escape')
      press('ArrowDown')
      const movedTo = document.activeElement
      await act(async () => failure.reject({ status: 500, detail: 'Down.' }))

      // Assert
      expect(movedTo).toBe(cellAt(1, 'note'))
      expect(cellAt(0, 'name')).toHaveTextContent('Alpha 2')
      expect(cellAt(0, 'name')).toHaveAttribute('data-edit-state', 'error')
    })

    describe('commits on a pointerdown anywhere outside the cell', () => {
      const targets: Array<[string, () => Element]> = [
        [
          'the empty body',
          () => document.querySelector('[data-grid-body-viewport]')!,
        ],
        ['a header', () => screen.getByRole('columnheader', { name: /Owner/ })],
        ['the toolbar', () => screen.getByText('3 of 3')],
        ['the chart pane', () => screen.getAllByTestId('bar')[0]],
        ['another cell', () => cellAt(2, 'code')],
      ]

      it.each(targets)('%s', async (_name, target) => {
        // Arrange
        const { onCommit } = renderEditable({
          rightPane: {
            header: <div>axis</div>,
            renderRow: () => <div data-testid="bar" />,
          },
        })
        editName(0, 'Alpha 2')

        // Act
        fireEvent.pointerDown(target())
        await flush()

        // Assert
        expect(onCommit).toHaveBeenCalledWith('1', { name: 'Alpha 2' })
        expect(openEditor()).toBeNull()
      })

      it('but not inside the cell', async () => {
        // Arrange
        const { onCommit } = renderEditable()
        editName(0, 'Alpha 2')

        // Act
        fireEvent.pointerDown(screen.getByRole('textbox', { name: 'Name' }))
        await flush()

        // Assert
        expect(onCommit).not.toHaveBeenCalled()
        expect(openEditor()).not.toBeNull()
      })

      it('and on focus moving out of the cell', async () => {
        // Arrange
        const { onCommit } = renderEditable()
        editName(0, 'Alpha 2')

        // Act
        fireEvent.blur(screen.getByRole('textbox', { name: 'Name' }), {
          relatedTarget: cellAt(1, 'code'),
        })
        await flush()

        // Assert
        expect(onCommit).toHaveBeenCalledWith('1', { name: 'Alpha 2' })
      })
    })

    it('closes only the open list on the first Escape', async () => {
      // Arrange — rc-select closes its list through a MessageChannel task,
      // which the global jest stub never delivers; deliver it here.
      const stub = global.MessageChannel
      global.MessageChannel = class {
        port1 = { onmessage: null as null | ((e: unknown) => void) }
        port2 = {
          postMessage: () =>
            setTimeout(() => this.port1.onmessage?.({ data: null }), 0),
        }
      } as unknown as typeof MessageChannel
      const { onCommit } = renderEditable()
      focus(cellAt(1, 'owner'))
      press('Enter')
      const combobox = screen.getByRole('combobox', { name: 'Owner' })
      expect(combobox).toHaveAttribute('aria-expanded', 'true')

      // Act — rc-select reads keyCode, and closes the list a task later
      const escape = { key: 'Escape', keyCode: 27, which: 27 }
      fireEvent.keyDown(combobox, escape)
      await act(() => new Promise((resolve) => setTimeout(resolve, 10)))
      const expanded = combobox.getAttribute('aria-expanded')
      const afterFirst = openEditor()
      fireEvent.keyDown(combobox, escape)
      global.MessageChannel = stub

      // Assert
      expect(expanded).toBe('false')
      expect(afterFirst).not.toBeNull()
      expect(openEditor()).toBeNull()
      expect(document.activeElement).toBe(cellAt(1, 'owner'))
      expect(onCommit).not.toHaveBeenCalled()
    })

    it('commits once for Enter followed by a click', async () => {
      // Arrange
      const pending = deferred()
      const onCommit = jest.fn(() => pending.promise)
      renderEditable({ onCommit })
      editName(0, 'Alpha 2')

      // Act
      press('Enter')
      fireEvent.pointerDown(
        document.querySelector('[data-grid-body-viewport]')!,
      )
      fireEvent.blur(document.activeElement!, { relatedTarget: document.body })
      await act(async () => pending.resolve())

      // Assert
      expect(onCommit).toHaveBeenCalledTimes(1)
    })

    it('never takes keys typed outside the grid', () => {
      // Arrange
      render(
        <>
          <input aria-label="Elsewhere" />
          <WaydGrid<Item>
            data={ITEMS}
            columns={editableColumns}
            includeIdColumn={false}
            onCommit={jest.fn(() => Promise.resolve())}
          />
        </>,
      )
      focus(cellAt(0, 'name'))
      press('Enter')
      const elsewhere = screen.getByRole('textbox', { name: 'Elsewhere' })

      // Act
      const results = [
        'ArrowDown',
        'ArrowUp',
        'Enter',
        'Escape',
        'Tab',
        'a',
      ].map((key) => fireEvent.keyDown(elsewhere, { key }))

      // Assert — every event kept its default, and the editor is untouched
      expect(results).toEqual([true, true, true, true, true, true])
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue('Alpha')
    })
  })

  describe('paired fields', () => {
    // Text controls stand in for date pickers: the pairing is the same for
    // any editor, and jsdom cannot drive a picker panel.
    const [pairStart, pairEnd] = pairedEditors<
      Item,
      string | null,
      string | null
    >({
      start: textEditor<Item>({
        field: 'start',
        get: (row) => row.start ?? '',
        apply: (row, start) => ({ ...row, start }),
      }) as never,
      end: textEditor<Item>({
        field: 'end',
        get: (row) => row.end ?? '',
        apply: (row, end) => ({ ...row, end }),
      }) as never,
      validate: ({ start, end }) =>
        start && end && end < start
          ? { end: 'End cannot be before Start.' }
          : undefined,
    })
    const pairColumns = editableColumns.map((column) =>
      column.id === 'start'
        ? { ...column, meta: { editor: pairStart } }
        : column.id === 'end'
          ? { ...column, meta: { editor: pairEnd } }
          : column,
    )

    const editStart = (value: string) => {
      focus(cellAt(0, 'start'))
      press('Enter')
      typeInEditor(value)
    }

    it('holds Start while moving to End, then sends both together', async () => {
      // Arrange
      const { onCommit } = renderEditable({ columns: pairColumns })
      // A Start valid on its own, so only the pairing keeps it from sending
      editStart('2026-03-10')

      // Act
      press('Tab')
      await flush()
      const startState = cellAt(0, 'start').getAttribute('data-edit-state')
      const callsWhileInPair = onCommit.mock.calls.length
      typeInEditor('2026-04-30')
      press('Enter')
      await flush()

      // Assert
      expect(startState).toBe('unsaved')
      expect(callsWhileInPair).toBe(0)
      expect(onCommit).toHaveBeenCalledTimes(1)
      expect(onCommit).toHaveBeenCalledWith('1', {
        start: '2026-03-10',
        end: '2026-04-30',
      })
    })

    it('sends a held value when a click selects the other cell', async () => {
      // Arrange
      const { onCommit } = renderEditable({ columns: pairColumns })
      editStart('2026-03-05')

      // Act — a click only selects End; no editor opens there
      fireEvent.pointerDown(cellAt(0, 'end'))
      await flush()

      // Assert
      expect(onCommit).toHaveBeenCalledWith('1', { start: '2026-03-05' })
    })

    it('sends only the member that changed', async () => {
      // Arrange
      const { onCommit } = renderEditable({ columns: pairColumns })
      editStart('2026-03-05')

      // Act
      press('Tab')
      press('Enter')
      await flush()

      // Assert
      expect(onCommit).toHaveBeenCalledWith('1', { start: '2026-03-05' })
    })

    it('checks the pair together and sends nothing when it fails', async () => {
      // Arrange — Start moves past the unchanged End
      const { onCommit } = renderEditable({ columns: pairColumns })
      editStart('2026-05-01')

      // Act
      press('Tab')
      press('Enter')
      await flush()

      // Assert
      expect(onCommit).not.toHaveBeenCalled()
      expect(cellAt(0, 'end')).toHaveAccessibleDescription(
        'End: End cannot be before Start.',
      )
      expect(cellAt(0, 'start')).toHaveTextContent('2026-05-01')
    })
  })

  describe('date editor', () => {
    const openStart = () => {
      focus(cellAt(0, 'start'))
      press('Enter')
    }

    it('keeps a typed date when Tab commits it', async () => {
      // Arrange
      const { onCommit } = renderEditable()
      openStart()

      // Act — typed, never confirmed in the picker; Tab twice leaves the pair
      fireEvent.input(document.activeElement!, {
        target: { value: '2026-03-05' },
      })
      press('Tab')
      press('Tab')
      await flush()

      // Assert
      expect(onCommit).toHaveBeenCalledWith('1', { start: '2026-03-05' })
    })

    it('commits a typed date on the first Enter', async () => {
      // Arrange
      const { onCommit } = renderEditable()
      openStart()
      fireEvent.input(document.activeElement!, {
        target: { value: '2026-03-07' },
      })

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(onCommit).toHaveBeenCalledWith('1', { start: '2026-03-07' })
    })

    it('keeps focus in the editor when Escape closes the calendar', () => {
      // Arrange — the calendar opens with the down arrow
      renderEditable()
      openStart()
      press('ArrowDown')

      // Act
      press('Escape')

      // Assert
      expect(openEditor()).not.toBeNull()
      expect(openEditor()).toContainElement(
        document.activeElement as HTMLElement,
      )
    })

    it('reads and writes calendar dates', () => {
      // Arrange
      const row = item('1', 'A', { start: '2026-03-01' })

      // Act
      const shown = startEditor.format!(startEditor.get(row), row)
      const written = startEditor.parse!(shown.add(1, 'day'), row)

      // Assert
      expect(shown.format('YYYY-MM-DD')).toBe('2026-03-01')
      expect(written).toBe('2026-03-02')
    })
  })

  describe('drafts', () => {
    const DraftHarness = (props: GridProps & { drafts?: DraftItem[] }) => (
      <WaydGrid<Item>
        data={ITEMS}
        columns={editableColumns}
        includeIdColumn={false}
        getSubRows={(row) => row.children}
        createDraftNode={(draft) =>
          item(draft.id, '', { start: null, end: null })
        }
        leftSlot={(ctx) => (
          <Button onClick={() => ctx.addDraftAtRoot()}>New item</Button>
        )}
        onCommit={jest.fn(() => Promise.resolve())}
        {...props}
      />
    )

    it('creates the row when its Name commits, with the values staged before', async () => {
      // Arrange
      const onCreate = jest.fn(() => Promise.resolve('9'))
      render(<DraftHarness onCreate={onCreate} />)
      fireEvent.click(screen.getByRole('button', { name: 'New item' }))
      const draftRow = bodyCells('name').length - 1
      fireEvent.pointerDown(cellAt(draftRow, 'note'))
      focus(cellAt(draftRow, 'note'))
      press('x')
      press('Enter')
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('New thing')

      // Act
      press('Enter')
      fireEvent.pointerDown(
        document.querySelector('[data-grid-body-viewport]')!,
      )
      await flush()

      // Assert
      expect(onCreate).toHaveBeenCalledTimes(1)
      expect(onCreate).toHaveBeenCalledWith(null, {
        note: 'x',
        name: 'New thing',
      })
    })

    it('opens the new draft on its Name', () => {
      // Arrange
      render(<DraftHarness onCreate={jest.fn()} />)

      // Act
      fireEvent.click(screen.getByRole('button', { name: 'New item' }))

      // Assert
      const draftRow = bodyCells('name').length - 1
      expect(cellAt(draftRow, 'name')).toContainElement(
        screen.getByRole('textbox', { name: 'Name' }),
      )
    })

    it('discards an empty draft with Escape and keeps focus in the grid', () => {
      // Arrange
      const onCreate = jest.fn()
      render(<DraftHarness onCreate={onCreate} />)
      fireEvent.click(screen.getByRole('button', { name: 'New item' }))
      const rowsWithDraft = bodyCells('name').length

      // Act
      press('Escape')

      // Assert
      expect(bodyCells('name')).toHaveLength(rowsWithDraft - 1)
      expect(onCreate).not.toHaveBeenCalled()
      expect(document.activeElement).toBe(cellAt(rowsWithDraft - 2, 'name'))
    })

    /** Adds a draft and opens its Name editor. */
    const addDraft = () => {
      fireEvent.click(screen.getByRole('button', { name: 'New item' }))
      return bodyCells('name').length - 1
    }

    it('keeps what is typed in the draft while it is created', async () => {
      // Arrange
      const created = deferred()
      const onCommit = jest.fn(() => Promise.resolve())
      render(
        <DraftHarness
          onCommit={onCommit}
          onCreate={() => created.promise.then(() => 'new-1')}
        />,
      )
      addDraft()
      typeInEditor('New thing')
      press('Tab') // the create goes out; Note opens on the draft
      typeInEditor('typed during create')

      // Act
      await act(async () => created.resolve())

      // Assert
      expect(onCommit).toHaveBeenCalledWith('new-1', {
        note: 'typed during create',
      })
    })

    it('retries the create once the Name is fixed after a failed create', async () => {
      // Arrange
      const onCreate = jest
        .fn()
        .mockImplementationOnce(() => Promise.reject({ status: 500 }))
        .mockImplementation(() => Promise.resolve('new-1'))
      render(<DraftHarness onCreate={onCreate} />)
      const draftRow = addDraft()
      fireEvent.pointerDown(cellAt(draftRow, 'note'))
      focus(cellAt(draftRow, 'note'))
      press('x')
      press('Enter')
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('First try')
      press('Enter')
      await flush()

      // Act
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('Second try')
      press('Enter')
      await flush()

      // Assert
      expect(onCreate).toHaveBeenCalledTimes(2)
      expect(onCreate).toHaveBeenLastCalledWith(null, {
        note: 'x',
        name: 'Second try',
      })
    })

    it('never sends the dates of a draft through onCommit', async () => {
      // Arrange
      const onCommit = jest.fn(() => Promise.resolve())
      render(<DraftHarness onCommit={onCommit} onCreate={jest.fn()} />)
      const draftRow = addDraft()
      fireEvent.pointerDown(cellAt(draftRow, 'start'))
      focus(cellAt(draftRow, 'start'))
      press('Enter')
      fireEvent.input(document.activeElement!, {
        target: { value: '2026-03-05' },
      })

      // Act — on to End, then Escape there
      press('Tab')
      press('Escape')
      await flush()

      // Assert
      expect(onCommit).not.toHaveBeenCalled()
    })

    describe('until the new row is in the data', () => {
      const created = item('new-1', 'New thing', { start: null, end: null })

      /** Creates a draft named "New thing"; onCreate resolves with new-1. */
      const createOne = async () => {
        const utils = render(
          <DraftHarness onCreate={jest.fn(() => Promise.resolve('new-1'))} />,
        )
        addDraft()
        typeInEditor('New thing')
        press('Enter')
        await flush()
        return utils
      }

      const named = (name: string) =>
        bodyCells('name').filter((cell) => cell.textContent?.trim() === name)

      it('keeps the draft on screen with what was typed', async () => {
        // Arrange / Act
        await createOne()

        // Assert
        expect(named('New thing')).toHaveLength(1)
        expect(bodyCells('name')).toHaveLength(ITEMS.length + 1)
      })

      it('does not edit the waiting draft', async () => {
        // Arrange
        await createOne()
        focus(named('New thing')[0])

        // Act
        press('Enter')

        // Assert
        expect(openEditor()).toBeNull()
      })

      it('shows the new row in its place, never both', async () => {
        // Arrange — every set of rows the grid displays is recorded
        const shown: Item[][] = []
        const onDisplayedRowsChange = (rows: Item[]) => shown.push(rows)
        const onCreate = jest.fn(() => Promise.resolve('new-1'))
        const { rerender } = render(
          <DraftHarness
            onCreate={onCreate}
            onDisplayedRowsChange={onDisplayedRowsChange}
          />,
        )
        addDraft()
        typeInEditor('New thing')
        press('Enter')
        await flush()

        // Act
        rerender(
          <DraftHarness
            data={[...ITEMS, created]}
            onCreate={onCreate}
            onDisplayedRowsChange={onDisplayedRowsChange}
          />,
        )
        await flush()

        // Assert
        const most = Math.max(
          ...shown.map(
            (rows) => rows.filter((row) => row.name === 'New thing').length,
          ),
        )
        expect(most).toBe(1)
        expect(named('New thing')).toHaveLength(1)
        expect(bodyCells('name')).toHaveLength(ITEMS.length + 1)
      })

      it('lets the draft go after two data changes without its row', async () => {
        // Arrange
        const { rerender } = await createOne()
        const onCreate = jest.fn()

        // Act — two refetches that do not bring the row
        rerender(<DraftHarness data={[...ITEMS]} onCreate={onCreate} />)
        await flush()
        rerender(<DraftHarness data={[...ITEMS]} onCreate={onCreate} />)
        await flush()

        // Assert
        expect(named('New thing')).toHaveLength(0)
        expect(bodyCells('name')).toHaveLength(ITEMS.length)
      })
    })

    it('retries a failed create when any of its cells is changed', async () => {
      // Arrange
      const onCreate = jest
        .fn()
        .mockImplementationOnce(() => Promise.reject({ status: 500 }))
        .mockImplementation(() => Promise.resolve('new-1'))
      render(<DraftHarness onCreate={onCreate} />)
      const draftRow = addDraft()
      typeInEditor('First try')
      press('Enter')
      await flush()

      // Act
      focus(cellAt(draftRow, 'note'))
      press('x')
      press('Enter')
      await flush()

      // Assert
      expect(onCreate.mock.calls).toStrictEqual([
        [null, { name: 'First try' }],
        [null, { name: 'First try', note: 'x' }],
      ])
    })

    describe('cells required to create', () => {
      // Text controls stand in for date pickers, as in the paired tests.
      const [needStart, needEnd] = pairedEditors<
        Item,
        string | null,
        string | null
      >({
        start: textEditor<Item>({
          field: 'start',
          get: (row) => row.start ?? '',
          apply: (row, start) => ({ ...row, start }),
          requiredToCreate: true,
        }) as never,
        end: textEditor<Item>({
          field: 'end',
          get: (row) => row.end ?? '',
          apply: (row, end) => ({ ...row, end }),
          requiredToCreate: true,
        }) as never,
        validate: ({ start, end }) =>
          start && end && end < start
            ? { end: 'End cannot be before Start.' }
            : undefined,
      })
      const columnsWith = (end = needEnd) =>
        editableColumns.map((column) =>
          column.id === 'start'
            ? { ...column, meta: { editor: needStart } }
            : column.id === 'end'
              ? { ...column, meta: { editor: end } }
              : column,
        )

      /** Opens a draft cell and types into it. */
      const fill = (row: number, columnId: string, value: string) => {
        focus(cellAt(row, columnId))
        press('Enter')
        typeInEditor(value)
      }

      it('waits for them, marking each, and creates once the last is filled', async () => {
        // Arrange
        const onCreate = jest.fn(() => Promise.resolve('new-1'))
        render(<DraftHarness columns={columnsWith()} onCreate={onCreate} />)
        const draftRow = addDraft()
        typeInEditor('Plan')
        press('Tab')
        await flush()
        const callsAfterName = onCreate.mock.calls.length
        const startMark = cellAt(draftRow, 'start').getAttribute(
          'data-edit-state',
        )

        // Act
        fill(draftRow, 'start', '2026-03-01')
        press('Tab')
        typeInEditor('2026-03-31')
        press('Enter')
        await flush()

        // Assert
        expect(callsAfterName).toBe(0)
        expect(startMark).toBe('error')
        expect(onCreate.mock.calls).toStrictEqual([
          [null, { name: 'Plan', start: '2026-03-01', end: '2026-03-31' }],
        ])
      })

      it('says which cells it still needs once it has a Name', async () => {
        // Arrange
        render(<DraftHarness columns={columnsWith()} onCreate={jest.fn()} />)
        const draftRow = addDraft()
        typeInEditor('Plan')

        // Act
        press('Tab')
        await flush()

        // Assert
        expect(cellAt(draftRow, 'start')).toHaveAccessibleDescription(
          'Start: Required to create the item.',
        )
        expect(cellAt(draftRow, 'end')).toHaveAccessibleDescription(
          'End: Required to create the item.',
        )
      })

      it('marks nothing before the draft has a Name', async () => {
        // Arrange
        render(<DraftHarness columns={columnsWith()} onCreate={jest.fn()} />)
        const draftRow = addDraft()

        // Act — moving to Note leaves the Name empty
        fill(draftRow, 'note', 'x')
        press('Enter')
        await flush()

        // Assert
        expect(cellAt(draftRow, 'start')).not.toHaveAttribute('data-edit-state')
      })

      it('creates when the Name comes last', async () => {
        // Arrange
        const onCreate = jest.fn(() => Promise.resolve('new-1'))
        render(<DraftHarness columns={columnsWith()} onCreate={onCreate} />)
        const draftRow = addDraft()
        fill(draftRow, 'start', '2026-03-01')
        press('Tab')
        typeInEditor('2026-03-31')
        press('Enter')

        // Act
        fill(draftRow, 'name', 'Plan')
        press('Enter')
        await flush()

        // Assert
        expect(onCreate.mock.calls).toStrictEqual([
          [null, { start: '2026-03-01', end: '2026-03-31', name: 'Plan' }],
        ])
      })

      it('opens the first cell still needed with Enter on the Name', async () => {
        // Arrange
        render(<DraftHarness columns={columnsWith()} onCreate={jest.fn()} />)
        const draftRow = addDraft()
        typeInEditor('Plan')

        // Act
        press('Enter')
        await flush()

        // Assert
        expect(cellAt(draftRow, 'start')).toContainElement(openEditor())
        expect(openEditor()).toContainElement(
          document.activeElement as HTMLElement,
        )
      })

      it('checks the pair before creating', async () => {
        // Arrange
        const onCreate = jest.fn(() => Promise.resolve('new-1'))
        render(<DraftHarness columns={columnsWith()} onCreate={onCreate} />)
        const draftRow = addDraft()
        typeInEditor('Plan')
        press('Tab')
        fill(draftRow, 'start', '2026-03-10')
        press('Tab')
        typeInEditor('2026-03-01')

        // Act
        press('Enter')
        await flush()

        // Assert
        expect(onCreate).not.toHaveBeenCalled()
        expect(cellAt(draftRow, 'end')).toHaveAccessibleDescription(
          'End: End cannot be before Start.',
        )
      })

      it('does not wait for a cell the draft cannot edit', async () => {
        // Arrange
        const onCreate = jest.fn(() => Promise.resolve('new-1'))
        const lockedEnd = { ...needEnd, editable: () => false }
        render(
          <DraftHarness columns={columnsWith(lockedEnd)} onCreate={onCreate} />,
        )
        const draftRow = addDraft()
        typeInEditor('Plan')
        press('Tab')

        // Act
        fill(draftRow, 'start', '2026-03-01')
        press('Enter')
        await flush()

        // Assert
        expect(onCreate.mock.calls).toStrictEqual([
          [null, { name: 'Plan', start: '2026-03-01' }],
        ])
      })

      it('says aloud that the draft was not created yet', async () => {
        // Arrange
        render(<DraftHarness columns={columnsWith()} onCreate={jest.fn()} />)
        addDraft()
        typeInEditor('Plan')

        // Act
        press('Tab')
        await flush()

        // Assert
        expect(
          screen.getAllByRole('status').map((region) => region.textContent),
        ).toContain(
          'Not created yet. Start: Required to create the item. End: Required to create the item.',
        )
      })

      it('says aloud when the pair check refuses the create', async () => {
        // Arrange
        render(<DraftHarness columns={columnsWith()} onCreate={jest.fn()} />)
        const draftRow = addDraft()
        typeInEditor('Plan')
        press('Tab')
        fill(draftRow, 'start', '2026-03-10')
        press('Tab')
        typeInEditor('2026-03-01')

        // Act
        press('Enter')
        await flush()

        // Assert
        expect(
          screen.getAllByRole('status').map((region) => region.textContent),
        ).toContain('Not created. End: End cannot be before Start.')
      })

      it('leaves a hidden required cell to the server', async () => {
        // Arrange
        const onCreate = jest.fn(() => Promise.resolve('new-1'))
        const columns = columnsWith().map((column) =>
          column.id === 'end'
            ? { ...column, meta: { ...column.meta, hiddenByDefault: true } }
            : column,
        )
        render(<DraftHarness columns={columns} onCreate={onCreate} />)
        const draftRow = addDraft()
        typeInEditor('Plan')
        press('Tab')

        // Act
        fill(draftRow, 'start', '2026-03-01')
        press('Enter')
        await flush()

        // Assert
        expect(onCreate.mock.calls).toStrictEqual([
          [null, { name: 'Plan', start: '2026-03-01' }],
        ])
      })

      it('keeps the other date when Escape is pressed on an empty required cell', async () => {
        // Arrange
        const onCreate = jest.fn(() => Promise.resolve('new-1'))
        render(<DraftHarness columns={columnsWith()} onCreate={onCreate} />)
        const draftRow = addDraft()
        typeInEditor('Plan')
        press('Tab')
        fill(draftRow, 'end', '2026-03-31')
        press('Enter')
        await flush()
        focus(cellAt(draftRow, 'start'))

        // Act
        press('Escape')
        fill(draftRow, 'start', '2026-03-01')
        press('Enter')
        await flush()

        // Assert
        expect(onCreate.mock.calls).toStrictEqual([
          [null, { name: 'Plan', end: '2026-03-31', start: '2026-03-01' }],
        ])
      })

      it('lets Enter leave an empty Name', async () => {
        // Arrange
        render(<DraftHarness columns={columnsWith()} onCreate={jest.fn()} />)
        const draftRow = addDraft()
        fill(draftRow, 'start', '2026-03-01')
        press('Enter')
        focus(cellAt(draftRow, 'name'))
        press('Enter')

        // Act
        press('Enter')
        await flush()

        // Assert
        expect(openEditor()).toBeNull()
      })

      it('does not count a blank Name as named', async () => {
        // Arrange
        render(<DraftHarness columns={columnsWith()} onCreate={jest.fn()} />)
        const draftRow = addDraft()
        typeInEditor('   ')

        // Act
        press('Tab')
        await flush()

        // Assert
        expect(cellAt(draftRow, 'start')).not.toHaveAttribute('data-edit-state')
      })
    })
  })

  describe('tree column editor', () => {
    const TREE = [
      item('1', 'Alpha', { children: [item('11', 'Alpha child')] }),
      item('2', 'Beta'),
    ]

    it('sits past a nested row’s indent', () => {
      // Arrange
      render(
        <WaydGrid<Item>
          data={TREE}
          columns={editableColumns}
          includeIdColumn={false}
          getSubRows={(row) => row.children}
          onCommit={jest.fn(() => Promise.resolve())}
        />,
      )

      // Act
      focus(cellAt(1, 'name'))
      press('Enter')

      // Assert
      expect(
        openEditor()!.parentElement!.style.getPropertyValue(
          '--wayd-grid-tree-depth',
        ),
      ).toBe('1')
    })

    it('is not indented in a flat grid', () => {
      // Arrange
      renderEditable()

      // Act
      focus(cellAt(0, 'name'))
      press('Enter')

      // Assert
      expect(openEditor()!.parentElement!.tagName).toBe('TD')
    })
  })

  describe('committing from outside a cell (commitCells)', () => {
    it('sends the changed values as one patch, marked saving', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const saved = deferred()
      const onCommit = jest.fn(() => saved.promise)
      renderEditable({ ref, onCommit })

      // Act
      act(() =>
        ref.current!.commitCells('1', {
          start: '2026-03-05',
          end: '2026-04-04',
        }),
      )
      await flush()

      // Assert
      expect(onCommit.mock.calls).toStrictEqual([
        ['1', { start: '2026-03-05', end: '2026-04-04' }],
      ])
      expect(cellAt(0, 'start')).toHaveAttribute('data-edit-state', 'saving')
      expect(cellAt(0, 'start')).toHaveTextContent('2026-03-05')
      await act(async () => saved.resolve())
    })

    it('leaves out a value the row already has', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const { onCommit } = renderEditable({ ref })

      // Act
      act(() =>
        ref.current!.commitCells('1', {
          start: '2026-03-01',
          end: '2026-04-04',
        }),
      )
      await flush()

      // Assert
      expect(onCommit.mock.calls).toStrictEqual([['1', { end: '2026-04-04' }]])
    })

    it('runs the pair’s check first, and keeps the row’s own values when it fails', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const { onCommit } = renderEditable({ ref })

      // Act
      act(() =>
        ref.current!.commitCells('1', {
          start: '2026-03-10',
          end: '2026-03-05',
        }),
      )
      await flush()

      // Assert
      expect(onCommit).not.toHaveBeenCalled()
      expect(cellAt(0, 'end')).not.toHaveAttribute('data-edit-state')
      expect(cellAt(0, 'end')).toHaveTextContent('2026-03-31')
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).toContain('Not saved. End: End cannot be before Start.')
    })
    it('waits for a commit already running for the row', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const first = deferred()
      const onCommit = jest
        .fn()
        .mockImplementationOnce(() => first.promise)
        .mockImplementation(() => Promise.resolve())
      renderEditable({ ref, onCommit })
      editName(0, 'Alpha 2')
      press('Enter')

      // Act
      act(() => ref.current!.commitCells('1', { end: '2026-04-04' }))
      await flush()
      const callsBeforeFirstSettles = onCommit.mock.calls.length
      await act(async () => first.resolve())

      // Assert
      expect(callsBeforeFirstSettles).toBe(1)
      expect(onCommit.mock.calls).toStrictEqual([
        ['1', { name: 'Alpha 2' }],
        ['1', { end: '2026-04-04' }],
      ])
    })

    it('sends the way back when values return to the row’s while still saving', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const first = deferred()
      const onCommit = jest
        .fn()
        .mockImplementationOnce(() => first.promise)
        .mockImplementation(() => Promise.resolve())
      renderEditable({ ref, onCommit })
      act(() => ref.current!.commitCells('1', { end: '2026-04-04' }))
      await flush()

      // Act — back to the row's own End before the first save lands
      act(() => ref.current!.commitCells('1', { end: '2026-03-31' }))
      await act(async () => first.resolve())

      // Assert
      expect(onCommit.mock.calls).toStrictEqual([
        ['1', { end: '2026-04-04' }],
        ['1', { end: '2026-03-31' }],
      ])
    })

    it('drops a value that failed in its cell when the row’s own comes back', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const onCommit = jest
        .fn()
        .mockImplementationOnce(() =>
          Promise.reject({ status: 500, detail: 'Boom.' }),
        )
        .mockImplementation(() => Promise.resolve())
      renderEditable({ ref, onCommit })
      const end = cellAt(0, 'end')
      focus(end)
      press('Enter')
      fireEvent.input(document.activeElement!, {
        target: { value: '2026-04-04' },
      })
      press('Enter')
      await flush()

      // Act
      act(() => ref.current!.commitCells('1', { end: '2026-03-31' }))
      await flush()

      // Assert
      expect(onCommit).toHaveBeenCalledTimes(1)
      expect(cellAt(0, 'end')).not.toHaveAttribute('data-edit-state')
      expect(cellAt(0, 'end')).toHaveTextContent('2026-03-31')
    })
    it('checks the pair with the value held on its other cell', async () => {
      // Arrange — End failed at Mar 5 in its cell, earlier than the Start below
      const ref = createRef<WaydGridHandle>()
      const onCommit = jest
        .fn()
        .mockImplementationOnce(() =>
          Promise.reject({ status: 500, detail: 'Boom.' }),
        )
        .mockImplementation(() => Promise.resolve())
      renderEditable({ ref, onCommit })
      const end = cellAt(0, 'end')
      focus(end)
      press('Enter')
      fireEvent.input(document.activeElement!, {
        target: { value: '2026-03-05' },
      })
      press('Enter')
      await flush()

      // Act
      act(() => ref.current!.commitCells('1', { start: '2026-03-10' }))
      await flush()

      // Assert
      expect(onCommit).toHaveBeenCalledTimes(1)
      expect(cellAt(0, 'start')).toHaveTextContent('2026-03-01')
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).toContain('Not saved. End: End cannot be before Start.')
    })
    it('does nothing on a draft row', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const onCommit = jest.fn(() => Promise.resolve())
      render(
        <WaydGrid<Item>
          ref={ref}
          data={ITEMS}
          columns={editableColumns}
          includeIdColumn={false}
          getSubRows={(row) => row.children}
          createDraftNode={(draft) =>
            item(draft.id, '', { start: null, end: null })
          }
          leftSlot={(ctx) => (
            <Button onClick={() => ctx.addDraftAtRoot()}>New item</Button>
          )}
          onCommit={onCommit}
          onCreate={jest.fn()}
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: 'New item' }))
      const draftId = ref
        .current!.getDisplayedRows()
        .map((row) => (row as Item).id)
        .find((id) => id.startsWith('draft-'))!

      // Act
      act(() => ref.current!.commitCells(draftId, { note: 'x' }))
      await flush()

      // Assert
      expect(onCommit).not.toHaveBeenCalled()
      expect(cellAt(bodyCells('note').length - 1, 'note')).not.toHaveAttribute(
        'data-edit-state',
      )
    })

    it('keeps a saved value shown when the same value comes again', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const { onCommit } = renderEditable({ ref })
      act(() => ref.current!.commitCells('1', { end: '2026-04-04' }))
      await flush()

      // Act
      act(() => ref.current!.commitCells('1', { end: '2026-04-04' }))
      await flush()

      // Assert
      expect(onCommit).toHaveBeenCalledTimes(1)
      expect(cellAt(0, 'end')).toHaveTextContent('2026-04-04')
    })

    it('replaces a value that failed in its cell', async () => {
      // Arrange — End typed into its cell fails to save
      const ref = createRef<WaydGridHandle>()
      const onCommit = jest
        .fn()
        .mockImplementationOnce(() =>
          Promise.reject({ status: 500, detail: 'Boom.' }),
        )
        .mockImplementation(() => Promise.resolve())
      renderEditable({ ref, onCommit })
      const end = cellAt(0, 'end')
      focus(end)
      press('Enter')
      fireEvent.input(document.activeElement!, {
        target: { value: '2026-04-04' },
      })
      press('Enter')
      await flush()
      const failed = cellAt(0, 'end').getAttribute('data-edit-state')

      // Act
      act(() => ref.current!.commitCells('1', { end: '2026-04-10' }))
      await flush()

      // Assert
      expect(failed).toBe('error')
      expect(onCommit.mock.calls[1]).toStrictEqual(['1', { end: '2026-04-10' }])
      expect(cellAt(0, 'end')).not.toHaveAttribute('data-edit-state', 'error')
      expect(cellAt(0, 'end')).toHaveTextContent('2026-04-10')
    })
    it('goes back to the row’s own values when the save fails, and says why', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      render(
        <MessageProvider>
          <WaydGrid<Item>
            ref={ref}
            data={ITEMS}
            columns={editableColumns}
            includeIdColumn={false}
            onCommit={jest.fn(() =>
              Promise.reject({ status: 422, errors: { End: ['Too late.'] } }),
            )}
          />
        </MessageProvider>,
      )

      // Act
      act(() => ref.current!.commitCells('1', { end: '2026-04-04' }))
      await flush()

      // Assert
      expect(cellAt(0, 'end')).toHaveTextContent('2026-03-31')
      expect(cellAt(0, 'end')).not.toHaveAttribute('data-edit-state')
      // Once, in the toast: it is read out, so the live region stays quiet
      expect(
        await screen.findAllByText('Not saved. End: Too late.'),
      ).toHaveLength(1)
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).not.toContain('Not saved. End: Too late.')
    })
    it('leaves an editor open on another row alone', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      renderEditable({ ref })
      editName(1, 'Beta typed')

      // Act
      act(() => ref.current!.commitCells('1', { end: '2026-04-04' }))
      await flush()

      // Assert
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue(
        'Beta typed',
      )
      expect(cellAt(1, 'name')).toContainElement(openEditor())
    })

    it('ignores a column the row cannot edit', async () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const lockedEnd = { ...endEditor, editable: () => false }
      const { onCommit } = renderEditable({
        ref,
        columns: editableColumns.map((column) =>
          column.id === 'end'
            ? { ...column, meta: { editor: lockedEnd } }
            : column,
        ),
      })

      // Act
      act(() =>
        ref.current!.commitCells('1', {
          start: '2026-03-02',
          end: '2026-04-04',
        }),
      )
      await flush()

      // Assert
      expect(onCommit.mock.calls).toStrictEqual([
        ['1', { start: '2026-03-02' }],
      ])
    })
  })

  describe('a cell required to create only on some drafts', () => {
    const DraftHarness = (props: GridProps) => (
      <WaydGrid<Item>
        data={ITEMS}
        includeIdColumn={false}
        getSubRows={(row) => row.children}
        createDraftNode={(draft) =>
          item(draft.id, '', { start: null, end: null })
        }
        leftSlot={(ctx) => (
          <Button onClick={() => ctx.addDraftAtRoot()}>New item</Button>
        )}
        onCommit={jest.fn(() => Promise.resolve())}
        columns={editableColumns.map((column) =>
          column.id === 'start'
            ? {
                ...column,
                meta: {
                  editor: {
                    ...startEditor,
                    requiredToCreate: (row: Item) => row.note === 'dated',
                  },
                },
              }
            : column,
        )}
        {...props}
      />
    )

    const addDraft = () => {
      fireEvent.click(screen.getByRole('button', { name: 'New item' }))
      return bodyCells('name').length - 1
    }

    it('creates on the Name alone where the row does not need it', async () => {
      // Arrange
      const onCreate = jest.fn(() => Promise.resolve('new-1'))
      render(<DraftHarness onCreate={onCreate} />)
      addDraft()
      typeInEditor('Plan')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(onCreate.mock.calls).toStrictEqual([[null, { name: 'Plan' }]])
    })

    it('checks its pair against the draft’s staged values', async () => {
      // Arrange — a draft whose Note says "single" needs no End
      const [singleStart, singleEnd] = pairedEditors<
        Item,
        string | null,
        string | null
      >({
        start: textEditor<Item>({
          field: 'start',
          get: (row) => row.start ?? '',
          apply: (row, start) => ({ ...row, start }),
        }) as never,
        end: textEditor<Item>({
          field: 'end',
          get: (row) => row.end ?? '',
          apply: (row, end) => ({ ...row, end }),
        }) as never,
        validate: ({ start, end }, row) =>
          row.note !== 'single' && start && !end
            ? { end: 'End is needed.' }
            : undefined,
      })
      const onCreate = jest.fn(() => Promise.resolve('new-1'))
      render(
        <DraftHarness
          onCreate={onCreate}
          columns={editableColumns.map((column) =>
            column.id === 'start'
              ? { ...column, meta: { editor: singleStart } }
              : column.id === 'end'
                ? { ...column, meta: { editor: singleEnd } }
                : column,
          )}
        />,
      )
      const draftRow = addDraft()
      press('Tab') // on to Note, leaving the Name empty
      typeInEditor('single')
      press('Enter')
      focus(cellAt(draftRow, 'start'))
      press('Enter')
      typeInEditor('2026-03-01')
      press('Enter')
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('Plan')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(onCreate.mock.calls).toStrictEqual([
        [null, { note: 'single', start: '2026-03-01', name: 'Plan' }],
      ])
    })

    it('checks every staged value again before creating', async () => {
      // Arrange — a Note valid while the draft is unnamed fails once it is "Plan"
      const strictNote = textEditor<Item>({
        field: 'note',
        get: (row) => row.note,
        apply: (row, note) => ({ ...row, note }),
        validate: (note, row) =>
          row.name === 'Plan' && note === 'x' ? 'Not on a plan.' : undefined,
      })
      const onCreate = jest.fn(() => Promise.resolve('new-1'))
      render(
        <DraftHarness
          onCreate={onCreate}
          columns={editableColumns.map((column) =>
            column.id === 'note'
              ? { ...column, meta: { editor: strictNote } }
              : column,
          )}
        />,
      )
      const draftRow = addDraft()
      press('Tab') // on to Note, leaving the Name empty
      typeInEditor('x')
      press('Enter')
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('Plan')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(onCreate).not.toHaveBeenCalled()
      expect(cellAt(draftRow, 'note')).toHaveAccessibleDescription(
        'Note: Not on a plan.',
      )
    })

    it('waits for it where the row’s staged values need it', async () => {
      // Arrange
      const onCreate = jest.fn(() => Promise.resolve('new-1'))
      render(<DraftHarness onCreate={onCreate} />)
      const draftRow = addDraft()
      press('Tab') // on to Note, leaving the Name empty
      typeInEditor('dated')
      press('Enter')
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('Plan')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(onCreate).not.toHaveBeenCalled()
      expect(cellAt(draftRow, 'start')).toHaveAccessibleDescription(
        'Start: Required to create the item.',
      )
    })
  })

  describe('beside other grid features', () => {
    /** Renders a row's sortable state as seen by its drag handle. */
    const DragProbe = () => {
      const { attributes } = useGridDragHandle()
      return (
        <span
          data-testid="drag-probe"
          aria-disabled={attributes['aria-disabled']}
        />
      )
    }

    const withDragProbe: ColumnDef<Item, unknown>[] = [
      ...editableColumns,
      { id: 'drag', header: 'Drag', cell: () => <DragProbe /> },
    ]

    it('turns dragging off while a cell editor is open, and back on after', () => {
      // Arrange
      const seen: boolean[] = []
      renderEditable({
        getSubRows: (row) => row.children,
        enableDragAndDrop: true,
        onNodeMove: jest.fn(() => Promise.resolve()),
        columns: (context) => {
          seen.push(context.isDragEnabled)
          return editableColumns
        },
      })
      const before = seen.at(-1)
      focus(cellAt(0, 'name'))

      // Act
      press('Enter')
      const whileOpen = seen.at(-1)
      press('Escape')

      // Assert
      expect(before).toBe(true)
      expect(whileOpen).toBe(false)
      expect(seen.at(-1)).toBe(true)
    })

    it('never lets a draft row be dragged', () => {
      // Arrange
      render(
        <WaydGrid<Item>
          data={ITEMS}
          columns={withDragProbe}
          includeIdColumn={false}
          getSubRows={(row) => row.children}
          enableDragAndDrop
          onNodeMove={jest.fn(() => Promise.resolve())}
          createDraftNode={(draft) =>
            item(draft.id, '', { start: null, end: null })
          }
          leftSlot={(ctx) => (
            <Button onClick={() => ctx.addDraftAtRoot()}>New item</Button>
          )}
          onCommit={jest.fn(() => Promise.resolve())}
          onCreate={jest.fn()}
        />,
      )
      fireEvent.click(screen.getByRole('button', { name: 'New item' }))
      const draftRow = bodyCells('name').length - 1
      fireEvent.pointerDown(cellAt(draftRow, 'note'))
      focus(cellAt(draftRow, 'note'))
      press('x')

      // Act — the note is staged on the draft and its editor closes
      press('Enter')

      // Assert
      expect(openEditor()).toBeNull()
      const probes = screen.getAllByTestId('drag-probe')
      expect(probes[draftRow]).toHaveAttribute('aria-disabled', 'true')
      expect(probes[0]).toHaveAttribute('aria-disabled', 'false')
    })

    it('moves into a link with Enter on a column that has no editor', () => {
      // Arrange
      renderEditable({
        columns: editableColumns.map((column): ColumnDef<Item, unknown> =>
          column.id === 'code'
            ? {
                ...column,
                cell: ({ row }) => (
                  <a href={`#${row.original.id}`} tabIndex={-1}>
                    {row.original.code}
                  </a>
                ),
              }
            : column,
        ),
      })
      focus(cellAt(0, 'code'))

      // Act
      press('Enter')

      // Assert
      expect(openEditor()).toBeNull()
      expect(document.activeElement).toBe(
        within(cellAt(0, 'code')).getByRole('link', { name: 'C-1' }),
      )
    })
  })

  describe('the selected cell', () => {
    const viewport = () =>
      document.querySelector<HTMLElement>('[data-grid-body-viewport]')!

    it('is outlined however it was chosen in a grid that edits', () => {
      // Arrange / Act
      renderEditable()

      // Assert
      expect(viewport().className).toContain('editsByCell')
    })

    it('keeps the keyboard-only outline in a grid that does not edit', () => {
      // Arrange / Act
      render(
        <WaydGrid<Item>
          data={ITEMS}
          columns={editableColumns}
          includeIdColumn={false}
        />,
      )

      // Assert
      expect(viewport().className).not.toContain('editsByCell')
    })
  })

  describe('key hints', () => {
    const hints = () =>
      document.querySelector<HTMLElement>('[data-grid-key-hints]')

    it('are not shown until a cell is selected', () => {
      // Arrange / Act
      renderEditable()

      // Assert
      expect(hints()).toBeNull()
    })

    it('show the cell keys once a cell is selected', () => {
      // Arrange
      renderEditable()

      // Act
      focus(cellAt(0, 'name'))

      // Assert
      expect(hints()).toHaveTextContent('Enter')
      expect(hints()).toHaveTextContent('edit')
      expect(hints()).toHaveTextContent('leave grid')
    })

    it('leave out editing on a cell that does not edit', () => {
      // Arrange
      renderEditable()

      // Act
      focus(cellAt(0, 'code'))

      // Assert
      expect(hints()).not.toHaveTextContent('edit')
      expect(hints()).toHaveTextContent('move')
    })

    it('switch to the editor’s keys while a cell is open', () => {
      // Arrange
      renderEditable()
      focus(cellAt(0, 'name'))

      // Act
      press('Enter')

      // Assert
      expect(hints()).toHaveTextContent('save & down')
      expect(hints()).toHaveTextContent('undo')
    })

    it('show a list’s keys in a select editor', () => {
      // Arrange
      renderEditable()
      focus(cellAt(0, 'owner'))

      // Act
      press('Enter')

      // Assert
      expect(hints()).toHaveTextContent('pick')
      expect(hints()).toHaveTextContent('close list')
    })

    it('go when focus leaves the grid', () => {
      // Arrange — the window keeps focus, so the grid reads it as leaving
      jest.spyOn(document, 'hasFocus').mockReturnValue(true)
      renderEditable()
      focus(cellAt(0, 'name'))

      // Act
      act(() => (document.activeElement as HTMLElement).blur())

      // Assert
      expect(hints()).toBeNull()
    })

    it('keep their footer line before a cell is selected, so nothing moves', () => {
      // Arrange / Act
      renderEditable()

      // Assert
      expect(document.querySelector('[data-grid-footer]')).toBeInTheDocument()
      expect(hints()).toBeNull()
    })

    it('sit in the footer, under the grid’s frame', () => {
      // Arrange
      renderEditable()

      // Act
      focus(cellAt(0, 'name'))

      // Assert
      expect(document.querySelector('[data-grid-footer]')).toContainElement(
        hints(),
      )
      expect(hints()!.closest('[role="grid"]')).toBeNull()
    })

    it('are hidden from assistive technology', () => {
      // Arrange
      renderEditable()

      // Act
      focus(cellAt(0, 'name'))

      // Assert
      expect(hints()).toHaveAttribute('aria-hidden', 'true')
    })

    it('are not shown in a grid that does not edit', () => {
      // Arrange
      render(
        <WaydGrid<Item>
          data={ITEMS}
          columns={editableColumns}
          includeIdColumn={false}
        />,
      )

      // Act
      focus(cellAt(0, 'name'))

      // Assert
      expect(hints()).toBeNull()
      expect(document.querySelector('[data-grid-footer]')).toBeNull()
    })
  })

  describe('help', () => {
    it('lists the editing keys when the grid has editors', () => {
      // Arrange
      renderEditable()

      // Act
      fireEvent.click(screen.getByRole('button', { name: /help/i }))

      // Assert
      expect(screen.getByText(/^Save and move down/)).toBeInTheDocument()
    })
  })
})
