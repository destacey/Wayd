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

import WaydGrid from './wayd-grid'
import { createCsvColumn } from '../wayd-grid-core/csv-column'
import { SET_FILTER_BLANK } from '../wayd-grid-core/filters'
import { createActionsColumn } from '../wayd-grid-core/actions-column'
import { gridStateStorageKey } from '../wayd-grid-core/use-grid-persistence'
import type { WaydGridHandle, WaydGridColumnMeta } from './types'
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

type GridProps = Partial<Parameters<typeof WaydGrid<Flag>>[0]> & {
  ref?: React.Ref<WaydGridHandle>
}

const renderGrid = ({ ref, ...props }: GridProps = {}) =>
  render(<WaydGrid<Flag> ref={ref} data={DATA} columns={columns} {...props} />)

/** Cells for a given column id across all body rows. */
const bodyCells = (columnId: string) =>
  Array.from(
    document.querySelectorAll(`tbody td[data-column-id="${columnId}"]`),
  ) as HTMLTableCellElement[]

describe('WaydGrid', () => {
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

    it('treats undefined data (query still loading) as an empty grid', () => {
      // Arrange / Act — consumers pass query-hook data straight through, which
      // is undefined until the fetch resolves (old ag-grid rowData tolerance)
      renderGrid({ data: undefined, isLoading: true })

      // Assert — loading spinner, no crash, zero row count
      expect(document.querySelector('.ant-spin')).toBeInTheDocument()
      expect(screen.getByText('0 of 0')).toBeInTheDocument()
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
      render(<WaydGrid<Flag> data={DATA} columns={hiddenCols} />)

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
      render(<WaydGrid<Flag> data={DATA} columns={cols} />)

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
      {
        key: 1,
        name: 'Alpha',
        status: { name: 'Active' },
        team: { name: 'Juice' },
      },
      { key: 2, name: 'Beta', status: { name: 'Closed' }, team: null },
    ]

    /** Renders an Obj grid and clicks the toolbar export button; returns the CSV. */
    const exportCsv = (cols: ColumnDef<Obj, unknown>[]) => {
      mockDownloadCsv.mockClear()
      const { container } = render(
        <WaydGrid<Obj> data={OBJ_DATA} columns={cols} />,
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
      render(<WaydGrid<Flag> data={DATA} columns={cols} />)

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
      render(<WaydGrid<Flag> data={DATA} columns={cols} />)

      // Assert — no number spinner; a text input is present
      const row = floatingRow()
      expect(
        row.querySelector('input[role="spinbutton"]'),
      ).not.toBeInTheDocument()
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
      const ref = createRef<WaydGridHandle>()
      renderGrid({ ref })

      // Act
      act(() => {
        ref
          .current!.table.getColumn('type')!
          .setFilterValue({ type: 'set', values: ['System'] })
      })

      // Assert — only the System row remains
      expect(bodyCells('name').map((c) => c.textContent)).toEqual([
        'planning-poker',
      ])
    })

    it('filters blank cells via the (Blanks) sentinel', () => {
      // Arrange — a nullable Team column with one blank row
      interface Item {
        id: number
        name: string
        team: string | null
      }
      const data: Item[] = [
        { id: 1, name: 'alpha', team: 'Juice' },
        { id: 2, name: 'beta', team: null },
      ]
      const cols: ColumnDef<Item, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        {
          id: 'team',
          accessorKey: 'team',
          header: 'Team',
          meta: { filterType: 'set' },
        },
      ]
      const ref = createRef<WaydGridHandle>()
      render(<WaydGrid<Item> ref={ref} data={data} columns={cols} />)

      // Act / Assert — a value-only selection hides the blank row
      act(() => {
        ref
          .current!.table.getColumn('team')!
          .setFilterValue({ type: 'set', values: ['Juice'] })
      })
      expect(bodyCells('name').map((c) => c.textContent)).toEqual(['alpha'])

      // Act / Assert — selecting only (Blanks) shows just the blank row
      act(() => {
        ref
          .current!.table.getColumn('team')!
          .setFilterValue({ type: 'set', values: [SET_FILTER_BLANK] })
      })
      expect(bodyCells('name').map((c) => c.textContent)).toEqual(['beta'])
    })

    it('filters a multi-value (createCsvColumn) column on an individual token', () => {
      // Arrange — a Tags column whose rows each hold several tags
      interface Item {
        id: number
        name: string
        tags: string[]
      }
      const data: Item[] = [
        { id: 1, name: 'alpha', tags: ['red', 'blue'] },
        { id: 2, name: 'beta', tags: ['green'] },
        { id: 3, name: 'gamma', tags: ['blue', 'green'] },
      ]
      const cols: ColumnDef<Item, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        createCsvColumn<Item>({
          id: 'tags',
          header: 'Tags',
          getValues: (row) => row.tags,
        }) as ColumnDef<Item, unknown>,
      ]
      const ref = createRef<WaydGridHandle>()
      render(<WaydGrid<Item> ref={ref} data={data} columns={cols} />)

      // Act — filter to the 'blue' token
      act(() => {
        ref
          .current!.table.getColumn('tags')!
          .setFilterValue({ type: 'set', values: ['blue'] })
      })

      // Assert — rows sharing 'blue' remain (matched per-token, not on the
      // whole joined "red, blue" string)
      expect(bodyCells('name').map((c) => c.textContent)).toEqual([
        'alpha',
        'gamma',
      ])
    })

    it('filters a yesNo column on the Yes/No display value (not true/false)', () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      renderGrid({ ref })

      // Act — filter Enabled to "Yes"
      act(() => {
        ref
          .current!.table.getColumn('isEnabled')!
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

      // Assert — one <col> more than data columns (the filler) in EACH of the
      // two tables (split header + body viewports share one colgroup shape)
      const tables = container.querySelectorAll('table')
      expect(tables).toHaveLength(2)
      tables.forEach((t) =>
        expect(t.querySelectorAll('colgroup col')).toHaveLength(
          columns.length + 1,
        ),
      )

      // Header row ends in an aria-hidden filler th
      const headerRow = container.querySelector(
        'thead tr',
      ) as HTMLTableRowElement
      const headerCells = headerRow.querySelectorAll('th')
      expect(headerCells[headerCells.length - 1]).toHaveAttribute(
        'aria-hidden',
        'true',
      )

      // Each body row ends in an aria-hidden filler td
      const firstBodyRow = container.querySelector(
        'tbody tr',
      ) as HTMLTableRowElement
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
        <WaydGrid<Node>
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

  describe('grouped headers (ColGroupDef-style bands)', () => {
    // Two bands over three leaves, plus one ungrouped column: the band row
    // must span its leaves via colSpan and pass the ungrouped column through
    // as an empty placeholder cell.
    const groupedColumns: ColumnDef<Flag, unknown>[] = [
      {
        id: 'info',
        header: 'Info',
        columns: [
          { id: 'name', accessorKey: 'name', header: 'Name' },
          { id: 'type', accessorKey: 'type', header: 'Type' },
        ],
      },
      {
        id: 'state',
        header: 'State',
        columns: [
          {
            id: 'isEnabled',
            accessorKey: 'isEnabled',
            header: 'Enabled',
            meta: { columnType: 'yesNo' } satisfies WaydGridColumnMeta,
          },
        ],
      },
      { id: 'id', accessorKey: 'id', header: 'Id' },
    ]

    const bandRows = () =>
      Array.from(
        document.querySelectorAll('tr[data-role="header-band"]'),
      ) as HTMLTableRowElement[]

    it('renders one band row with group labels spanning their leaves', () => {
      // Arrange / Act
      renderGrid({ columns: groupedColumns })

      // Assert — a single band row above the leaf header row
      const bands = bandRows()
      expect(bands).toHaveLength(1)

      // Group cells span their visible leaves; the ungrouped column passes
      // through as a placeholder; the trailing filler cell closes the row.
      const cells = Array.from(bands[0].cells)
      expect(cells.map((c) => c.textContent)).toEqual(['Info', 'State', '', ''])
      expect(cells.map((c) => c.colSpan)).toEqual([2, 1, 1, 1])

      // Empty placeholder + filler cells are hidden from assistive tech.
      expect(cells.map((c) => c.getAttribute('aria-hidden'))).toEqual([
        null,
        null,
        'true',
        'true',
      ])
    })

    it('renders no band row for flat (ungrouped) columns', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      expect(bandRows()).toHaveLength(0)
    })

    it('renders the floating filter row exactly once', () => {
      // Arrange / Act
      renderGrid({ columns: groupedColumns })

      // Assert
      expect(
        document.querySelectorAll('tr[data-role="floating-filters"]'),
      ).toHaveLength(1)
    })

    it('sorts a grouped leaf column on header click', () => {
      // Arrange
      renderGrid({ columns: groupedColumns })

      // Act — click the leaf header, not the band
      fireEvent.click(screen.getByText('Name'))

      // Assert — rows sort ascending by name
      const names = bodyCells('name').map((c) => c.textContent)
      expect(names).toEqual(['insights', 'planning-poker', 'roadmap'])
    })

    it('applies meta.columnType to leaves nested under a band', () => {
      // Arrange / Act
      renderGrid({ columns: groupedColumns })

      // Assert — the yesNo type formats the grouped Enabled leaf
      const enabled = bodyCells('isEnabled').map((c) => c.textContent)
      expect(enabled).toEqual(['Yes', 'No', 'Yes'])
    })

    it('hides a grouped leaf via meta.hide and shrinks the band colSpan', () => {
      // Arrange — hide Type inside the Info band
      const cols: ColumnDef<Flag, unknown>[] = [
        {
          id: 'info',
          header: 'Info',
          columns: [
            { id: 'name', accessorKey: 'name', header: 'Name' },
            {
              id: 'type',
              accessorKey: 'type',
              header: 'Type',
              meta: { hide: true } satisfies WaydGridColumnMeta,
            },
          ],
        },
        { id: 'id', accessorKey: 'id', header: 'Id' },
      ]

      // Act
      renderGrid({ columns: cols })

      // Assert — Type is gone and Info spans only the remaining leaf
      expect(screen.queryByText('Type')).not.toBeInTheDocument()
      const infoCell = Array.from(bandRows()[0].cells).find(
        (c) => c.textContent === 'Info',
      )
      expect(infoCell?.colSpan).toBe(1)
    })

    it('exports band labels as a prelude row above the leaf headers', () => {
      // Arrange
      mockDownloadCsv.mockClear()
      const { container } = renderGrid({ columns: groupedColumns })

      // Act
      const exportBtn = container
        .querySelector('[aria-label="download"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(exportBtn)

      // Assert — band row (label at each group's first column, blank across
      // the span, ungrouped Id blank), then leaf headers, then data
      expect(mockDownloadCsv).toHaveBeenCalledTimes(1)
      const lines = (mockDownloadCsv.mock.calls[0][0] as string).split('\n')
      expect(lines[0]).toBe('Info,,State,')
      expect(lines[1]).toBe('Name,Type,Enabled,Id')
      expect(lines[2]).toContain('planning-poker')
    })
  })

  describe('displayed-rows surface (onDisplayedRowsChange / getDisplayedRows)', () => {
    it('fires on mount with all rows in display order', () => {
      // Arrange / Act
      const onDisplayedRowsChange = jest.fn()
      renderGrid({ onDisplayedRowsChange })

      // Assert
      expect(onDisplayedRowsChange).toHaveBeenCalled()
      const last = onDisplayedRowsChange.mock.calls.at(-1)![0] as Flag[]
      expect(last.map((f) => f.name)).toEqual([
        'planning-poker',
        'roadmap',
        'insights',
      ])
    })

    it('fires with the filtered subset when the global search changes', () => {
      // Arrange
      const onDisplayedRowsChange = jest.fn()
      renderGrid({ onDisplayedRowsChange })

      // Act
      fireEvent.change(screen.getByPlaceholderText('Search'), {
        target: { value: 'road' },
      })

      // Assert
      const last = onDisplayedRowsChange.mock.calls.at(-1)![0] as Flag[]
      expect(last.map((f) => f.name)).toEqual(['roadmap'])
    })

    it('fires with the re-sorted order when a sort is applied', () => {
      // Arrange
      const onDisplayedRowsChange = jest.fn()
      renderGrid({ onDisplayedRowsChange })

      // Act
      fireEvent.click(screen.getByText('Name'))

      // Assert
      const last = onDisplayedRowsChange.mock.calls.at(-1)![0] as Flag[]
      expect(last.map((f) => f.name)).toEqual([
        'insights',
        'planning-poker',
        'roadmap',
      ])
    })

    it('exposes the displayed rows via the handle', () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      renderGrid({ ref })

      // Act — filter, then read the handle
      fireEvent.change(screen.getByPlaceholderText('Search'), {
        target: { value: 'insights' },
      })
      const displayed = ref.current!.getDisplayedRows() as Flag[]

      // Assert
      expect(displayed.map((f) => f.name)).toEqual(['insights'])
    })
  })

  describe('flat row-reorder DnD (onRowReorder)', () => {
    it('wraps rows in sortable rows keyed by getRowId', () => {
      // Arrange / Act
      renderGrid({
        onRowReorder: jest.fn(),
        getRowId: (row) => `flag-${row.id}`,
      })

      // Assert — each body row carries its sortable data-row-id
      const rowIds = Array.from(
        document.querySelectorAll('tbody tr[data-row-id]'),
      ).map((tr) => tr.getAttribute('data-row-id'))
      expect(rowIds).toEqual(['flag-1', 'flag-2', 'flag-3'])
    })

    it('renders plain rows (no sortable wrapper) without onRowReorder', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      expect(document.querySelectorAll('tbody tr[data-row-id]')).toHaveLength(0)
    })

    it('reports drag disabled through the column context while sorted', () => {
      // Arrange — a columns function that surfaces context.isDragEnabled
      const seen: boolean[] = []
      renderGrid({
        onRowReorder: jest.fn(),
        getRowId: (row) => String(row.id),
        columns: (context) => {
          seen.push(context.isDragEnabled)
          return columns
        },
      })
      expect(seen.at(-1)).toBe(true)

      // Act — apply a sort (displayed order no longer the data order)
      fireEvent.click(screen.getByText('Name'))

      // Assert
      expect(seen.at(-1)).toBe(false)
    })
  })

  describe('meta.headerTooltip', () => {
    const cols: ColumnDef<Flag, unknown>[] = [
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        meta: { headerTooltip: 'The flag name' },
      },
    ]

    it('keeps click-to-sort working through the wrapped header label', () => {
      // Arrange — antd tooltips portal on hover (not renderable in jsdom);
      // assert the anchor renders and stays interactive.
      renderGrid({ columns: cols })
      const label = screen.getByText('Name')

      // Act
      fireEvent.click(label)

      // Assert — rows sort ascending by name
      const names = bodyCells('name').map((c) => c.textContent)
      expect(names).toEqual(['insights', 'planning-poker', 'roadmap'])
    })

    it('exports the plain string header (no exportHeader override needed)', () => {
      // Arrange
      mockDownloadCsv.mockClear()
      const { container } = renderGrid({ columns: cols })

      // Act
      const exportBtn = container
        .querySelector('[aria-label="download"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(exportBtn)

      // Assert
      const csv = mockDownloadCsv.mock.calls[0][0] as string
      expect(csv.split('\n')[0]).toBe('Name')
    })
  })

  describe('dotted accessorKeys over optional relations', () => {
    interface Row {
      id: number
      name: string
      team?: { name: string }
    }

    const rows: Row[] = [
      { id: 1, name: 'alpha', team: { name: 'Juice' } },
      { id: 2, name: 'beta' }, // no team — the hop TanStack would warn on
    ]

    const cols: ColumnDef<Row, unknown>[] = [
      { id: 'name', accessorKey: 'name', header: 'Name' },
      { accessorKey: 'team.name', header: 'Team' },
    ]

    it('renders values without TanStack deep-accessor dev warnings', () => {
      // Arrange
      const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {})

      try {
        // Act
        render(<WaydGrid<Row> data={rows} columns={cols} />)

        // Assert — value renders, and no "deeply nested key" warning fired
        expect(screen.getByText('Juice')).toBeInTheDocument()
        const deepWarnings = warnSpy.mock.calls.filter((call) =>
          String(call[0]).includes('deeply nested'),
        )
        expect(deepWarnings).toHaveLength(0)
      } finally {
        warnSpy.mockRestore()
      }
    })

    it('keeps the TanStack-derived column id (dots to underscores)', () => {
      // Arrange / Act
      render(<WaydGrid<Row> data={rows} columns={cols} />)

      // Assert — body cells carry the derived data-column-id
      expect(
        document.querySelectorAll('tbody td[data-column-id="team_name"]'),
      ).toHaveLength(2)
    })
  })

  describe('row virtualization', () => {
    // jsdom has no layout; jest.setup.ts mocks the body viewport's rect to
    // 800×600 (see data-grid-body-viewport). These tests pin the resulting
    // window plus the spacer geometry standing in for the unrendered rows.
    const ROW_ESTIMATE = 28
    // 600px viewport ÷ 28px rows → indexes 0-21 visible, +10 overscan = 32.
    const WINDOW_SIZE = 32

    const BIG_DATA: Flag[] = Array.from({ length: 200 }, (_, i) => ({
      id: i + 1,
      name: `flag-${String(i + 1).padStart(3, '0')}`,
      isEnabled: i % 2 === 0,
      type: i % 3 === 0 ? 'System' : 'User',
    }))

    /** The scrolling body viewport (parent of the body table, tables[1]). */
    const bodyViewport = (container: HTMLElement) =>
      container.querySelectorAll('table')[1].parentElement as HTMLElement

    /** Spacer rows (aria-hidden <tr>s) with their pixel heights. */
    const spacerHeights = () =>
      Array.from(
        document.querySelectorAll('tbody tr[aria-hidden="true"] td'),
      ).map((td) => (td as HTMLElement).style.height)

    it('renders only the virtual window of a large dataset, keeping the row model complete', () => {
      // Arrange / Act
      renderGrid({ data: BIG_DATA })

      // Assert — the toolbar count (row model) sees all rows…
      expect(screen.getByText('200 of 200')).toBeInTheDocument()

      // …but the DOM holds only the window, from the top of the list
      const names = bodyCells('name').map((c) => c.textContent)
      expect(names).toHaveLength(WINDOW_SIZE)
      expect(names[0]).toBe('flag-001')
      expect(names[WINDOW_SIZE - 1]).toBe(`flag-0${WINDOW_SIZE}`)

      // A single bottom spacer holds the unrendered remainder's height so
      // scroll geometry matches the full dataset
      expect(spacerHeights()).toEqual([
        `${(BIG_DATA.length - WINDOW_SIZE) * ROW_ESTIMATE}px`,
      ])
    })

    it('moves the window (and adds a top spacer) when the body viewport scrolls', () => {
      // Arrange
      const { container } = renderGrid({ data: BIG_DATA })
      const viewport = bodyViewport(container)

      // Act — scroll to row index 50
      viewport.scrollTop = 50 * ROW_ESTIMATE
      fireEvent.scroll(viewport)

      // Assert — the window re-anchors around index 50 (± overscan): rows
      // before index 40 are unmounted, replaced by a top spacer of their
      // exact height
      const names = bodyCells('name').map((c) => c.textContent)
      expect(names[0]).toBe('flag-041')
      expect(names).not.toContain('flag-001')
      expect(spacerHeights()[0]).toBe(`${40 * ROW_ESTIMATE}px`)
    })

    it('exports ALL rows to CSV, not just the rendered window', () => {
      // Arrange
      mockDownloadCsv.mockClear()
      const { container } = renderGrid({ data: BIG_DATA })

      // Act
      const exportBtn = container
        .querySelector('[aria-label="download"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(exportBtn)

      // Assert — header row + one line per data row, including unrendered ones
      const csv = mockDownloadCsv.mock.calls[0][0] as string
      expect(csv.split('\n')).toHaveLength(BIG_DATA.length + 1)
      expect(csv).toContain('flag-200')
    })

    it('reports ALL displayed rows through onDisplayedRowsChange, not just the window', () => {
      // Arrange / Act
      const onDisplayedRowsChange = jest.fn()
      renderGrid({ data: BIG_DATA, onDisplayedRowsChange })

      // Assert
      const last = onDisplayedRowsChange.mock.calls.at(-1)![0] as Flag[]
      expect(last).toHaveLength(BIG_DATA.length)
    })
  })

  describe('initialSorting', () => {
    it('applies the initial sort on mount', () => {
      // Arrange / Act
      renderGrid({ initialSorting: [{ id: 'name', desc: true }] })

      // Assert — rows sorted descending by name without any user click
      const names = bodyCells('name').map((c) => c.textContent)
      expect(names).toEqual(['roadmap', 'planning-poker', 'insights'])
    })

    it('clears the initial sort via the toolbar Clear button', () => {
      // Arrange
      const { container } = renderGrid({
        initialSorting: [{ id: 'name', desc: true }],
      })

      // Act — the clear button carries a clear icon (aria-label="clear")
      const clearBtn = container
        .querySelector('[aria-label="clear"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(clearBtn)

      // Assert — back to data order
      const names = bodyCells('name').map((c) => c.textContent)
      expect(names).toEqual(['planning-poker', 'roadmap', 'insights'])
    })
  })

  describe('column menu', () => {
    it('renders a menu trigger in every leaf header cell', () => {
      // Arrange / Act
      renderGrid()

      // Assert — one ⋮ trigger per column (portal-driven menu behavior is
      // exercised in the browser, not jsdom)
      expect(screen.getAllByLabelText('Column menu')).toHaveLength(
        columns.length,
      )
    })

    it('opening the menu does not toggle the column sort', () => {
      // Arrange
      renderGrid()
      const namesBefore = bodyCells('name').map((c) => c.textContent)

      // Act — the trigger sits inside the sortable <th>
      fireEvent.click(screen.getAllByLabelText('Column menu')[0])

      // Assert — row order untouched
      expect(bodyCells('name').map((c) => c.textContent)).toEqual(namesBefore)
    })
  })

  describe('column pinning', () => {
    it('reorders a left-pinned column to the front of both tables', () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      renderGrid({ ref })

      // Act
      act(() => {
        ref.current!.table.getColumn('type').pin('left')
      })

      // Assert — header and body cells lead with the pinned column. Target
      // the leaf header row explicitly (band/floating rows carry data-role),
      // so this survives grids with grouped headers.
      const headerRow = document.querySelector(
        'thead tr:not([data-role])',
      ) as HTMLElement
      const firstTh = headerRow.querySelector('th[data-column-id]')
      expect(firstTh?.getAttribute('data-column-id')).toBe('type')
      const firstBodyRow = document.querySelector('tbody tr') as HTMLElement
      const firstTd = firstBodyRow.querySelector('td[data-column-id]')
      expect(firstTd?.getAttribute('data-column-id')).toBe('type')
    })

    it('applies the sticky inset to pinned header and body cells', () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      renderGrid({ ref })

      // Act — pin two columns left; the second is offset by the first's width
      act(() => {
        ref.current!.table.getColumn('name').pin('left')
        ref.current!.table.getColumn('type').pin('left')
      })

      // Assert
      const nameTh = document.querySelector(
        'th[data-column-id="name"]',
      ) as HTMLElement
      const typeTh = document.querySelector(
        'th[data-column-id="type"]',
      ) as HTMLElement
      expect(nameTh.style.left).toBe('0px')
      expect(typeTh.style.left).toBe(
        `${ref.current!.table.getColumn('name').getSize()}px`,
      )
      const nameTd = bodyCells('name')[0]
      expect(nameTd.style.left).toBe('0px')
    })

    it('leaves unpinned cells without a sticky inset', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      const nameTh = document.querySelector(
        'th[data-column-id="name"]',
      ) as HTMLElement
      expect(nameTh.style.left).toBe('')
      expect(bodyCells('name')[0].style.left).toBe('')
    })
  })

  describe('column reordering', () => {
    /** data-column-id of every leaf header cell, in rendered order. */
    const headerOrder = () => {
      const headerRow = document.querySelector(
        'thead tr:not([data-role])',
      ) as HTMLElement
      return Array.from(headerRow.querySelectorAll('th[data-column-id]')).map(
        (th) => th.getAttribute('data-column-id'),
      )
    }

    it('renders columns in the applied columnOrder', () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      renderGrid({ ref })
      expect(headerOrder()).toEqual(['name', 'type', 'isEnabled'])

      // Act — move isEnabled to the front
      act(() => {
        ref.current!.table.setColumnOrder(['isEnabled', 'name', 'type'])
      })

      // Assert — header and body cells both follow the new order
      expect(headerOrder()).toEqual(['isEnabled', 'name', 'type'])
      const firstBodyRow = document.querySelector('tbody tr') as HTMLElement
      expect(
        firstBodyRow
          .querySelector('td[data-column-id]')
          ?.getAttribute('data-column-id'),
      ).toBe('isEnabled')
    })

    /** Leaf header cells that are drag-reorderable (whole cell is the handle;
     *  dnd-kit stamps aria-roledescription="sortable" on it). */
    const reorderableHeaders = () =>
      document.querySelectorAll(
        'thead tr:not([data-role]) th[data-column-id][aria-roledescription="sortable"]',
      )

    it('makes every reorderable leaf header cell a drag handle', () => {
      // Arrange / Act
      renderGrid()

      // Assert — the whole cell is draggable (no separate grip element)
      expect(reorderableHeaders().length).toBe(3)
      expect(
        document.querySelectorAll('[aria-label="Reorder column"]'),
      ).toHaveLength(0)
    })

    it('makes no header cell a drag handle on a grouped-header grid', () => {
      // Arrange — a band splits reordering, so it is disabled
      const groupedColumns: ColumnDef<Flag, unknown>[] = [
        {
          id: 'group',
          header: 'Group',
          columns: [
            { id: 'name', accessorKey: 'name', header: 'Name' },
            { id: 'type', accessorKey: 'type', header: 'Type' },
          ],
        },
      ]

      // Act
      renderGrid({ columns: groupedColumns })

      // Assert
      expect(reorderableHeaders()).toHaveLength(0)
    })

    it('keeps the actions column from becoming a drag handle', () => {
      // Arrange — actions column opts out of reordering
      const withActions: ColumnDef<Flag, unknown>[] = [
        ...columns,
        createActionsColumn<Flag>({
          getItems: () => [{ key: 'edit', label: 'Edit' }],
        }),
      ]

      // Act
      renderGrid({ columns: withActions })

      // Assert — the actions header is not draggable; the 3 data columns are
      const actionsTh = document.querySelector(
        'thead tr:not([data-role]) th[data-column-id="actions"]',
      ) as HTMLElement
      expect(actionsTh.getAttribute('aria-roledescription')).not.toBe(
        'sortable',
      )
      expect(reorderableHeaders().length).toBe(3)
    })

    it('exports CSV in the displayed column order', () => {
      // Arrange
      mockDownloadCsv.mockClear()
      const ref = createRef<WaydGridHandle>()
      const { container } = renderGrid({ ref })

      // Act — reorder, then click the toolbar export (download icon) button
      act(() => {
        ref.current!.table.setColumnOrder(['type', 'isEnabled', 'name'])
      })
      const exportBtn = container
        .querySelector('[aria-label="download"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(exportBtn)

      // Assert — the header row of the CSV follows the display order
      const csv = mockDownloadCsv.mock.calls.at(-1)?.[0] as string
      expect(csv.split('\n')[0]).toBe('Type,Enabled,Name')
    })
  })

  describe('column state persistence (persistStateKey)', () => {
    const STORAGE_KEY = gridStateStorageKey('test-grid')

    beforeEach(() => {
      // jest.setup's localStorage stub has no backing store — replace it with
      // a real in-memory implementation for persistence round-trips
      const localStorageMock = (() => {
        let store: Record<string, string> = {}
        return {
          getItem: (key: string) => store[key] ?? null,
          setItem: (key: string, value: string) => {
            store[key] = value
          },
          removeItem: (key: string) => {
            delete store[key]
          },
          clear: () => {
            store = {}
          },
          get length() {
            return Object.keys(store).length
          },
          key: (index: number) => {
            const keys = Object.keys(store)
            return keys[index] ?? null
          },
        }
      })()
      Object.defineProperty(window, 'localStorage', {
        value: localStorageMock,
        writable: true,
      })
      jest.useFakeTimers()
    })

    afterEach(() => {
      jest.useRealTimers()
    })

    it('restores sizing, user visibility, and pinning from a stored entry', () => {
      // Arrange
      window.localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          columnSizing: { name: 240 },
          userColumnVisibility: { type: false },
          columnPinning: { left: ['isEnabled'], right: [] },
        }),
      )
      const ref = createRef<WaydGridHandle>()

      // Act
      renderGrid({ ref, persistStateKey: 'test-grid' })

      // Assert — the user-hidden column is gone from the DOM (the raw user
      // layer flows through mergeColumnVisibility), the pinned column leads,
      // and the stored width is applied
      expect(
        document.querySelector('th[data-column-id="type"]'),
      ).not.toBeInTheDocument()
      const headerRow = document.querySelector(
        'thead tr:not([data-role])',
      ) as HTMLElement
      expect(
        headerRow
          .querySelector('th[data-column-id]')
          ?.getAttribute('data-column-id'),
      ).toBe('isEnabled')
      expect(ref.current!.table.getColumn('name').getSize()).toBe(240)
    })

    it('saves changes and restores them on a fresh mount', () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const { unmount } = renderGrid({ ref, persistStateKey: 'test-grid' })

      // Act — resize and pin through the table (wired to useGridState), let
      // the debounce elapse, then remount
      act(() => {
        ref.current!.table.setColumnSizing({ name: 300 })
        ref.current!.table.getColumn('type').pin('left')
      })
      act(() => {
        jest.advanceTimersByTime(1000)
      })
      unmount()

      // Assert — the entry holds the full payload
      expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
        columnSizing: { name: 300 },
        userColumnVisibility: {},
        columnPinning: { left: ['type'], right: [] },
      })

      // Act — fresh mount restores it
      const ref2 = createRef<WaydGridHandle>()
      renderGrid({ ref: ref2, persistStateKey: 'test-grid' })

      // Assert
      expect(ref2.current!.table.getColumn('name').getSize()).toBe(300)
      const headerRow = document.querySelector(
        'thead tr:not([data-role])',
      ) as HTMLElement
      expect(
        headerRow
          .querySelector('th[data-column-id]')
          ?.getAttribute('data-column-id'),
      ).toBe('type')
    })

    it('never touches storage without a persistStateKey', () => {
      // Arrange
      const getItemSpy = jest.spyOn(window.localStorage, 'getItem')
      const setItemSpy = jest.spyOn(window.localStorage, 'setItem')
      const ref = createRef<WaydGridHandle>()

      // Act
      renderGrid({ ref })
      act(() => {
        ref.current!.table.setColumnSizing({ name: 300 })
        jest.advanceTimersByTime(1000)
      })

      // Assert
      expect(getItemSpy).not.toHaveBeenCalled()
      expect(setItemSpy).not.toHaveBeenCalled()
    })

    it('persists a column reorder and restores it on a fresh mount', () => {
      // Arrange
      const ref = createRef<WaydGridHandle>()
      const { unmount } = renderGrid({ ref, persistStateKey: 'test-grid' })

      // Act — reorder, let the debounce elapse, remount
      act(() => {
        ref.current!.table.setColumnOrder(['isEnabled', 'name', 'type'])
      })
      act(() => {
        jest.advanceTimersByTime(1000)
      })
      unmount()

      // Assert — the payload carries the order
      expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
        columnSizing: {},
        userColumnVisibility: {},
        columnPinning: { left: [], right: [] },
        columnOrder: ['isEnabled', 'name', 'type'],
      })

      // Act — fresh mount restores the display order
      renderGrid({ persistStateKey: 'test-grid' })

      // Assert
      const headerRow = document.querySelector(
        'thead tr:not([data-role])',
      ) as HTMLElement
      expect(
        Array.from(headerRow.querySelectorAll('th[data-column-id]')).map((th) =>
          th.getAttribute('data-column-id'),
        ),
      ).toEqual(['isEnabled', 'name', 'type'])
    })
  })

  describe('numeric cell alignment', () => {
    // `id` is numeric in DATA, so its filter/data type infers to 'number'.
    const numericColumns: ColumnDef<Flag, unknown>[] = [
      ...columns,
      { id: 'id', accessorKey: 'id', header: 'Id' },
    ]

    it('right-aligns body cells of a data-inferred numeric column, but not its header', () => {
      // Arrange / Act
      renderGrid({ columns: numericColumns })

      // Assert — numeric cells carry the alignment class; the header and
      // text-column cells do not
      expect(bodyCells('id')[0].className).toContain('tdNumeric')
      expect(
        document.querySelector('th[data-column-id="id"]')?.className,
      ).not.toContain('tdNumeric')
      expect(bodyCells('name')[0].className).not.toContain('tdNumeric')
    })

    it('meta.align overrides the default in both directions', () => {
      // Arrange
      const overriddenColumns: ColumnDef<Flag, unknown>[] = [
        {
          id: 'name',
          accessorKey: 'name',
          header: 'Name',
          meta: { align: 'right' } satisfies WaydGridColumnMeta,
        },
        {
          id: 'id',
          accessorKey: 'id',
          header: 'Id',
          meta: { align: 'left' } satisfies WaydGridColumnMeta,
        },
      ]

      // Act
      renderGrid({ columns: overriddenColumns })

      // Assert
      expect(bodyCells('name')[0].className).toContain('tdNumeric')
      expect(bodyCells('id')[0].className).not.toContain('tdNumeric')
    })

    it('leaves boolean (yesNo set-filter) columns left-aligned', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      expect(bodyCells('isEnabled')[0].className).not.toContain('tdNumeric')
    })
  })
})
