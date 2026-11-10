const mockMessage = {
  success: vi.fn(),
  error: vi.fn(),
  warning: vi.fn(),
  info: vi.fn(),
}
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

const mockPatch = vi.fn()
const mockCreate = vi.fn()
const mockPlaceActivity = vi.fn()
let mockRoadmapColors: unknown[] = []
vi.mock('@/src/store/features/planning/roadmaps-api', () => ({
  useGetRoadmapQuery: () => ({
    data: { colors: mockRoadmapColors, start: '2026-01-01', end: '2026-12-31' },
  }),
  usePatchRoadmapItemMutation: () => [mockPatch],
  useCreateRoadmapItemMutation: () => [mockCreate],
  useUpdateRoadmapActivityPlacementMutation: () => [mockPlaceActivity],
  useUpdateRoadmapItemDatesMutation: () => [vi.fn()],
}))

vi.mock('./delete-roadmap-item-form', () => ({
  default: ({
    roadmapItemId,
    onFormComplete,
    onFormCancel,
  }: {
    roadmapItemId: string
    onFormComplete: () => void
    onFormCancel: () => void
  }) => (
    <div role="dialog" aria-label="Delete roadmap item">
      {roadmapItemId}
      <button onClick={onFormComplete}>Confirm delete</button>
      <button onClick={onFormCancel}>Cancel delete</button>
    </div>
  ),
}))

// The chart pane is not under test; keep it hidden.
vi.mock(import('@/src/components/common/timeline'), async (importOriginal) => ({
  ...(await importOriginal()),
  useGanttVisibility: () => ({ visible: false, toggle: vi.fn() }),
}))

import { act, fireEvent, render, screen, within } from '@testing-library/react'
import type { RoadmapItemListDto } from '@/src/services/wayd-api'
import RoadmapItemsGrid from './roadmap-items-grid'

const ITEMS = [
  {
    id: 'a1',
    name: 'Alpha',
    type: { name: 'Activity' },
    $type: 'activity',
    start: '2026-03-01',
    end: '2026-03-31',
    color: null,
    children: [],
  },
  {
    id: 't1',
    name: 'Sprint 1',
    type: { name: 'Timebox' },
    $type: 'timebox',
    start: '2026-03-01',
    end: '2026-03-14',
    color: null,
  },
  {
    id: 'm1',
    name: 'Launch',
    type: { name: 'Milestone' },
    $type: 'milestone',
    date: '2026-04-15',
    color: null,
  },
] as unknown as RoadmapItemListDto[]

const resolved = (value?: unknown) => ({
  unwrap: () => Promise.resolve(value),
})
const rejected = (error: unknown) => ({ unwrap: () => Promise.reject(error) })

const renderGrid = ({
  items = ITEMS,
  canManage = true,
}: { items?: RoadmapItemListDto[]; canManage?: boolean } = {}) => {
  const openRoadmapItemDrawer = vi.fn()
  const refreshRoadmapItems = vi.fn().mockResolvedValue(undefined)
  const props = {
    roadmapId: 'r1',
    roadmapItemsIsLoading: false,
    refreshRoadmapItems,
    openRoadmapItemDrawer,
    canManage,
  }
  const { rerender } = render(
    <RoadmapItemsGrid {...props} roadmapItemsData={items} />,
  )
  return {
    openRoadmapItemDrawer,
    refreshRoadmapItems,
    rerender: (next: RoadmapItemListDto[]) =>
      rerender(<RoadmapItemsGrid {...props} roadmapItemsData={next} />),
  }
}

/** Opens a row's actions menu and returns its item labels. */
const openRowMenu = async (row: number) => {
  const trigger = within(cellAt(row, 'actions')).getByRole('button', {
    name: 'Row actions',
  })
  fireEvent.click(trigger)
  await flush()
  return screen.getAllByRole('menuitem').map((item) => item.textContent?.trim())
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
const typeInEditor = (value: string) =>
  fireEvent.change(document.activeElement!, { target: { value } })
const typeDate = (value: string) =>
  fireEvent.input(document.activeElement!, { target: { value } })
const flush = () => act(async () => {})

/** Opens a cell and replaces its text. */
const editText = (row: number, columnId: string, value: string) => {
  focus(cellAt(row, columnId))
  press('Enter')
  typeInEditor(value)
}

/** Adds a draft at the root; its Name editor opens. Returns its row. */
const addDraft = () => {
  fireEvent.click(screen.getByRole('button', { name: /Create Item/ }))
  return bodyCells('name').length - 1
}

describe('RoadmapItemsGrid cell editing', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockRoadmapColors = []
    mockPatch.mockReturnValue(resolved())
    mockCreate.mockReturnValue(resolved('new-1'))
  })

  it('sends only the changed field, as JSON Patch', async () => {
    // Arrange
    renderGrid()
    editText(0, 'name', 'Alpha renamed')

    // Act
    press('Enter')
    await flush()

    // Assert
    expect(mockPatch.mock.calls).toStrictEqual([
      [
        {
          roadmapId: 'r1',
          itemId: 'a1',
          patchOperations: [
            { op: 'replace', path: '/name', value: 'Alpha renamed' },
          ],
        },
      ],
    ])
  })

  it("sends a milestone's Start as its date", async () => {
    // Arrange
    renderGrid()
    focus(cellAt(2, 'start'))
    press('Enter')
    typeDate('2026-04-20')

    // Act
    press('Enter')
    await flush()

    // Assert
    expect(mockPatch.mock.calls).toStrictEqual([
      [
        {
          roadmapId: 'r1',
          itemId: 'm1',
          patchOperations: [
            { op: 'replace', path: '/date', value: '2026-04-20' },
          ],
        },
      ],
    ])
  })

  it('shows no toast when a change saves', async () => {
    // Arrange
    renderGrid()
    editText(0, 'name', 'Alpha renamed')

    // Act
    press('Enter')
    await flush()

    // Assert
    expect(mockPatch).toHaveBeenCalledTimes(1)
    expect(mockMessage.success).not.toHaveBeenCalled()
    expect(mockMessage.error).not.toHaveBeenCalled()
  })

  it("places a milestone's Date error on its Start cell", async () => {
    // Arrange — the server reports the date while the name is saved
    mockPatch.mockReturnValue(
      rejected({
        status: 422,
        data: { status: 422, errors: { Date: ['Date is out of range.'] } },
      }),
    )
    renderGrid()
    editText(2, 'name', 'Launch day')

    // Act
    press('Enter')
    await flush()

    // Assert
    expect(cellAt(2, 'start')).toHaveAccessibleDescription(
      'Start: Date is out of range.',
    )
    expect(mockMessage.error).not.toHaveBeenCalled()
  })

  it('places a field error on the cell it names', async () => {
    // Arrange
    mockPatch.mockReturnValue(
      rejected({
        status: 422,
        data: { status: 422, errors: { Name: ['Name is taken.'] } },
      }),
    )
    renderGrid()
    editText(0, 'name', 'Beta')

    // Act
    press('Enter')
    await flush()

    // Assert
    expect(cellAt(0, 'name')).toHaveAccessibleDescription(
      'Name: Name is taken.',
    )
  })

  it('saves a color picked from the palette', async () => {
    // Arrange
    mockRoadmapColors = [
      { color: '#FF0000', name: 'At Risk', order: 1, isDefault: false },
      { color: '#00AA00', name: 'On Track', order: 2, isDefault: true },
    ]
    renderGrid()
    focus(cellAt(0, 'color'))
    press('Enter')
    const host = document.querySelector<HTMLElement>(
      '[data-grid-editor-owner]',
    )!

    // Act
    const option = within(host).getByTitle('On Track')
    fireEvent.pointerDown(option)
    fireEvent.click(option)
    await flush()

    // Assert
    expect(mockPatch.mock.calls).toStrictEqual([
      [
        {
          roadmapId: 'r1',
          itemId: 'a1',
          patchOperations: [
            { op: 'replace', path: '/color', value: '#00AA00' },
          ],
        },
      ],
    ])
  })

  it('saves a typed hex color when there is no palette', async () => {
    // Arrange
    renderGrid()
    focus(cellAt(0, 'color'))
    press('Enter')
    typeInEditor('#1677FF')

    // Act
    press('Enter')
    await flush()

    // Assert
    expect(mockPatch.mock.calls).toStrictEqual([
      [
        {
          roadmapId: 'r1',
          itemId: 'a1',
          patchOperations: [
            { op: 'replace', path: '/color', value: '#1677FF' },
          ],
        },
      ],
    ])
  })

  it('keeps a color that is not hex on its cell, unsent', async () => {
    // Arrange
    renderGrid()
    focus(cellAt(0, 'color'))
    press('Enter')
    typeInEditor('blue')

    // Act
    press('Enter')
    await flush()

    // Assert
    expect(mockPatch).not.toHaveBeenCalled()
    expect(cellAt(0, 'color')).toHaveAccessibleDescription(
      'Color: Use a hex color, such as #1677FF.',
    )
  })

  it('clears a palette color with Delete', async () => {
    // Arrange
    mockRoadmapColors = [
      { color: '#FF0000', name: 'At Risk', order: 1, isDefault: false },
    ]
    const items = ITEMS.map((item) =>
      item.id === 'a1' ? { ...item, color: '#FF0000' } : item,
    )
    render(
      <RoadmapItemsGrid
        roadmapId="r1"
        roadmapItemsData={items}
        roadmapItemsIsLoading={false}
        refreshRoadmapItems={vi.fn()}
        openRoadmapItemDrawer={vi.fn()}
        canManage
      />,
    )
    focus(cellAt(0, 'color'))
    press('Enter')

    // Act
    press('Delete')
    await flush()

    // Assert
    expect(mockPatch.mock.calls).toStrictEqual([
      [
        {
          roadmapId: 'r1',
          itemId: 'a1',
          patchOperations: [{ op: 'replace', path: '/color', value: null }],
        },
      ],
    ])
  })

  describe('row menu', () => {
    it('opens the item from View Details', async () => {
      // Arrange
      const { openRoadmapItemDrawer } = renderGrid()
      await openRowMenu(0)

      // Act
      fireEvent.click(screen.getByRole('menuitem', { name: /View Details/ }))

      // Assert
      expect(openRoadmapItemDrawer).toHaveBeenCalledWith('a1')
    })

    it('adds a child row under an activity', async () => {
      // Arrange
      renderGrid()
      const rowsBefore = bodyCells('name').length
      await openRowMenu(0)

      // Act
      fireEvent.click(screen.getByRole('menuitem', { name: /Add Child Item/ }))
      await flush()

      // Assert
      expect(bodyCells('name')).toHaveLength(rowsBefore + 1)
      expect(cellAt(1, 'name')).toContainElement(
        screen.getByRole('textbox', { name: 'Name' }),
      )
    })

    it('offers every action on an activity', async () => {
      // Arrange
      renderGrid()

      // Act
      const items = await openRowMenu(0)

      // Assert
      expect(items).toStrictEqual([
        'View Details',
        'Add Child Item',
        'Edit',
        'Delete',
      ])
    })

    it('offers no Add Child Item or Edit on a milestone', async () => {
      // Arrange
      renderGrid()

      // Act
      const items = await openRowMenu(2)

      // Assert
      expect(items).toStrictEqual(['View Details', 'Delete'])
    })
  })

  it('has no Color editor on a timebox', () => {
    // Arrange
    renderGrid()
    focus(cellAt(1, 'color'))

    // Act
    press('Enter')

    // Assert
    expect(document.querySelector('tbody [data-grid-cell-editor]')).toBeNull()
  })

  describe('new rows', () => {
    it('creates the row when its Name commits, with the dates put on it first', async () => {
      // Arrange
      renderGrid()
      const draftRow = addDraft()
      focus(cellAt(draftRow, 'start'))
      press('Enter')
      typeDate('2026-05-01')
      press('Tab')
      typeDate('2026-05-31')
      press('Enter')
      await flush()
      const callsBeforeName = mockCreate.mock.calls.length

      // Act
      editText(draftRow, 'name', 'New activity')
      press('Enter')
      await flush()

      // Assert
      expect(callsBeforeName).toBe(0)
      expect(mockCreate.mock.calls).toStrictEqual([
        [
          {
            $type: 'activity',
            roadmapId: 'r1',
            parentId: undefined,
            name: 'New activity',
            start: '2026-05-01',
            end: '2026-05-31',
          },
        ],
      ])
      expect(mockPatch).not.toHaveBeenCalled()
      expect(mockMessage.success).not.toHaveBeenCalled()
    })

    it('waits for its dates when the Name comes first', async () => {
      // Arrange
      renderGrid()
      const draftRow = addDraft()
      typeInEditor('New activity')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockCreate).not.toHaveBeenCalled()
      expect(cellAt(draftRow, 'start')).toContainElement(
        document.activeElement as HTMLElement,
      )
      expect(cellAt(draftRow, 'end')).toHaveAccessibleDescription(
        'End: Required to create the item.',
      )
    })

    it('does not pick the highlighted Type with Tab', async () => {
      // Arrange
      renderGrid()
      const draftRow = addDraft()
      press('Tab') // Name, left empty, on to Type; its list opens
      const combobox = screen.getByRole('combobox', { name: 'Type' })
      fireEvent.keyDown(combobox, { key: 'ArrowDown', keyCode: 40, which: 40 })

      // Act
      fireEvent.keyDown(combobox, { key: 'Tab', keyCode: 9, which: 9 })
      await flush()

      // Assert
      expect(cellAt(draftRow, 'type')).toHaveTextContent('Activity')
      expect(cellAt(draftRow, 'type')).not.toHaveAttribute('data-edit-state')
      // Start opened and untouched: no late pick from the list reached it
      expect(cellAt(draftRow, 'start')).toContainElement(
        document.querySelector<HTMLElement>('tbody [data-grid-cell-editor]'),
      )
      expect(cellAt(draftRow, 'start')).not.toHaveAttribute('data-edit-state')
      expect(cellAt(draftRow, 'start')).toContainElement(
        document.activeElement as HTMLElement,
      )
    })

    it('edits Type only on a new row', () => {
      // Arrange
      renderGrid()
      focus(cellAt(0, 'type'))

      // Act
      press('Enter')

      // Assert
      expect(document.querySelector('tbody [data-grid-cell-editor]')).toBeNull()
    })
  })

  describe('row-action keys', () => {
    // Rows: 0 Alpha, 1 Alpha child, 2 Beta, 3 Sprint 1, 4 Launch
    const TREE = [
      {
        ...ITEMS[0],
        children: [
          {
            id: 'c1',
            name: 'Alpha child',
            type: { name: 'Activity' },
            $type: 'activity',
            parent: { id: 'a1' },
            start: '2026-03-02',
            end: '2026-03-10',
            color: null,
            children: [],
          },
        ],
      },
      {
        id: 'a2',
        name: 'Beta',
        type: { name: 'Activity' },
        $type: 'activity',
        start: '2026-04-01',
        end: '2026-04-30',
        color: null,
        children: [],
      },
      ITEMS[1],
      ITEMS[2],
    ] as unknown as RoadmapItemListDto[]
    const ROW = { a1: 0, c1: 1, a2: 2, t1: 3, m1: 4 }

    const openEditorRow = () =>
      bodyCells('name').findIndex((cell) =>
        cell.contains(document.querySelector('tbody [data-grid-cell-editor]')),
      )
    const announcements = () =>
      screen.getAllByRole('status').map((region) => region.textContent?.trim())

    /** Gives the open draft its dates, then its name, which creates it. */
    const completeDraft = async (row: number, name: string) => {
      focus(cellAt(row, 'start'))
      press('Enter')
      typeDate('2026-05-01')
      press('Tab')
      typeDate('2026-05-31')
      press('Enter')
      await flush()
      editText(row, 'name', name)
      press('Enter')
      await flush()
    }

    it('Insert creates an activity below the row, in that place', async () => {
      // Arrange
      renderGrid({ items: TREE })
      focus(cellAt(ROW.a2, 'name'))
      press('Insert')
      const draftRow = openEditorRow()

      // Act
      await completeDraft(draftRow, 'Gamma')

      // Assert
      expect(draftRow).toBe(ROW.a2 + 1)
      expect(mockCreate.mock.calls).toStrictEqual([
        [
          {
            $type: 'activity',
            roadmapId: 'r1',
            parentId: undefined,
            name: 'Gamma',
            start: '2026-05-01',
            end: '2026-05-31',
            order: 3,
          },
        ],
      ])
    })

    it('Ctrl+Enter creates an activity below a child, under its parent', async () => {
      // Arrange
      renderGrid({ items: TREE })
      focus(cellAt(ROW.c1, 'name'))
      press('Enter', { ctrlKey: true })
      const draftRow = openEditorRow()

      // Act
      await completeDraft(draftRow, 'Second child')

      // Assert
      expect(draftRow).toBe(ROW.c1 + 1)
      expect(mockCreate.mock.calls[0][0]).toStrictEqual(
        expect.objectContaining({ parentId: 'a1', order: 2 }),
      )
    })

    it('creates an activity inserted below a timebox after the last activity', async () => {
      // Arrange — the grid counts the timebox; the roadmap orders only activities
      renderGrid({ items: TREE })
      focus(cellAt(ROW.t1, 'name'))
      press('Insert')
      const draftRow = openEditorRow()

      // Act
      await completeDraft(draftRow, 'Gamma')

      // Assert
      expect(draftRow).toBe(ROW.t1 + 1)
      expect(mockCreate.mock.calls[0][0]).toStrictEqual(
        expect.objectContaining({ parentId: undefined, order: 3 }),
      )
    })

    it('Insert adds nothing under an item that cannot hold children', async () => {
      // Arrange — a timebox has no children in practice; the guard still holds
      const items = [
        {
          ...ITEMS[1],
          children: [
            {
              id: 'x1',
              name: 'Stray',
              type: { name: 'Activity' },
              $type: 'activity',
              start: '2026-03-02',
              end: '2026-03-05',
              color: null,
              children: [],
            },
          ],
        },
      ] as unknown as RoadmapItemListDto[]
      renderGrid({ items })
      const rows = bodyCells('name').length
      focus(cellAt(1, 'name'))

      // Act
      press('Insert')
      await flush()

      // Assert
      expect(bodyCells('name')).toHaveLength(rows)
      expect(announcements()).toContain('A row cannot be added here.')
    })

    it('Alt+Shift+Right indents an activity under the one above', async () => {
      // Arrange
      mockPlaceActivity.mockReturnValue(resolved())
      const { refreshRoadmapItems } = renderGrid({ items: TREE })
      focus(cellAt(ROW.a2, 'name'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockPlaceActivity.mock.calls).toStrictEqual([
        [
          {
            request: {
              roadmapId: 'r1',
              itemId: 'a2',
              parentId: 'a1',
              order: 2,
            },
          },
        ],
      ])
      expect(refreshRoadmapItems).toHaveBeenCalledTimes(1)
      expect(announcements()).toContain('Indented.')
    })

    it('Alt+Shift+Left outdents an activity to just after its parent', async () => {
      // Arrange
      mockPlaceActivity.mockReturnValue(resolved())
      renderGrid({ items: TREE })
      focus(cellAt(ROW.c1, 'name'))

      // Act
      press('ArrowLeft', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockPlaceActivity.mock.calls).toStrictEqual([
        [
          {
            request: {
              roadmapId: 'r1',
              itemId: 'c1',
              parentId: undefined,
              order: 2,
            },
          },
        ],
      ])
    })

    it('reports once that only activities move', async () => {
      // Arrange
      renderGrid({ items: TREE })
      focus(cellAt(ROW.t1, 'name'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockPlaceActivity).not.toHaveBeenCalled()
      expect(mockMessage.warning.mock.calls).toStrictEqual([
        ['Only activities can be reordered'],
      ])
    })

    it('reports a move the server refuses once, and leaves the row', async () => {
      // Arrange
      mockPlaceActivity.mockReturnValue(
        rejected({ status: 400, data: { detail: 'Not allowed.' } }),
      )
      const { refreshRoadmapItems } = renderGrid({ items: TREE })
      focus(cellAt(ROW.a2, 'name'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockMessage.warning.mock.calls).toStrictEqual([['Not allowed.']])
      expect(mockMessage.error).not.toHaveBeenCalled()
      expect(refreshRoadmapItems).not.toHaveBeenCalled()
      expect(announcements()).not.toContain('Indented.')
    })

    it('reports a refused move with a fallback when the server gives no reason', async () => {
      // Arrange
      mockPlaceActivity.mockReturnValue(rejected({ status: 500 }))
      renderGrid({ items: TREE })
      focus(cellAt(ROW.a2, 'name'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockMessage.warning.mock.calls).toStrictEqual([
        ['Failed to reorganize activity. Please try again.'],
      ])
    })

    it('Ctrl+Delete asks to delete the item', () => {
      // Arrange
      renderGrid({ items: TREE })
      focus(cellAt(ROW.t1, 'start'))

      // Act
      press('Delete', { ctrlKey: true })

      // Assert
      expect(
        screen.getByRole('dialog', { name: 'Delete roadmap item' }),
      ).toHaveTextContent('t1')
    })

    it('returns focus to the item when the delete is cancelled', async () => {
      // Arrange
      renderGrid({ items: TREE })
      focus(cellAt(ROW.a2, 'start'))
      press('Delete', { ctrlKey: true })

      // Act
      fireEvent.click(screen.getByRole('button', { name: 'Cancel delete' }))
      await flush()

      // Assert
      expect(document.activeElement).toBe(cellAt(ROW.a2, 'name'))
    })

    it('moves focus to the row taking a deleted item’s place', async () => {
      // Arrange
      const { refreshRoadmapItems, rerender } = renderGrid({ items: TREE })
      focus(cellAt(ROW.a2, 'start'))
      press('Delete', { ctrlKey: true })

      // Act
      fireEvent.click(screen.getByRole('button', { name: 'Confirm delete' }))
      rerender(TREE.filter((item) => item.id !== 'a2'))
      await flush()

      // Assert
      expect(refreshRoadmapItems).toHaveBeenCalledTimes(1)
      expect(document.activeElement).toBe(cellAt(ROW.a2, 'name'))
      expect(cellAt(ROW.a2, 'name')).toHaveTextContent('Sprint 1')
    })

    it('Ctrl+Shift+Enter opens the item drawer', () => {
      // Arrange
      const { openRoadmapItemDrawer } = renderGrid({ items: TREE })
      focus(cellAt(ROW.m1, 'start'))

      // Act
      press('Enter', { ctrlKey: true, shiftKey: true })

      // Assert
      expect(openRoadmapItemDrawer.mock.calls).toStrictEqual([['m1']])
    })

    it('gives a reader only the key that opens an item', async () => {
      // Arrange
      const { openRoadmapItemDrawer } = renderGrid({
        items: TREE,
        canManage: false,
      })
      const rows = bodyCells('name').length
      focus(cellAt(ROW.a2, 'name'))

      // Act
      press('Insert')
      press('ArrowRight', { altKey: true, shiftKey: true })
      press('Delete', { ctrlKey: true })
      press('Enter', { ctrlKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(bodyCells('name')).toHaveLength(rows)
      expect(mockPlaceActivity).not.toHaveBeenCalled()
      expect(
        screen.queryByRole('dialog', { name: 'Delete roadmap item' }),
      ).not.toBeInTheDocument()
      expect(openRoadmapItemDrawer.mock.calls).toStrictEqual([['a2']])
    })

    it('lists the row keys in the help panel', () => {
      // Arrange
      renderGrid({ items: TREE })

      // Act
      fireEvent.click(screen.getByRole('button', { name: /help/i }))

      // Assert
      expect(screen.getByText('Insert / Ctrl + Enter')).toBeInTheDocument()
      expect(screen.getByText('Ctrl + Delete')).toBeInTheDocument()
      expect(screen.getByText('Ctrl + Shift + Enter')).toBeInTheDocument()
    })
  })
})
