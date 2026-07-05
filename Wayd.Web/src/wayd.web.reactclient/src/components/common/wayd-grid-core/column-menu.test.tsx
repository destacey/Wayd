import { fireEvent, render, screen } from '@testing-library/react'
import {
  createTable,
  getCoreRowModel,
  type ColumnDef,
  type TableState,
  type VisibilityState,
} from '@tanstack/react-table'

import {
  COLUMN_MENU_KEYS,
  ColumnChooserModal,
  buildColumnMenuItems,
  getColumnChooserOptions,
  type ColumnMenuItemsInput,
} from './column-menu'

type Item = { name: string; team: string; secret: string; flagged: string }

const data: Item[] = [
  { name: 'Widget', team: 'Falcons', secret: 'x', flagged: 'y' },
]

const buildChooserTable = (
  columns: ColumnDef<Item, any>[],
  columnVisibility: VisibilityState = {},
) => {
  const state: Partial<TableState> = {
    columnVisibility,
    columnPinning: { left: [], right: [] },
    columnSizing: {},
  }
  return createTable<Item>({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    state: state as TableState,
    onStateChange: () => {},
    renderFallbackValue: null,
  })
}

/** Flattened keys of a built items array (submenu children inlined). */
const itemKeys = (items: ReturnType<typeof buildColumnMenuItems>): string[] =>
  (items ?? []).flatMap((item) => {
    if (!item || !('key' in item) || item.key == null) return []
    const children =
      'children' in item && Array.isArray(item.children)
        ? itemKeys(item.children as ReturnType<typeof buildColumnMenuItems>)
        : []
    return [String(item.key), ...children]
  })

const baseInput: ColumnMenuItemsInput = {
  canSort: true,
  sortState: false,
  canPin: true,
  pinnedState: false,
  canResize: true,
  hasHidableColumns: true,
}

describe('column-menu', () => {
  describe('getColumnChooserOptions', () => {
    it('lists hidable leaf columns with their visibility', () => {
      // Arrange
      const table = buildChooserTable(
        [
          { id: 'name', accessorKey: 'name', header: 'Name' },
          { id: 'team', accessorKey: 'team', header: 'Team' },
        ],
        { team: false },
      )

      // Act
      const options = getColumnChooserOptions(table)

      // Assert
      expect(options).toEqual([
        { id: 'name', label: 'Name', visible: true },
        { id: 'team', label: 'Team', visible: false },
      ])
    })

    it('excludes consumer-hidden (meta.hide), unhidable, and unlabeled columns', () => {
      // Arrange — meta.hide=true is consumer-controlled; enableHiding=false is
      // locked; an empty header with no exportHeader (actions-style) has no
      // displayable label
      const table = buildChooserTable([
        { id: 'name', accessorKey: 'name', header: 'Name' },
        {
          id: 'secret',
          accessorKey: 'secret',
          header: 'Secret',
          meta: { hide: true },
        },
        {
          id: 'flagged',
          accessorKey: 'flagged',
          header: 'Flagged',
          enableHiding: false,
        },
        { id: 'actions', header: '' },
      ])

      // Act
      const options = getColumnChooserOptions(table)

      // Assert
      expect(options.map((o) => o.id)).toEqual(['name'])
    })

    it('falls back to meta.exportHeader when the header is not a string', () => {
      // Arrange
      const table = buildChooserTable([
        {
          id: 'name',
          accessorKey: 'name',
          header: () => null,
          meta: { exportHeader: 'Name (export)' },
        },
      ])

      // Act
      const options = getColumnChooserOptions(table)

      // Assert
      expect(options).toEqual([
        { id: 'name', label: 'Name (export)', visible: true },
      ])
    })

    it('resolves visibility through grouped column defs', () => {
      // Arrange
      const table = buildChooserTable([
        {
          id: 'group',
          header: 'Group',
          columns: [
            { id: 'name', accessorKey: 'name', header: 'Name' },
            { id: 'team', accessorKey: 'team', header: 'Team' },
          ],
        },
      ])

      // Act
      const options = getColumnChooserOptions(table)

      // Assert — leaves only, no band entry
      expect(options.map((o) => o.id)).toEqual(['name', 'team'])
    })
  })

  describe('buildColumnMenuItems', () => {
    it('builds the full menu for a sortable, pinnable, resizable column', () => {
      // Arrange / Act
      const keys = itemKeys(buildColumnMenuItems(baseInput))

      // Assert
      expect(keys).toEqual([
        COLUMN_MENU_KEYS.sortAsc,
        COLUMN_MENU_KEYS.sortDesc,
        COLUMN_MENU_KEYS.pin,
        COLUMN_MENU_KEYS.pinLeft,
        COLUMN_MENU_KEYS.pinRight,
        COLUMN_MENU_KEYS.pinNone,
        COLUMN_MENU_KEYS.autosizeThis,
        COLUMN_MENU_KEYS.autosizeAll,
        COLUMN_MENU_KEYS.chooseColumns,
        COLUMN_MENU_KEYS.reset,
      ])
    })

    it('adds Clear Sort only while the column is sorted', () => {
      // Arrange / Act
      const unsorted = itemKeys(buildColumnMenuItems(baseInput))
      const sorted = itemKeys(
        buildColumnMenuItems({ ...baseInput, sortState: 'asc' }),
      )

      // Assert
      expect(unsorted).not.toContain(COLUMN_MENU_KEYS.sortClear)
      expect(sorted).toContain(COLUMN_MENU_KEYS.sortClear)
    })

    it('omits sections a column does not support', () => {
      // Arrange / Act — e.g. the actions column: no sort, no pin, no resize
      const keys = itemKeys(
        buildColumnMenuItems({
          ...baseInput,
          canSort: false,
          canPin: false,
          canResize: false,
          hasHidableColumns: false,
        }),
      )

      // Assert — Autosize All and Reset remain grid-level actions
      expect(keys).toEqual([
        COLUMN_MENU_KEYS.autosizeAll,
        COLUMN_MENU_KEYS.reset,
      ])
    })

    it('marks the active pin option with a check icon', () => {
      // Arrange / Act
      const items = buildColumnMenuItems({
        ...baseInput,
        pinnedState: 'left',
      })
      const pin = (items ?? []).find(
        (item) => item && 'key' in item && item.key === COLUMN_MENU_KEYS.pin,
      ) as { children: { key: string; icon?: unknown }[] }

      // Assert — only Pin Left carries the check
      const iconsByKey = Object.fromEntries(
        pin.children.map((child) => [child.key, child.icon !== undefined]),
      )
      expect(iconsByKey).toEqual({
        [COLUMN_MENU_KEYS.pinLeft]: true,
        [COLUMN_MENU_KEYS.pinRight]: false,
        [COLUMN_MENU_KEYS.pinNone]: false,
      })
    })

  })

  describe('ColumnChooserModal', () => {
    const chooserColumns: ColumnDef<Item, any>[] = [
      { id: 'name', accessorKey: 'name', header: 'Name' },
      { id: 'team', accessorKey: 'team', header: 'Team' },
      { id: 'flagged', accessorKey: 'flagged', header: 'Flagged' },
    ]

    const renderModal = (
      columnVisibility: VisibilityState = {},
      onToggleColumn: jest.Mock = jest.fn(),
      onClose: jest.Mock = jest.fn(),
    ) => {
      const table = buildChooserTable(chooserColumns, columnVisibility)
      render(
        <ColumnChooserModal
          table={table}
          open
          onClose={onClose}
          onToggleColumn={onToggleColumn}
        />,
      )
      return { onToggleColumn, onClose }
    }

    /** The checkbox input inside the row labeled `label`. */
    const checkboxFor = (label: string) =>
      screen
        .getByText(label)
        .closest('label')!
        .querySelector('input[type="checkbox"]') as HTMLInputElement

    it('lists every hidable column with its current visibility', () => {
      // Arrange / Act
      renderModal({ team: false })

      // Assert
      expect(checkboxFor('Name').checked).toBe(true)
      expect(checkboxFor('Team').checked).toBe(false)
      expect(checkboxFor('Flagged').checked).toBe(true)
    })

    it('toggles apply per column without closing the modal', () => {
      // Arrange
      const { onToggleColumn, onClose } = renderModal({ team: false })

      // Act — hide one column, show another (multiple changes in one visit)
      fireEvent.click(checkboxFor('Flagged'))
      fireEvent.click(checkboxFor('Team'))

      // Assert
      expect(onToggleColumn).toHaveBeenNthCalledWith(1, 'flagged', false)
      expect(onToggleColumn).toHaveBeenNthCalledWith(2, 'team', true)
      expect(onClose).not.toHaveBeenCalled()
    })

    it('locks the last visible column on', () => {
      // Arrange / Act — only Name still visible
      const { onToggleColumn } = renderModal({ team: false, flagged: false })

      // Assert — its checkbox is disabled and clicking does nothing
      expect(checkboxFor('Name').disabled).toBe(true)
      fireEvent.click(checkboxFor('Name'))
      expect(onToggleColumn).not.toHaveBeenCalled()
    })

    it('filters the list by search', () => {
      // Arrange
      renderModal()

      // Act
      fireEvent.change(screen.getByPlaceholderText('Search columns...'), {
        target: { value: 'tea' },
      })

      // Assert
      expect(screen.getByText('Team')).toBeInTheDocument()
      expect(screen.queryByText('Flagged')).not.toBeInTheDocument()
    })

    it('closes via the Done button', () => {
      // Arrange
      const { onClose } = renderModal()

      // Act
      fireEvent.click(screen.getByRole('button', { name: 'Done' }))

      // Assert
      expect(onClose).toHaveBeenCalledTimes(1)
    })
  })
})
