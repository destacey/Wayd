// Capture CSV export output. Keep generateCsv real so we assert on real content.
jest.mock('@/src/utils/csv-utils', () => {
  const actual = jest.requireActual('@/src/utils/csv-utils')
  return {
    ...actual,
    downloadCsvWithTimestamp: jest.fn(),
  }
})

import {
  createTable,
  getCoreRowModel,
  type ColumnDef,
  type TableState,
} from '@tanstack/react-table'
import { downloadCsvWithTimestamp } from '@/src/utils/csv-utils'
import { exportGridToCsv } from './grid-export'

const mockDownloadCsv = downloadCsvWithTimestamp as jest.Mock

type Item = {
  name: string
  status: { name: string }
  points: number | null
}

const data: Item[] = [
  { name: 'Widget', status: { name: 'Active' }, points: 3 },
  { name: 'Gadget', status: { name: 'Closed' }, points: null },
]

/** Builds a headless table and runs the export; returns the generated CSV. */
const exportCsv = (
  columns: ColumnDef<Item, any>[],
  state: Partial<TableState> = {},
): string => {
  const table = createTable<Item>({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    state: state as TableState,
    onStateChange: () => {},
    renderFallbackValue: null,
  })
  exportGridToCsv(table, 'test-export')
  const csv = mockDownloadCsv.mock.calls.at(-1)?.[0] as string
  return csv
}

describe('grid-export', () => {
  beforeEach(() => {
    mockDownloadCsv.mockClear()
  })

  describe('exportGridToCsv', () => {
    it('exports values from nested accessorKeys', () => {
      // Arrange
      const columns: ColumnDef<Item, any>[] = [
        { accessorKey: 'name', header: 'Name' },
        { accessorKey: 'status.name', header: 'Status' },
      ]

      // Act
      const csv = exportCsv(columns)

      // Assert
      expect(csv).toBe('Name,Status\nWidget,Active\nGadget,Closed')
    })

    it('excludes hidden columns from the export', () => {
      // Arrange
      const columns: ColumnDef<Item, any>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        { id: 'status', accessorKey: 'status.name', header: 'Status' },
      ]

      // Act
      const csv = exportCsv(columns, {
        columnVisibility: { status: false },
      })

      // Assert
      expect(csv).toContain('Name')
      expect(csv).not.toContain('Status')
      expect(csv).not.toContain('Active')
    })

    it('excludes columns with meta.enableExport false', () => {
      // Arrange
      const columns: ColumnDef<Item, any>[] = [
        { accessorKey: 'name', header: 'Name' },
        {
          accessorKey: 'points',
          header: 'Points',
          meta: { enableExport: false },
        },
      ]

      // Act
      const csv = exportCsv(columns)

      // Assert
      expect(csv).not.toContain('Points')
    })

    it('excludes display columns without an accessor', () => {
      // Arrange
      const columns: ColumnDef<Item, any>[] = [
        { accessorKey: 'name', header: 'Name' },
        { id: 'actions', header: 'Actions' },
      ]

      // Act
      const csv = exportCsv(columns)

      // Assert
      expect(csv).toBe('Name\nWidget\nGadget')
    })

    it('prefers meta.exportHeader, then a string header, then the column id', () => {
      // Arrange
      const columns: ColumnDef<Item, any>[] = [
        {
          accessorKey: 'name',
          header: 'Name',
          meta: { exportHeader: 'Item Name' },
        },
        { accessorKey: 'points', header: 'Points' },
        { id: 'statusName', accessorKey: 'status.name', header: () => null },
      ]

      // Act
      const csv = exportCsv(columns)

      // Assert
      expect(csv.split('\n')[0]).toBe('Item Name,Points,statusName')
    })

    it('applies meta.exportFormatter to each value', () => {
      // Arrange
      const columns: ColumnDef<Item, any>[] = [
        {
          accessorKey: 'points',
          header: 'Points',
          meta: {
            exportFormatter: (value) => (value == null ? 'n/a' : `${value}pt`),
          },
        },
      ]

      // Act
      const csv = exportCsv(columns)

      // Assert
      expect(csv).toBe('Points\n3pt\nn/a')
    })

    it('exports null/undefined values as empty cells', () => {
      // Arrange
      const columns: ColumnDef<Item, any>[] = [
        { accessorKey: 'name', header: 'Name' },
        { accessorKey: 'points', header: 'Points' },
      ]

      // Act
      const csv = exportCsv(columns)

      // Assert
      expect(csv).toBe('Name,Points\nWidget,3\nGadget,')
    })
  })
})
