jest.mock('dayjs', () => jest.requireActual('dayjs'))

const mockMessage = {
  success: jest.fn(),
  error: jest.fn(),
  warning: jest.fn(),
  info: jest.fn(),
}
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

const mockPatch = jest.fn()
const mockCreate = jest.fn()
let mockRoadmapColors: unknown[] = []
jest.mock('@/src/store/features/planning/roadmaps-api', () => ({
  useGetRoadmapQuery: () => ({
    data: { colors: mockRoadmapColors, start: '2026-01-01', end: '2026-12-31' },
  }),
  usePatchRoadmapItemMutation: () => [mockPatch],
  useCreateRoadmapItemMutation: () => [mockCreate],
  useUpdateRoadmapActivityPlacementMutation: () => [jest.fn()],
  useUpdateRoadmapItemDatesMutation: () => [jest.fn()],
}))

// The chart pane is not under test; keep it hidden.
jest.mock('@/src/components/common/timeline', () => ({
  ...jest.requireActual('@/src/components/common/timeline'),
  useGanttVisibility: () => ({ visible: false, toggle: jest.fn() }),
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

const renderGrid = () => {
  const openRoadmapItemDrawer = jest.fn()
  render(
    <RoadmapItemsGrid
      roadmapId="r1"
      roadmapItemsData={ITEMS}
      roadmapItemsIsLoading={false}
      refreshRoadmapItems={jest.fn()}
      openRoadmapItemDrawer={openRoadmapItemDrawer}
      canManage
    />,
  )
  return { openRoadmapItemDrawer }
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
    jest.clearAllMocks()
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
        refreshRoadmapItems={jest.fn()}
        openRoadmapItemDrawer={jest.fn()}
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
})
