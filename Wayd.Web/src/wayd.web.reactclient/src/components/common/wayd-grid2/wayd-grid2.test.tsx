jest.mock('dayjs', () => jest.requireActual('dayjs'))

// Capture CSV export output. Keep generateCsv real so we assert on real content.
jest.mock('@/src/utils/csv-utils', () => {
  const actual = jest.requireActual('@/src/utils/csv-utils')
  return {
    ...actual,
    downloadCsvWithTimestamp: jest.fn(),
  }
})

import { createRef } from 'react'
import { render, screen, fireEvent, within, act } from '@testing-library/react'
import type { ColumnDef } from '@tanstack/react-table'

import WaydGrid2 from './wayd-grid2'
import type { WaydGrid2Handle, WaydGridColumnMeta } from './types'
import { downloadCsvWithTimestamp } from '@/src/utils/csv-utils'

const mockDownloadCsv = downloadCsvWithTimestamp as jest.Mock

interface Flag {
  id: number
  name: string
  isEnabled: boolean
  type: string
}

const DATA: Flag[] = [
  { id: 1, name: 'planning-poker', isEnabled: true, type: 'System' },
  { id: 2, name: 'roadmap', isEnabled: false, type: 'User' },
  { id: 3, name: 'insights', isEnabled: true, type: 'User' },
]

const columns: ColumnDef<Flag, unknown>[] = [
  { id: 'name', accessorKey: 'name', header: 'Name' },
  {
    id: 'type',
    accessorKey: 'type',
    header: 'Type',
    meta: {
      filterType: 'set',
      filterOptions: [
        { label: 'System', value: 'System' },
        { label: 'User', value: 'User' },
      ],
    } satisfies WaydGridColumnMeta,
  },
  {
    id: 'isEnabled',
    accessorKey: 'isEnabled',
    header: 'Enabled',
    meta: { columnType: 'yesNo' } satisfies WaydGridColumnMeta,
  },
]

type GridProps = Partial<Parameters<typeof WaydGrid2<Flag>>[0]> & {
  ref?: React.Ref<WaydGrid2Handle>
}

const renderGrid = ({ ref, ...props }: GridProps = {}) =>
  render(<WaydGrid2<Flag> ref={ref} data={DATA} columns={columns} {...props} />)

/** Cells for a given column id across all body rows. */
const bodyCells = (columnId: string) =>
  Array.from(
    document.querySelectorAll(`tbody td[data-column-id="${columnId}"]`),
  ) as HTMLTableCellElement[]

describe('WaydGrid2', () => {
  describe('basics', () => {
    it('renders a header cell per column', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      expect(screen.getByText('Name')).toBeInTheDocument()
      expect(screen.getByText('Type')).toBeInTheDocument()
      expect(screen.getByText('Enabled')).toBeInTheDocument()
    })

    it('renders a row per data item', () => {
      // Arrange / Act
      renderGrid()

      // Assert — name cells reflect the three rows
      const names = bodyCells('name').map((c) => c.textContent)
      expect(names).toEqual(['planning-poker', 'roadmap', 'insights'])
    })

    it('shows the empty message when there is no data', () => {
      // Arrange / Act
      renderGrid({ data: [], emptyMessage: 'Nothing here' })

      // Assert
      expect(screen.getByText('Nothing here')).toBeInTheDocument()
      expect(bodyCells('name')).toHaveLength(0)
    })

    it('shows a loading spinner when isLoading', () => {
      // Arrange / Act
      const { container } = renderGrid({ isLoading: true })

      // Assert — antd Spin renders .ant-spin
      expect(container.querySelector('.ant-spin')).toBeInTheDocument()
    })

    it('reports displayed-of-total count in the toolbar', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      expect(screen.getByText('3 of 3')).toBeInTheDocument()
    })
  })

  describe('meta.hide', () => {
    it('omits a column from the DOM when meta.hide is true', () => {
      // Arrange
      const hiddenCols: ColumnDef<Flag, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        {
          id: 'type',
          accessorKey: 'type',
          header: 'Type',
          meta: { hide: true } satisfies WaydGridColumnMeta,
        },
      ]

      // Act
      render(<WaydGrid2<Flag> data={DATA} columns={hiddenCols} />)

      // Assert — Type header absent, its cells absent, Name still present
      expect(screen.queryByText('Type')).not.toBeInTheDocument()
      expect(bodyCells('type')).toHaveLength(0)
      expect(screen.getByText('Name')).toBeInTheDocument()
    })

    it('shows the column when meta.hide is false', () => {
      // Arrange
      const cols: ColumnDef<Flag, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        {
          id: 'type',
          accessorKey: 'type',
          header: 'Type',
          meta: { hide: false } satisfies WaydGridColumnMeta,
        },
      ]

      // Act
      render(<WaydGrid2<Flag> data={DATA} columns={cols} />)

      // Assert
      expect(screen.getByText('Type')).toBeInTheDocument()
      expect(bodyCells('type').length).toBeGreaterThan(0)
    })
  })

  describe('yesNo column type', () => {
    it('renders boolean values as Yes / No', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      const enabled = bodyCells('isEnabled').map((c) => c.textContent)
      expect(enabled).toEqual(['Yes', 'No', 'Yes'])
    })

    it('exports yesNo columns as Yes / No (not true / false)', () => {
      // Arrange
      mockDownloadCsv.mockClear()
      const { container } = renderGrid()

      // Act — click the toolbar export (download icon) button
      const exportBtn = container
        .querySelector('[aria-label="download"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(exportBtn)

      // Assert — the generated CSV contains Yes/No, not true/false
      expect(mockDownloadCsv).toHaveBeenCalledTimes(1)
      const csv = mockDownloadCsv.mock.calls[0][0] as string
      expect(csv).toContain('Yes')
      expect(csv).toContain('No')
      expect(csv).not.toMatch(/\btrue\b/)
      expect(csv).not.toMatch(/\bfalse\b/)
    })
  })

  describe('CSV export', () => {
    interface Obj {
      key: number
      name: string
      status: { name: string }
      team: { name: string } | null
    }

    const OBJ_DATA: Obj[] = [
      { key: 1, name: 'Alpha', status: { name: 'Active' }, team: { name: 'Juice' } },
      { key: 2, name: 'Beta', status: { name: 'Closed' }, team: null },
    ]

    /** Renders an Obj grid and clicks the toolbar export button; returns the CSV. */
    const exportCsv = (cols: ColumnDef<Obj, unknown>[]) => {
      mockDownloadCsv.mockClear()
      const { container } = render(
        <WaydGrid2<Obj> data={OBJ_DATA} columns={cols} />,
      )
      const exportBtn = container
        .querySelector('[aria-label="download"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(exportBtn)
      expect(mockDownloadCsv).toHaveBeenCalledTimes(1)
      return mockDownloadCsv.mock.calls[0][0] as string
    }

    it('exports values from nested accessorKeys (status.name, team.name)', () => {
      // Arrange — columns whose accessorKey is a dot-path into the row
      const cols: ColumnDef<Obj, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        { id: 'status', accessorKey: 'status.name', header: 'Status' },
        { id: 'team', accessorKey: 'team.name', header: 'Team' },
      ]

      // Act
      const csv = exportCsv(cols)

      // Assert — nested values are present, not blank
      expect(csv).toContain('Active')
      expect(csv).toContain('Closed')
      expect(csv).toContain('Juice')
    })

    it('excludes hidden columns (meta.hide) from the export', () => {
      // Arrange — Status is hidden via meta.hide
      const cols: ColumnDef<Obj, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        {
          id: 'status',
          accessorKey: 'status.name',
          header: 'Status',
          meta: { hide: true } satisfies WaydGridColumnMeta,
        },
      ]

      // Act
      const csv = exportCsv(cols)

      // Assert — the hidden Status column's header and data are absent
      expect(csv).toContain('Name')
      expect(csv).not.toContain('Status')
      expect(csv).not.toContain('Active')
    })
  })

  describe('filter type inference (no explicit meta.filterType)', () => {
    const floatingRow = () =>
      document.querySelector(
        'tr[data-role="floating-filters"]',
      ) as HTMLTableRowElement

    it('infers a number filter for a numeric column (renders a number input)', () => {
      // Arrange — an id column with numeric values and no meta.filterType
      const cols: ColumnDef<Flag, unknown>[] = [
        { id: 'id', accessorKey: 'id', header: 'Id' },
        { id: 'name', accessorKey: 'name', header: 'Name' },
      ]

      // Act
      render(<WaydGrid2<Flag> data={DATA} columns={cols} />)

      // Assert — the numeric column's floating input is a spinbutton
      // (antd InputNumber), while the text column is a plain textbox.
      const row = floatingRow()
      expect(row.querySelector('input[role="spinbutton"]')).toBeInTheDocument()
      expect(row.querySelectorAll('input[type="text"]').length).toBeGreaterThan(
        0,
      )
    })

    it('infers a text filter for a string column (renders a text input)', () => {
      // Arrange — a name column with string values and no meta.filterType
      const cols: ColumnDef<Flag, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
      ]

      // Act
      render(<WaydGrid2<Flag> data={DATA} columns={cols} />)

      // Assert — no number spinner; a text input is present
      const row = floatingRow()
      expect(row.querySelector('input[role="spinbutton"]')).not.toBeInTheDocument()
      expect(row.querySelector('input[type="text"]')).toBeInTheDocument()
    })
  })

  describe('global search', () => {
    it('filters rows to those matching the search text', () => {
      // Arrange
      renderGrid()

      // Act
      fireEvent.change(screen.getByPlaceholderText('Search'), {
        target: { value: 'road' },
      })

      // Assert
      expect(bodyCells('name').map((c) => c.textContent)).toEqual(['roadmap'])
      expect(screen.getByText('1 of 3')).toBeInTheDocument()
    })
  })

  describe('floating filter row', () => {
    it('renders by default', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      expect(
        document.querySelector('tr[data-role="floating-filters"]'),
      ).toBeInTheDocument()
    })

    it('is hidden when includeFloatingFilters is false', () => {
      // Arrange / Act
      renderGrid({ includeFloatingFilters: false })

      // Assert
      expect(
        document.querySelector('tr[data-role="floating-filters"]'),
      ).not.toBeInTheDocument()
    })

    it('is hidden when includeColumnFilters is false', () => {
      // Arrange / Act
      renderGrid({ includeColumnFilters: false })

      // Assert
      expect(
        document.querySelector('tr[data-role="floating-filters"]'),
      ).not.toBeInTheDocument()
    })
  })

  describe('set filter (Excel-style panel)', () => {
    const floatingRow = () =>
      document.querySelector(
        'tr[data-role="floating-filters"]',
      ) as HTMLTableRowElement

    it('renders a filter trigger for each set column (box + icon, no inline select)', () => {
      // Arrange / Act
      renderGrid()

      // Assert — set columns (Type, Enabled) each expose a "Filter column"
      // trigger, and there is no inline antd Select in the floating row.
      const triggers = within(floatingRow()).getAllByRole('button', {
        name: /Filter column/,
      })
      expect(triggers.length).toBeGreaterThanOrEqual(2)
      expect(floatingRow().querySelector('.ant-select')).not.toBeInTheDocument()
    })

    // The set panel (Select All + value checkboxes) opens in an antd Popover
    // portal, which is unreliable to drive in jsdom — that interaction is
    // verified in the browser (playwright). The SetFilterPanel's own behavior is
    // unit-tested directly in set-filter-panel.test.tsx.
  })

  describe('set filter behavior (through the grid)', () => {
    it('filters a set column to the chosen value via the descriptor engine', () => {
      // Arrange
      const ref = createRef<WaydGrid2Handle>()
      renderGrid({ ref })

      // Act
      act(() => {
        ref.current!.table
          .getColumn('type')!
          .setFilterValue({ type: 'set', values: ['System'] })
      })

      // Assert — only the System row remains
      expect(bodyCells('name').map((c) => c.textContent)).toEqual([
        'planning-poker',
      ])
    })

    it('filters a yesNo column on the Yes/No display value (not true/false)', () => {
      // Arrange
      const ref = createRef<WaydGrid2Handle>()
      renderGrid({ ref })

      // Act — filter Enabled to "Yes"
      act(() => {
        ref.current!.table
          .getColumn('isEnabled')!
          .setFilterValue({ type: 'set', values: ['Yes'] })
      })

      // Assert — only the two enabled rows remain
      expect(bodyCells('name').map((c) => c.textContent)).toEqual([
        'planning-poker',
        'insights',
      ])
    })
  })

  describe('sorting', () => {
    it('sorts a column ascending on header click', () => {
      // Arrange
      renderGrid()

      // Act
      fireEvent.click(screen.getByText('Name'))

      // Assert — alphabetical
      expect(bodyCells('name').map((c) => c.textContent)).toEqual([
        'insights',
        'planning-poker',
        'roadmap',
      ])
    })
  })

  describe('full-width filler structure', () => {
    it('adds a trailing filler col plus filler header/body cells', () => {
      // Arrange / Act
      const { container } = renderGrid()

      // Assert — one <col> more than data columns (the filler)
      const cols = container.querySelectorAll('colgroup col')
      expect(cols).toHaveLength(columns.length + 1)

      // Header row ends in an aria-hidden filler th
      const headerRow = container.querySelector('thead tr') as HTMLTableRowElement
      const headerCells = headerRow.querySelectorAll('th')
      expect(headerCells[headerCells.length - 1]).toHaveAttribute(
        'aria-hidden',
        'true',
      )

      // Each body row ends in an aria-hidden filler td
      const firstBodyRow = container.querySelector('tbody tr') as HTMLTableRowElement
      const bodyRowCells = firstBodyRow.querySelectorAll('td')
      expect(bodyRowCells[bodyRowCells.length - 1]).toHaveAttribute(
        'aria-hidden',
        'true',
      )
    })

    it('adds a filler cell to the floating filter row', () => {
      // Arrange / Act
      const { container } = renderGrid()

      // Assert
      const floatingRow = container.querySelector(
        'tr[data-role="floating-filters"]',
      ) as HTMLTableRowElement
      const cells = floatingRow.querySelectorAll('th')
      expect(cells[cells.length - 1]).toHaveAttribute('aria-hidden', 'true')
    })
  })

  describe('toolbar behavior', () => {
    it('calls onRefresh when the refresh button is clicked', () => {
      // Arrange
      const onRefresh = jest.fn()
      const { container } = renderGrid({ onRefresh })

      // Act — the refresh button carries a reload icon (aria-label="reload")
      const reloadBtn = container
        .querySelector('[aria-label="reload"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(reloadBtn)

      // Assert
      expect(onRefresh).toHaveBeenCalledTimes(1)
    })

    it('clears active filters and sorting via Clear', () => {
      // Arrange
      const { container } = renderGrid()
      fireEvent.change(screen.getByPlaceholderText('Search'), {
        target: { value: 'road' },
      })
      expect(screen.getByText('1 of 3')).toBeInTheDocument()

      // Act — the clear button carries a clear icon (aria-label="clear")
      const clearBtn = container
        .querySelector('[aria-label="clear"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(clearBtn)

      // Assert — all rows shown again
      expect(screen.getByText('3 of 3')).toBeInTheDocument()
    })

    it('renders the rightSlot content', () => {
      // Arrange / Act
      renderGrid({ rightSlot: <button>Custom action</button> })

      // Assert
      expect(
        screen.getByRole('button', { name: 'Custom action' }),
      ).toBeInTheDocument()
    })

    it('hides the global search when includeGlobalSearch is false', () => {
      // Arrange / Act
      renderGrid({ includeGlobalSearch: false })

      // Assert
      expect(screen.queryByPlaceholderText('Search')).not.toBeInTheDocument()
    })
  })

  describe('tree mode (getSubRows)', () => {
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

    const TREE: Node[] = [
      node('1', 'Plan', [node('1.1', 'Kickoff')]),
      node('2', 'Execute', [
        node('2.1', 'Build widgets', [node('2.1.1', 'Widget spec')]),
        node('2.2', 'Review'),
      ]),
    ]

    const treeColumns: ColumnDef<Node, unknown>[] = [
      { id: 'name', accessorKey: 'name', header: 'Name' },
    ]

    const renderTree = () =>
      render(
        <WaydGrid2<Node>
          data={TREE}
          columns={treeColumns}
          getSubRows={(row) => row.children}
        />,
      )

    it('renders all descendants expanded by default', () => {
      // Arrange / Act
      renderTree()

      // Assert — every node at every depth is a visible row
      for (const name of [
        'Plan',
        'Kickoff',
        'Execute',
        'Build widgets',
        'Widget spec',
        'Review',
      ]) {
        expect(screen.getByText(name)).toBeInTheDocument()
      }
      expect(screen.getByText('6 of 6')).toBeInTheDocument()
    })

    it('keeps the ancestor chain visible when a filter matches only a deep child (filterFromLeafRows)', () => {
      // Arrange
      renderTree()

      // Act — global search matches only the depth-2 leaf
      fireEvent.change(screen.getByPlaceholderText('Search'), {
        target: { value: 'Widget spec' },
      })

      // Assert — the leaf plus its ancestor chain stay visible…
      expect(screen.getByText('Widget spec')).toBeInTheDocument()
      expect(screen.getByText('Build widgets')).toBeInTheDocument()
      expect(screen.getByText('Execute')).toBeInTheDocument()
      // …while unrelated branches are filtered out
      expect(screen.queryByText('Plan')).not.toBeInTheDocument()
      expect(screen.queryByText('Review')).not.toBeInTheDocument()
    })

    it('tags tree cells with data-cell-id for the editing machinery', () => {
      // Arrange / Act
      renderTree()

      // Assert — tree rows carry per-cell ids (`{nodeId}-{columnId}`)
      expect(
        document.querySelector('[data-cell-id="2.1.1-name"]'),
      ).toBeInTheDocument()
    })
  })
})
