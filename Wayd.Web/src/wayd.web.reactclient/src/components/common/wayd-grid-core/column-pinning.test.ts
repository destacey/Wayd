import {
  createTable,
  getCoreRowModel,
  type ColumnDef,
  type ColumnPinningState,
  type TableState,
} from '@tanstack/react-table'

import {
  getPinnedBandOffsets,
  getPinnedOffsets,
  pinnedCellClassNames,
  pinnedCellStyle,
  type PinnedCellClasses,
} from './column-pinning'

type Item = { a: string; b: string; c: string; d: string }

const data: Item[] = [{ a: '1', b: '2', c: '3', d: '4' }]

const leafColumns: ColumnDef<Item, any>[] = [
  { id: 'a', accessorKey: 'a', header: 'A', size: 100 },
  { id: 'b', accessorKey: 'b', header: 'B', size: 200 },
  { id: 'c', accessorKey: 'c', header: 'C', size: 50 },
  { id: 'd', accessorKey: 'd', header: 'D', size: 80 },
]

/** Headless table with the given pinning state (sizes from the defs). */
const buildTable = (
  columnPinning: ColumnPinningState,
  columns: ColumnDef<Item, any>[] = leafColumns,
) =>
  createTable<Item>({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    state: {
      columnPinning,
      columnSizing: {},
      columnVisibility: {},
    } as TableState,
    onStateChange: () => {},
    renderFallbackValue: null,
  })

const classes: PinnedCellClasses = {
  pinned: 'pinned',
  pinnedLeftEdge: 'left-edge',
  pinnedRightEdge: 'right-edge',
}

describe('column-pinning', () => {
  describe('getPinnedOffsets', () => {
    it('returns undefined for an unpinned column', () => {
      // Arrange
      const table = buildTable({ left: [], right: [] })

      // Act
      const offsets = getPinnedOffsets(table.getColumn('a')!)

      // Assert
      expect(offsets).toBeUndefined()
    })

    it('offsets left-pinned columns by the preceding pinned widths', () => {
      // Arrange — b (200) then c (50) pinned left
      const table = buildTable({ left: ['b', 'c'], right: [] })

      // Act
      const first = getPinnedOffsets(table.getColumn('b')!)
      const second = getPinnedOffsets(table.getColumn('c')!)

      // Assert
      expect(first).toEqual({ side: 'left', offset: 0, isEdge: false })
      expect(second).toEqual({ side: 'left', offset: 200, isEdge: true })
    })

    it('offsets right-pinned columns by the following pinned widths', () => {
      // Arrange — a (100) then d (80) pinned right; d renders last
      const table = buildTable({ left: [], right: ['a', 'd'] })

      // Act
      const first = getPinnedOffsets(table.getColumn('a')!)
      const last = getPinnedOffsets(table.getColumn('d')!)

      // Assert — a is the FIRST right-pinned column (the edge), offset by d
      expect(first).toEqual({ side: 'right', offset: 80, isEdge: true })
      expect(last).toEqual({ side: 'right', offset: 0, isEdge: false })
    })

    it('orders left → center → right sections for the shared colgroup', () => {
      // Arrange — the grid builds its colgroup from the three pin-section
      // methods because getHeaderGroups/getVisibleCells render that order
      // while plain getVisibleLeafColumns stays in def order
      const table = buildTable({ left: ['c'], right: ['a'] })

      // Act
      const order = [
        ...table.getLeftVisibleLeafColumns(),
        ...table.getCenterVisibleLeafColumns(),
        ...table.getRightVisibleLeafColumns(),
      ].map((col) => col.id)

      // Assert
      expect(order).toEqual(['c', 'b', 'd', 'a'])
      // Def order — reordering here would silently fix the colgroup, so pin
      // sections must stay explicit.
      expect(table.getVisibleLeafColumns().map((col) => col.id)).toEqual([
        'a',
        'b',
        'c',
        'd',
      ])
    })
  })

  describe('getPinnedBandOffsets', () => {
    const groupedColumns: ColumnDef<Item, any>[] = [
      {
        id: 'group',
        header: 'Group',
        columns: [
          { id: 'a', accessorKey: 'a', header: 'A', size: 100 },
          { id: 'b', accessorKey: 'b', header: 'B', size: 200 },
        ],
      },
      { id: 'c', accessorKey: 'c', header: 'C', size: 50 },
      { id: 'd', accessorKey: 'd', header: 'D', size: 80 },
    ]

    /** The band-row headers (all rows above the leaf row). */
    const bandHeaders = (columnPinning: ColumnPinningState) => {
      const table = buildTable(columnPinning, groupedColumns)
      const groups = table.getHeaderGroups()
      return groups.slice(0, -1).flatMap((group) => group.headers)
    }

    it('returns undefined for a band over unpinned leaves', () => {
      // Arrange
      const headers = bandHeaders({ left: [], right: [] })
      const band = headers.find((h) => h.column.id === 'group')!

      // Act / Assert
      expect(getPinnedBandOffsets(band)).toBeUndefined()
    })

    it('sticks a band over left-pinned leaves at the first leaf offset', () => {
      // Arrange — whole group pinned left
      const headers = bandHeaders({ left: ['a', 'b'], right: [] })
      const band = headers.find(
        (h) => h.column.id === 'group' && !h.isPlaceholder,
      )!

      // Act
      const offsets = getPinnedBandOffsets(band)

      // Assert — spans both leaves, contains the pinned edge (b)
      expect(offsets).toEqual({ side: 'left', offset: 0, isEdge: true })
    })

    it('splits a band whose leaves are torn apart by pinning', () => {
      // Arrange — pinning a and c reorders leaves to [a, c, b, d]: the group
      // band renders once over a (pinned) and once over b (center)
      const headers = bandHeaders({ left: ['a', 'c'], right: [] })
      const groupBands = headers.filter((h) => h.column.id === 'group')

      // Act
      const offsets = groupBands.map((h) => getPinnedBandOffsets(h))

      // Assert — one pinned section cell (a is not the pinned edge — c is),
      // one unpinned
      expect(groupBands).toHaveLength(2)
      expect(offsets).toContainEqual({
        side: 'left',
        offset: 0,
        isEdge: false,
      })
      expect(offsets).toContain(undefined)
    })

    it('renders a band unpinned while its leaves stay adjacent but mixed', () => {
      // Arrange — pinning only b reorders leaves to [b, a, ...] but the pair
      // stays adjacent, so TanStack emits ONE band over a mixed
      // pinned/unpinned run; the band scrolls (documented limitation) while
      // leaf b still sticks
      const headers = bandHeaders({ left: ['b'], right: [] })
      const groupBands = headers.filter((h) => h.column.id === 'group')

      // Act / Assert
      expect(groupBands).toHaveLength(1)
      expect(getPinnedBandOffsets(groupBands[0])).toBeUndefined()
    })
  })

  describe('pinnedCellStyle', () => {
    it('maps the side to the matching inset', () => {
      // Arrange / Act / Assert
      expect(
        pinnedCellStyle({ side: 'left', offset: 120, isEdge: false }),
      ).toEqual({ left: 120 })
      expect(
        pinnedCellStyle({ side: 'right', offset: 40, isEdge: true }),
      ).toEqual({ right: 40 })
      expect(pinnedCellStyle(undefined)).toBeUndefined()
    })
  })

  describe('pinnedCellClassNames', () => {
    it('adds the edge class only on the section edge', () => {
      // Arrange / Act / Assert
      expect(
        pinnedCellClassNames({ side: 'left', offset: 0, isEdge: false }, classes),
      ).toBe('pinned')
      expect(
        pinnedCellClassNames({ side: 'left', offset: 0, isEdge: true }, classes),
      ).toBe('pinned left-edge')
      expect(
        pinnedCellClassNames({ side: 'right', offset: 0, isEdge: true }, classes),
      ).toBe('pinned right-edge')
      expect(pinnedCellClassNames(undefined, classes)).toBe('')
    })
  })
})
