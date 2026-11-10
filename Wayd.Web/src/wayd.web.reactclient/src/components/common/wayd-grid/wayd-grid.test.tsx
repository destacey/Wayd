// Capture CSV export output. Keep generateCsv real so we assert on real content.
vi.mock(import('@/src/utils/csv-utils'), async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    downloadCsvWithTimestamp: vi.fn(),
  }
})

import type { Mock } from 'vitest'
import { createRef } from 'react'
import { render, screen, fireEvent, within, act } from '@testing-library/react'
import type { ColumnDef } from '../wayd-grid-core'
import WaydGrid from './wayd-grid'
import { createCsvColumn } from '../wayd-grid-core/csv-column'
import { SET_FILTER_BLANK } from '../wayd-grid-core/filters'
import { createActionsColumn } from '../wayd-grid-core/actions-column'
import { gridStateStorageKey } from '../wayd-grid-core/use-grid-persistence'
import type { WaydGridHandle, WaydGridColumnMeta } from './types'
import { downloadCsvWithTimestamp } from '@/src/utils/csv-utils'

const mockDownloadCsv = downloadCsvWithTimestamp as Mock

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

  describe('meta.unavailable', () => {
    it('omits a column from the DOM when meta.unavailable is true', () => {
      // Arrange
      const hiddenCols: ColumnDef<Flag, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        {
          id: 'type',
          accessorKey: 'type',
          header: 'Type',
          meta: { unavailable: true } satisfies WaydGridColumnMeta,
        },
      ]

      // Act
      render(<WaydGrid<Flag> data={DATA} columns={hiddenCols} />)

      // Assert — Type header absent, its cells absent, Name still present
      expect(screen.queryByText('Type')).not.toBeInTheDocument()
      expect(bodyCells('type')).toHaveLength(0)
      expect(screen.getByText('Name')).toBeInTheDocument()
    })

    it('shows the column when meta.unavailable is false', () => {
      // Arrange
      const cols: ColumnDef<Flag, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        {
          id: 'type',
          accessorKey: 'type',
          header: 'Type',
          meta: { unavailable: false } satisfies WaydGridColumnMeta,
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

  describe('auto-injected Id column', () => {
    /** Renders and clicks the toolbar export button; returns the CSV. */
    const exportFlags = (props: GridProps = {}) => {
      mockDownloadCsv.mockClear()
      const { container } = renderGrid(props)
      const exportBtn = container
        .querySelector('[aria-label="download"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(exportBtn)
      return mockDownloadCsv.mock.calls[0][0] as string
    }

    it('is hidden on a grid whose rows carry an id', () => {
      // Arrange / Act
      renderGrid()

      // Assert
      expect(bodyCells('id')).toHaveLength(0)
    })

    it('stays out of the export while hidden', () => {
      // Arrange / Act
      const csv = exportFlags()

      // Assert — the header row names the visible columns only
      expect(csv).toContain('Name')
      expect(csv.split('\n')[0]).not.toContain('Id')
    })

    it('is not added when the consumer opts out', () => {
      // Arrange / Act
      renderGrid({ includeIdColumn: false })

      // Assert
      expect(bodyCells('id')).toHaveLength(0)
    })

    it('leaves a consumer-defined id column visible', () => {
      // Arrange — the consumer claims `id` themselves
      const cols: ColumnDef<Flag, unknown>[] = [
        { id: 'id', accessorKey: 'id', header: 'Job Id' },
        { id: 'name', accessorKey: 'name', header: 'Name' },
      ]

      // Act
      renderGrid({ columns: cols })

      // Assert — the consumer's column renders, unaffected by injection
      expect(screen.getByText('Job Id')).toBeInTheDocument()
      expect(bodyCells('id').map((c) => c.textContent)).toEqual(['1', '2', '3'])
    })

    it('is not added when the rows have no id', () => {
      // Arrange
      interface Keyed {
        key: number
        name: string
      }
      const cols: ColumnDef<Keyed, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
      ]

      // Act
      render(
        <WaydGrid<Keyed> data={[{ key: 1, name: 'Alpha' }]} columns={cols} />,
      )

      // Assert
      expect(bodyCells('id')).toHaveLength(0)
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
        team: { name: 'Nova' },
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
      expect(csv).toContain('Nova')
    })

    it('excludes hidden columns (meta.unavailable) from the export', () => {
      // Arrange — Status is hidden via meta.unavailable
      const cols: ColumnDef<Obj, unknown>[] = [
        { id: 'name', accessorKey: 'name', header: 'Name' },
        {
          id: 'status',
          accessorKey: 'status.name',
          header: 'Status',
          meta: { unavailable: true } satisfies WaydGridColumnMeta,
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
        { id: 1, name: 'alpha', team: 'Nova' },
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
          .setFilterValue({ type: 'set', values: ['Nova'] })
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

    // The rows sort even when the header is stale, because the row model reads
    // the sort state directly — so asserting order alone let a frozen
    // getIsSorted() ship. These assert the indicator the user actually sees.
    it('shows an ascending indicator on the sorted header', () => {
      // Arrange
      const { container } = renderGrid()
      const header = screen.getByText('Name').closest('th')!

      // Act
      fireEvent.click(screen.getByText('Name'))

      // Assert
      expect(
        header.querySelectorAll('[class*="anticon-arrow-up"]'),
      ).toHaveLength(1)
      expect(
        container.querySelectorAll('[class*="anticon-arrow-down"]'),
      ).toHaveLength(0)
    })

    it('flips the indicator to descending on a second click', () => {
      // Arrange
      renderGrid()
      const header = screen.getByText('Name').closest('th')!

      // Act
      fireEvent.click(screen.getByText('Name'))
      fireEvent.click(screen.getByText('Name'))

      // Assert
      expect(
        header.querySelectorAll('[class*="anticon-arrow-down"]'),
      ).toHaveLength(1)
      expect(
        header.querySelectorAll('[class*="anticon-arrow-up"]'),
      ).toHaveLength(0)
    })

    it('clears the indicator on the third click', () => {
      // Arrange
      renderGrid()
      const header = screen.getByText('Name').closest('th')!

      // Act
      fireEvent.click(screen.getByText('Name'))
      fireEvent.click(screen.getByText('Name'))
      fireEvent.click(screen.getByText('Name'))

      // Assert
      expect(header.querySelectorAll('[class*="anticon-arrow"]')).toHaveLength(
        0,
      )
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
      const onRefresh = vi.fn()
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

    it('stripes alternate tree rows by display position', () => {
      // Arrange / Act
      renderTree()

      // Assert — the second and fourth displayed rows take the stripe
      const rowOf = (name: string) => screen.getByText(name).closest('tr')!
      expect(rowOf('Plan').className).not.toContain('trAlt')
      expect(rowOf('Kickoff').className).toContain('trAlt')
      expect(rowOf('Execute').className).not.toContain('trAlt')
      expect(rowOf('Build widgets').className).toContain('trAlt')
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

    it('hides a grouped leaf via meta.unavailable and shrinks the band colSpan', () => {
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
              meta: { unavailable: true } satisfies WaydGridColumnMeta,
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
      const onDisplayedRowsChange = vi.fn()
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
      const onDisplayedRowsChange = vi.fn()
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
      const onDisplayedRowsChange = vi.fn()
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
        onRowReorder: vi.fn(),
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
        onRowReorder: vi.fn(),
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
      { id: 1, name: 'alpha', team: { name: 'Nova' } },
      { id: 2, name: 'beta' }, // no team — the hop TanStack would warn on
    ]

    const cols: ColumnDef<Row, unknown>[] = [
      { id: 'name', accessorKey: 'name', header: 'Name' },
      { accessorKey: 'team.name', header: 'Team' },
    ]

    it('renders values without TanStack deep-accessor dev warnings', () => {
      // Arrange
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})

      try {
        // Act
        render(<WaydGrid<Row> data={rows} columns={cols} />)

        // Assert — value renders, and no "deeply nested key" warning fired
        expect(screen.getByText('Nova')).toBeInTheDocument()
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
    // jsdom has no layout; vitest.setup.dom.ts mocks the body viewport's rect to
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
      const onDisplayedRowsChange = vi.fn()
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

    it('omits the menu on control columns (meta.enableReordering false)', () => {
      // Arrange - a control column, matching createActionsColumn's shape
      const withControl = [
        ...columns,
        {
          id: 'row-actions',
          header: '',
          enableSorting: false,
          enableResizing: false,
          meta: { enableReordering: false } satisfies WaydGridColumnMeta,
          cell: () => null,
        },
      ] as ColumnDef<Flag, unknown>[]

      // Act
      renderGrid({ columns: withControl })

      // Assert - a trigger per real column, none on the control column
      expect(screen.getAllByLabelText('Column menu')).toHaveLength(
        columns.length,
      )
      expect(
        document
          .querySelector('th[data-column-id="row-actions"]')
          ?.querySelector('[aria-label="Column menu"]'),
      ).toBeNull()
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
        ref.current!.table.getColumn('type').pin('start')
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
        ref.current!.table.getColumn('name').pin('start')
        ref.current!.table.getColumn('type').pin('start')
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
        'thead tr:not([data-role]) th[data-column-id].thDraggable',
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
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
      vi.restoreAllMocks()
    })

    it('restores sizing, user visibility, and pinning from a stored entry', () => {
      // Arrange
      window.localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          columnSizing: { name: 240 },
          userColumnVisibility: { type: false },
          columnPinning: { start: ['isEnabled'], end: [] },
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
        ref.current!.table.getColumn('type').pin('start')
      })
      act(() => {
        vi.advanceTimersByTime(1000)
      })
      unmount()

      // Assert — the entry holds the full payload
      expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
        columnSizing: { name: 300 },
        userColumnVisibility: {},
        columnPinning: { start: ['type'], end: [] },
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
      const getItemSpy = vi.spyOn(Storage.prototype, 'getItem')
      const setItemSpy = vi.spyOn(Storage.prototype, 'setItem')
      const ref = createRef<WaydGridHandle>()

      // Act
      renderGrid({ ref })
      act(() => {
        ref.current!.table.setColumnSizing({ name: 300 })
        vi.advanceTimersByTime(1000)
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
        vi.advanceTimersByTime(1000)
      })
      unmount()

      // Assert — the payload carries the order
      expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
        columnSizing: {},
        userColumnVisibility: {},
        columnPinning: { start: [], end: [] },
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

  describe('rightPane (Gantt chart pane)', () => {
    it('does not render a bar track when rightPane is absent', () => {
      // Arrange / Act
      const { container } = renderGrid()
      // Assert — no gantt rows, one body viewport (the default single scroller).
      expect(container.querySelectorAll('[data-gantt-row]')).toHaveLength(0)
      expect(
        container.querySelectorAll('[data-grid-body-viewport]'),
      ).toHaveLength(1)
    })

    it('renders one bar per row via renderRow with its geometry', () => {
      // Arrange — a renderRow that emits a labeled marker carrying its top/height.
      const renderRow = vi.fn(({ row, top, height }) => (
        <div
          data-testid={`bar-${(row.original as Flag).id}`}
          data-top={top}
          data-height={height}
        >
          {(row.original as Flag).name}
        </div>
      ))
      // Act
      const { container } = renderGrid({
        rightPane: { renderRow, header: <div>Axis</div> },
      })
      // Assert — a bar per data row, wrapped in a gantt-row, geometry passed in.
      expect(container.querySelectorAll('[data-gantt-row]')).toHaveLength(
        DATA.length,
      )
      expect(screen.getByTestId('bar-1')).toBeInTheDocument()
      expect(screen.getByTestId('bar-1').getAttribute('data-top')).toBe('0')
      // Row 2 sits one row-height (28) below row 1 (uniform estimate).
      expect(screen.getByTestId('bar-2').getAttribute('data-top')).toBe('28')
      expect(renderRow).toHaveBeenCalled()
    })

    it('places bars at the rendered row height', () => {
      // Arrange — rows render at 29px against the 28px estimate
      vi.spyOn(
        HTMLTableRowElement.prototype,
        'getBoundingClientRect',
      ).mockReturnValue({ height: 29 } as DOMRect)
      const renderRow = ({
        row,
        top,
        height,
      }: {
        row: { original: Flag }
        top: number
        height: number
      }) => (
        <div
          data-testid={`bar-${row.original.id}`}
          data-top={top}
          data-height={height}
        />
      )

      // Act
      renderGrid({ rightPane: { renderRow } })

      // Assert
      expect(screen.getByTestId('bar-2')).toHaveAttribute('data-top', '29')
      expect(screen.getByTestId('bar-2')).toHaveAttribute('data-height', '29')
    })

    it('renders the rightPane header (e.g. the date axis)', () => {
      // Arrange / Act
      renderGrid({
        rightPane: {
          renderRow: () => null,
          header: <div>Date Axis</div>,
        },
      })
      // Assert
      expect(screen.getByText('Date Axis')).toBeInTheDocument()
    })

    it('keeps exactly one vertical scroll viewport when rightPane is present', () => {
      // Arrange / Act — the grid's own table wrapper stays the single scroll
      // viewport (carrying data-grid-body-viewport); the chart pane follows its
      // scroll rather than introducing a second scroller.
      const { container } = renderGrid({
        rightPane: { renderRow: () => null },
      })
      // Assert
      expect(
        container.querySelectorAll('[data-grid-body-viewport]'),
      ).toHaveLength(1)
    })
  })

  describe('simple variant', () => {
    it('fits its height to the rows at their rendered height', () => {
      // Arrange — rows render at 29px against the 28px estimate
      vi.spyOn(
        HTMLTableRowElement.prototype,
        'getBoundingClientRect',
      ).mockReturnValue({ height: 29 } as DOMRect)

      // Act
      const { container } = renderGrid({ variant: 'simple' })

      // Assert — header (39) + three rows + borders (2)
      expect(container.firstElementChild).toHaveStyle({
        height: `${39 + DATA.length * 29 + 2}px`,
      })
    })
  })

  describe('row activation', () => {
    /** The body rows, in display order. */
    const bodyRows = () =>
      Array.from(document.querySelectorAll('tbody tr[data-grid-row]'))

    it('keeps rows as grid rows, never buttons', () => {
      // Arrange / Act — the cells take focus and keys; a row claiming a
      // button role would hide the grid's structure from assistive tech.
      renderGrid({ onRowActivate: vi.fn() })

      // Assert
      bodyRows().forEach((row) => {
        expect(row).toHaveAttribute('role', 'row')
        expect(row).not.toHaveAttribute('tabindex')
      })
    })

    it('activates the clicked row', () => {
      // Arrange
      const onRowActivate = vi.fn()
      renderGrid({ onRowActivate })

      // Act
      fireEvent.click(bodyCells('name')[1])

      // Assert — the row's data, not the TanStack row wrapper
      expect(onRowActivate).toHaveBeenCalledTimes(1)
      expect(onRowActivate).toHaveBeenCalledWith(DATA[1])
    })

    it('activates on Enter and Space in a focused cell', () => {
      // Arrange
      const onRowActivate = vi.fn()
      renderGrid({ onRowActivate })
      const cell = bodyCells('type')[0]

      // Act
      fireEvent.keyDown(cell, { key: 'Enter' })
      fireEvent.keyDown(cell, { key: ' ' })

      // Assert
      expect(onRowActivate).toHaveBeenCalledTimes(2)
      expect(onRowActivate).toHaveBeenNthCalledWith(1, DATA[0])
      expect(onRowActivate).toHaveBeenNthCalledWith(2, DATA[0])
    })

    it('ignores other keys', () => {
      // Arrange
      const onRowActivate = vi.fn()
      renderGrid({ onRowActivate })

      // Act
      fireEvent.keyDown(bodyCells('name')[0], { key: 'a' })

      // Assert
      expect(onRowActivate).not.toHaveBeenCalled()
    })

    it('names the row from getRowActivateLabel', () => {
      // Arrange / Act
      renderGrid({
        onRowActivate: vi.fn(),
        getRowActivateLabel: (flag) => `Open ${flag.name}`,
      })

      // Assert
      expect(bodyRows()[0]).toHaveAttribute('aria-label', 'Open planning-poker')
    })

    it('leaves clicks on interactive cell content alone', () => {
      // Arrange — an actions button inside an activatable row must open its
      // own thing, not the row.
      const onRowActivate = vi.fn()
      const onButtonClick = vi.fn()
      render(
        <WaydGrid<Flag>
          data={DATA}
          columns={[
            {
              id: 'name',
              accessorKey: 'name',
              header: 'Name',
              cell: ({ row }) => (
                <button onClick={onButtonClick}>{row.original.name}</button>
              ),
            },
          ]}
          onRowActivate={onRowActivate}
        />,
      )

      // Act
      fireEvent.click(
        within(bodyCells('name')[0]).getByRole('button', {
          name: 'planning-poker',
        }),
      )

      // Assert
      expect(onButtonClick).toHaveBeenCalledTimes(1)
      expect(onRowActivate).not.toHaveBeenCalled()
    })

    it('leaves keyboard activation of interactive cell content alone', () => {
      // Arrange — Enter on a focused in-cell button is that button's, and the
      // event bubbles to the row, so the row has to decline it too.
      const onRowActivate = vi.fn()
      render(
        <WaydGrid<Flag>
          data={DATA}
          columns={[
            {
              id: 'name',
              accessorKey: 'name',
              header: 'Name',
              cell: ({ row }) => <button>{row.original.name}</button>,
            },
          ]}
          onRowActivate={onRowActivate}
        />,
      )

      // Act
      fireEvent.keyDown(
        within(bodyCells('name')[1]).getByRole('button', { name: 'roadmap' }),
        { key: 'Enter' },
      )

      // Assert
      expect(onRowActivate).not.toHaveBeenCalled()
    })

    it('respects an explicit data-row-activate="ignore" opt-out', () => {
      // Arrange — for non-semantic interactive content (a custom toggle) that
      // the interactive-element selector would not otherwise catch.
      const onRowActivate = vi.fn()
      render(
        <WaydGrid<Flag>
          data={DATA}
          columns={[
            {
              id: 'name',
              accessorKey: 'name',
              header: 'Name',
              cell: ({ row }) => (
                <span data-row-activate="ignore">{row.original.name}</span>
              ),
            },
          ]}
          onRowActivate={onRowActivate}
        />,
      )

      // Act
      fireEvent.click(screen.getByText('insights'))

      // Assert
      expect(onRowActivate).not.toHaveBeenCalled()
    })

    it('marks the activated row and only that row', () => {
      // Arrange / Act — activatedRowId is matched against the row id, which
      // falls back to row.original.id.
      renderGrid({ onRowActivate: vi.fn(), activatedRowId: '2' })

      // Assert — the highlight is a class on exactly one row
      const activated = document.querySelectorAll(`tbody tr.${'trActivated'}`)
      expect(activated).toHaveLength(1)
      expect(activated[0]).toHaveTextContent('roadmap')
    })

    it('marks no row when activatedRowId matches nothing', () => {
      // Arrange / Act — a stale id from a URL must not highlight a row, and
      // must not throw.
      renderGrid({ onRowActivate: vi.fn(), activatedRowId: '999' })

      // Assert
      expect(document.querySelectorAll('tbody tr.trActivated')).toHaveLength(0)
    })
  })

  describe('row grouping (grouping)', () => {
    const groupRows = () =>
      Array.from(document.querySelectorAll('tbody tr[data-group-row]'))

    it('puts a heading over each distinct value and takes the grouped column out of the rows', () => {
      // Arrange / Act
      renderGrid({ grouping: ['type'] })

      // Assert — one heading per type, in the column's sort order, each
      // saying how many rows it holds
      const headings = groupRows().map((tr) => tr.textContent)
      expect(headings).toEqual(['System1 row', 'User2 rows'])
      expect(bodyCells('name').map((c) => c.textContent)).toEqual([
        'planning-poker',
        'roadmap',
        'insights',
      ])
      // The value is the heading now, so the column has nothing left to say
      expect(bodyCells('type')).toHaveLength(0)
      expect(screen.queryByText('Type')).not.toBeInTheDocument()
    })

    it('collapses and expands a group from its toggle', () => {
      // Arrange
      renderGrid({ grouping: ['type'] })
      const userToggle = within(groupRows()[1] as HTMLElement).getByRole(
        'button',
        { name: 'Collapse group' },
      )

      // Act
      fireEvent.click(userToggle)

      // Assert
      expect(bodyCells('name').map((c) => c.textContent)).toEqual([
        'planning-poker',
      ])
      expect(groupRows()[1]).toHaveTextContent('User2 rows')

      fireEvent.click(
        within(groupRows()[1] as HTMLElement).getByRole('button', {
          name: 'Expand group',
        }),
      )
      expect(bodyCells('name')).toHaveLength(3)
    })

    it('can start with every group collapsed', () => {
      // Arrange / Act
      renderGrid({ grouping: ['type'], initialGroupsExpanded: false })

      // Assert
      expect(groupRows()).toHaveLength(2)
      expect(bodyCells('name')).toHaveLength(0)
    })

    it('sorts within each group and keeps the groups in the grouped order', () => {
      // Arrange / Act
      renderGrid({
        grouping: ['type'],
        initialSorting: [{ id: 'name', desc: true }],
      })

      // Assert — System still first; User's rows now descend by name
      expect(groupRows().map((tr) => tr.textContent)).toEqual([
        'System1 row',
        'User2 rows',
      ])
      expect(bodyCells('name').map((c) => c.textContent)).toEqual([
        'planning-poker',
        'roadmap',
        'insights',
      ])
    })

    it('lets the caller write the heading from the group value and its rows', () => {
      // Arrange
      const renderGroupHeader = vi.fn(
        ({ value, leafRows }: { value: unknown; leafRows: Flag[] }) =>
          `${String(value)}: ${leafRows.map((f) => f.name).join(', ')}`,
      )

      // Act
      renderGrid({ grouping: ['type'], renderGroupHeader })

      // Assert
      expect(groupRows()[1]).toHaveTextContent('User: roadmap, insights')
      expect(renderGroupHeader).toHaveBeenCalledWith(
        expect.objectContaining({ columnId: 'type', value: 'User', depth: 0 }),
      )
    })

    it('activates data rows but never a heading', () => {
      // Arrange
      const onRowActivate = vi.fn()
      renderGrid({ grouping: ['type'], onRowActivate })

      // Act
      fireEvent.click(bodyCells('name')[1])
      fireEvent.click(groupRows()[0])

      // Assert
      expect(onRowActivate).toHaveBeenCalledTimes(1)
      expect(onRowActivate).toHaveBeenCalledWith(DATA[1])
      expect(groupRows()[0]).not.toHaveAttribute('role', 'button')
    })

    it('keeps headings out of the displayed rows and the CSV', () => {
      // Arrange
      mockDownloadCsv.mockClear()
      const onDisplayedRowsChange = vi.fn()
      const { container } = renderGrid({
        grouping: ['type'],
        onDisplayedRowsChange,
      })

      // Act
      const exportBtn = container
        .querySelector('[aria-label="download"]')
        ?.closest('button') as HTMLButtonElement
      fireEvent.click(exportBtn)

      // Assert — three data rows, no heading rows, in either surface; the
      // grouped column leads the CSV so each row keeps the value its heading
      // carried on screen
      const last = onDisplayedRowsChange.mock.calls.at(-1)![0] as Flag[]
      expect(last).toHaveLength(3)
      const csv = mockDownloadCsv.mock.calls[0][0] as string
      const lines = csv.split('\n')
      expect(lines).toHaveLength(DATA.length + 1)
      expect(lines[0]).toBe('Type,Name,Enabled')
      expect(lines[1]).toContain('System')
    })

    it('drops a group whose rows are all filtered out', () => {
      // Arrange
      renderGrid({ grouping: ['type'] })

      // Act — quick search for a name only User rows have
      fireEvent.change(screen.getByPlaceholderText(/search/i), {
        target: { value: 'roadmap' },
      })

      // Assert
      expect(groupRows().map((tr) => tr.textContent)).toEqual(['User1 row'])
    })
  })
  describe('keyboard navigation', () => {
    const renderPlain = (props: GridProps = {}) =>
      renderGrid({ includeIdColumn: false, ...props })

    /** Body cells holding the grid's Tab stop. */
    const tabStops = () =>
      Array.from(document.querySelectorAll('tbody td[tabindex="0"]'))

    const focus = (el: HTMLElement) => act(() => el.focus())

    it("puts the grid's single Tab stop on the first cell", () => {
      // Arrange / Act
      renderPlain()

      // Assert — every other cell is reachable by arrows but skipped by Tab
      expect(tabStops()).toEqual([bodyCells('name')[0]])
      expect(bodyCells('type')[0]).toHaveAttribute('tabindex', '-1')
    })

    it('moves focus and the Tab stop with the arrow keys', () => {
      // Arrange
      renderPlain()
      focus(bodyCells('name')[0])

      // Act
      fireEvent.keyDown(document.activeElement!, { key: 'ArrowDown' })
      fireEvent.keyDown(document.activeElement!, { key: 'ArrowRight' })

      // Assert
      expect(document.activeElement).toBe(bodyCells('type')[1])
      expect(tabStops()).toEqual([bodyCells('type')[1]])
    })

    it('moves to the row ends with Home/End and the grid corners with Ctrl', () => {
      // Arrange
      renderPlain()
      focus(bodyCells('type')[1])

      // Act / Assert
      fireEvent.keyDown(document.activeElement!, { key: 'End' })
      expect(document.activeElement).toBe(bodyCells('isEnabled')[1])

      fireEvent.keyDown(document.activeElement!, { key: 'Home' })
      expect(document.activeElement).toBe(bodyCells('name')[1])

      fireEvent.keyDown(document.activeElement!, { key: 'End', ctrlKey: true })
      expect(document.activeElement).toBe(bodyCells('isEnabled')[2])

      fireEvent.keyDown(document.activeElement!, { key: 'Home', ctrlKey: true })
      expect(document.activeElement).toBe(bodyCells('name')[0])
    })

    it('walks columns in on-screen order once a column is pinned', () => {
      // Arrange — pinning moves Enabled to the front of every row
      const ref = createRef<WaydGridHandle>()
      renderPlain({ ref })
      act(() => {
        ref.current!.table.getColumn('isEnabled').pin('start')
      })
      focus(bodyCells('type')[0])

      // Act
      fireEvent.keyDown(document.activeElement!, { key: 'Home' })
      const afterHome = document.activeElement
      fireEvent.keyDown(document.activeElement!, { key: 'ArrowRight' })

      // Assert
      expect(afterHome).toBe(bodyCells('isEnabled')[0])
      expect(document.activeElement).toBe(bodyCells('name')[0])
    })

    it('ignores navigation keys pressed in a control inside a cell', () => {
      // Arrange
      render(
        <WaydGrid<Flag>
          data={DATA}
          includeIdColumn={false}
          columns={[
            {
              id: 'name',
              accessorKey: 'name',
              header: 'Name',
              cell: ({ row }) => <input aria-label={row.original.name} />,
            },
          ]}
        />,
      )
      const input = screen.getByRole('textbox', { name: 'roadmap' })
      focus(input)

      // Act — the caret is the input's, not the grid's
      fireEvent.keyDown(input, { key: 'ArrowDown' })

      // Assert
      expect(document.activeElement).toBe(input)
    })

    it("moves into a cell's control with Enter or F2 and back out with Escape", () => {
      // Arrange
      render(
        <WaydGrid<Flag>
          data={DATA}
          includeIdColumn={false}
          columns={[
            {
              id: 'name',
              accessorKey: 'name',
              header: 'Name',
              cell: ({ row }) => (
                <button tabIndex={-1}>{row.original.name}</button>
              ),
            },
          ]}
        />,
      )
      const cell = bodyCells('name')[1]
      const button = within(cell).getByRole('button', { name: 'roadmap' })
      focus(cell)

      // Act / Assert
      fireEvent.keyDown(cell, { key: 'Enter' })
      expect(document.activeElement).toBe(button)

      fireEvent.keyDown(button, { key: 'Escape' })
      expect(document.activeElement).toBe(cell)

      fireEvent.keyDown(cell, { key: 'F2' })
      expect(document.activeElement).toBe(button)
    })

    it("opens the row's actions menu with Shift+F10 or the Menu key", () => {
      // Arrange
      const onActions = vi.fn()
      render(
        <WaydGrid<Flag>
          data={DATA}
          includeIdColumn={false}
          columns={[
            {
              id: 'actions',
              header: '',
              cell: ({ row }) => (
                <button
                  data-row-actions=""
                  tabIndex={-1}
                  onClick={() => onActions(row.original.id)}
                >
                  …
                </button>
              ),
            },
            { id: 'name', accessorKey: 'name', header: 'Name' },
          ]}
        />,
      )
      focus(bodyCells('name')[1])

      // Act
      fireEvent.keyDown(bodyCells('name')[1], { key: 'F10', shiftKey: true })
      fireEvent.keyDown(bodyCells('name')[2], { key: 'ContextMenu' })
      fireEvent.keyDown(bodyCells('name')[0], { key: 'F10' })

      // Assert — plain F10 is the browser's, not the row's
      expect(onActions.mock.calls).toEqual([[2], [3]])
    })

    it('leaves the grid on Tab from a control inside a cell', () => {
      // Arrange — the cell's own button, reached with Enter
      render(
        <WaydGrid<Flag>
          data={DATA}
          includeIdColumn={false}
          columns={[
            {
              id: 'name',
              accessorKey: 'name',
              header: 'Name',
              cell: ({ row }) => <button>{row.original.name}</button>,
            },
          ]}
        />,
      )
      const button = screen.getByRole('button', { name: 'roadmap' })
      focus(button)

      // Act
      fireEvent.keyDown(button, { key: 'Tab' })

      // Assert — not on to the next row's button
      expect(document.activeElement?.previousElementSibling?.tagName).toBe(
        'TABLE',
      )
    })

    it('lands on the active cell when Tab enters the grid onto a control inside a cell', () => {
      // Arrange — a cell control left in the tab order (a link, a drag
      // handle) would otherwise be where Shift+Tab into the grid stops
      render(
        <WaydGrid<Flag>
          data={DATA}
          includeIdColumn={false}
          columns={[
            {
              id: 'name',
              accessorKey: 'name',
              header: 'Name',
              cell: ({ row }) => <button>{row.original.name}</button>,
            },
          ]}
        />,
      )
      fireEvent.keyDown(document.body, { key: 'Tab', shiftKey: true })

      // Act
      focus(screen.getByRole('button', { name: 'insights' }))

      // Assert
      expect(document.activeElement).toBe(bodyCells('name')[0])
    })

    it('keeps the Tab stop at the same position when the focused row goes away', () => {
      // Arrange
      const { rerender } = renderPlain()
      focus(bodyCells('name')[2])

      // Act — e.g. a cancelled draft or a deleted row
      rerender(
        <WaydGrid<Flag>
          data={DATA.slice(0, 2)}
          columns={columns}
          includeIdColumn={false}
        />,
      )

      // Assert — the neighbouring row, not a jump back to the top
      expect(tabStops()).toEqual([bodyCells('name')[1]])
    })

    it('keeps Space from scrolling the grid', () => {
      // Arrange
      renderPlain()

      // Act
      const notPrevented = fireEvent.keyDown(bodyCells('name')[0], {
        key: ' ',
      })

      // Assert
      expect(notPrevented).toBe(false)
    })

    it('suppresses the browser menu that follows the Menu key, but not a right-click', () => {
      // Arrange
      render(
        <WaydGrid<Flag>
          data={DATA}
          includeIdColumn={false}
          columns={[
            { id: 'name', accessorKey: 'name', header: 'Name' },
            createActionsColumn<Flag>({
              getItems: () => [{ key: 'edit', label: 'Edit' }],
            }),
          ]}
        />,
      )
      const cell = bodyCells('name')[0]

      // Act
      fireEvent.keyDown(cell, { key: 'ContextMenu' })
      const menuKeyMenuShown = fireEvent.contextMenu(cell)
      const rightClickMenuShown = fireEvent.contextMenu(cell)

      // Assert
      expect(menuKeyMenuShown).toBe(false)
      expect(rightClickMenuShown).toBe(true)
    })

    it('moves Enter to a cell link first, and never to an expand toggle', () => {
      // Arrange — the link comes after two buttons in the Name cell; the
      // Type cell has no link, only a toggle and a button
      render(
        <WaydGrid<Flag>
          data={DATA}
          includeIdColumn={false}
          columns={[
            {
              id: 'name',
              accessorKey: 'name',
              header: 'Name',
              cell: ({ row }) => (
                <>
                  <button tabIndex={-1} aria-expanded="false">
                    toggle
                  </button>
                  <button tabIndex={-1}>copy</button>
                  <a href={`/flags/${row.original.id}`}>{row.original.name}</a>
                </>
              ),
            },
            {
              id: 'type',
              accessorKey: 'type',
              header: 'Type',
              cell: ({ row }) => (
                <>
                  <button tabIndex={-1} aria-expanded="false">
                    toggle
                  </button>
                  <button tabIndex={-1}>{row.original.type}</button>
                </>
              ),
            },
          ]}
        />,
      )
      const nameCell = bodyCells('name')[0]
      const typeCell = bodyCells('type')[0]

      // Act / Assert
      focus(nameCell)
      fireEvent.keyDown(nameCell, { key: 'Enter' })
      expect(document.activeElement).toBe(
        within(nameCell).getByRole('link', { name: 'planning-poker' }),
      )

      focus(typeCell)
      fireEvent.keyDown(typeCell, { key: 'Enter' })
      expect(document.activeElement).toBe(
        within(typeCell).getByRole('button', { name: 'System' }),
      )
    })

    it('leaves the grid on Tab and Shift+Tab instead of stepping through cells', () => {
      // Arrange
      renderPlain()
      const cell = bodyCells('type')[1]
      focus(cell)

      // Act / Assert — focus lands on the sentinel past the body, where the
      // browser's own Tab step continues to the next control on the page
      fireEvent.keyDown(cell, { key: 'Tab' })
      expect(document.activeElement?.previousElementSibling?.tagName).toBe(
        'TABLE',
      )

      focus(cell)
      fireEvent.keyDown(cell, { key: 'Tab', shiftKey: true })
      expect(document.activeElement?.nextElementSibling?.tagName).toBe('TABLE')
    })

    describe('with more rows than the viewport', () => {
      const BIG_DATA: Flag[] = Array.from({ length: 200 }, (_, i) => ({
        id: i + 1,
        name: `flag-${String(i + 1).padStart(3, '0')}`,
        isEnabled: true,
        type: 'User',
      }))

      const scrollBodyTo = (rowIndex: number) => {
        const viewport = document.querySelector(
          '[data-grid-body-viewport]',
        ) as HTMLElement
        viewport.scrollTop = rowIndex * 28
        fireEvent.scroll(viewport)
      }

      const nameCell = (name: string) =>
        bodyCells('name').find((c) => c.textContent === name)

      it('moves a viewport of rows with PageDown and PageUp', () => {
        // Arrange — the 600px test viewport holds 21 rows; a page keeps one
        // row of context
        renderPlain({ data: BIG_DATA })
        focus(bodyCells('name')[0])

        // Act / Assert
        fireEvent.keyDown(document.activeElement!, { key: 'PageDown' })
        expect(document.activeElement).toBe(nameCell('flag-021'))

        fireEvent.keyDown(document.activeElement!, { key: 'PageUp' })
        expect(document.activeElement).toBe(nameCell('flag-001'))
      })

      it('keeps the focused row rendered and focused when it scrolls out of view', () => {
        // Arrange
        renderPlain({ data: BIG_DATA })
        focus(bodyCells('name')[0])

        // Act
        act(() => scrollBodyTo(100))

        // Assert — the window moved, the focused row stayed
        expect(nameCell('flag-101')).toBeInTheDocument()
        expect(document.activeElement).toBe(nameCell('flag-001'))
      })

      it('keeps each row the same DOM node as the window shifts', () => {
        // Arrange — a remounted row would drop focus and redo its work on
        // every scroll
        renderPlain({ data: BIG_DATA })
        const before = nameCell('flag-025')

        // Act — the window now starts ten rows further down
        act(() => scrollBodyTo(20))

        // Assert
        expect(nameCell('flag-001')).toBeUndefined()
        expect(nameCell('flag-025')).toBe(before)
      })

      it('focuses a row outside the window when the keys move to it', () => {
        // Arrange
        renderPlain({ data: BIG_DATA })
        focus(bodyCells('name')[0])

        // Act
        fireEvent.keyDown(document.activeElement!, {
          key: 'End',
          ctrlKey: true,
        })

        // Assert
        expect(document.activeElement).toBe(
          bodyCells('isEnabled').find((c) =>
            c.closest('tr')?.textContent?.startsWith('flag-200'),
          ),
        )
      })

      it('hands the Tab stop to a rendered row once the active row scrolls away unfocused', () => {
        // Arrange
        renderPlain({ data: BIG_DATA })

        // Act
        act(() => scrollBodyTo(100))

        // Assert — Tab into the grid must land on a body cell in view, not
        // one in the overscan above it
        expect(nameCell('flag-001')).toBeUndefined()
        expect(tabStops()).toHaveLength(1)
        expect(tabStops()[0].closest('tr')).toHaveTextContent('flag-101')
      })
    })

    describe('tree rows', () => {
      interface Node {
        id: string
        name: string
        children: Node[]
      }

      const TREE: Node[] = [
        {
          id: '1',
          name: 'Plan',
          children: [{ id: '1.1', name: 'Kickoff', children: [] }],
        },
        { id: '2', name: 'Execute', children: [] },
      ]

      const renderTree = () =>
        render(
          <WaydGrid<Node>
            data={TREE}
            includeIdColumn={false}
            columns={[
              { id: 'name', accessorKey: 'name', header: 'Name' },
              { id: 'id', accessorKey: 'id', header: 'Ref' },
            ]}
            getSubRows={(row) => row.children}
          />,
        )

      const cellFor = (name: string) =>
        bodyCells('name').find((c) => c.textContent === name)

      it('collapses with Left and expands with Right in the first column', () => {
        // Arrange
        renderTree()
        focus(cellFor('Plan')!)

        // Act / Assert
        fireEvent.keyDown(cellFor('Plan')!, { key: 'ArrowLeft' })
        expect(cellFor('Kickoff')).toBeUndefined()

        fireEvent.keyDown(cellFor('Plan')!, { key: 'ArrowRight' })
        expect(cellFor('Kickoff')).toBeInTheDocument()
        // An expanded row moves on, as any other cell does
        fireEvent.keyDown(cellFor('Plan')!, { key: 'ArrowRight' })
        expect(document.activeElement).toBe(bodyCells('id')[0])
      })

      it('expands and collapses from the column marked treeColumn, wherever it sits', () => {
        // Arrange — the caret column is second; the first column's arrows
        // only move
        render(
          <WaydGrid<Node>
            data={TREE}
            includeIdColumn={false}
            columns={[
              { id: 'id', accessorKey: 'id', header: 'Ref' },
              {
                id: 'name',
                accessorKey: 'name',
                header: 'Name',
                meta: { treeColumn: true } satisfies WaydGridColumnMeta,
              },
            ]}
            getSubRows={(row) => row.children}
          />,
        )
        const planRef = bodyCells('id')[0]
        focus(planRef)

        // Act / Assert
        fireEvent.keyDown(planRef, { key: 'ArrowLeft' })
        expect(cellFor('Kickoff')).toBeInTheDocument()

        fireEvent.keyDown(cellFor('Plan')!, { key: 'ArrowLeft' })
        expect(cellFor('Kickoff')).toBeUndefined()
      })

      it('moves Left from a child to its parent', () => {
        // Arrange
        renderTree()
        focus(cellFor('Kickoff')!)

        // Act
        fireEvent.keyDown(cellFor('Kickoff')!, { key: 'ArrowLeft' })

        // Assert
        expect(document.activeElement).toBe(cellFor('Plan'))
      })

      it('announces the tree structure on each row', () => {
        // Arrange / Act
        renderTree()

        // Assert
        expect(screen.getByRole('treegrid')).toBeInTheDocument()
        const plan = cellFor('Plan')!.closest('tr')!
        const kickoff = cellFor('Kickoff')!.closest('tr')!
        expect(plan).toHaveAttribute('aria-level', '1')
        expect(plan).toHaveAttribute('aria-expanded', 'true')
        expect(kickoff).toHaveAttribute('aria-level', '2')
        expect(kickoff).not.toHaveAttribute('aria-expanded')
      })
    })

    it('toggles a group heading with Enter and the arrow keys', () => {
      // Arrange
      renderPlain({ grouping: ['type'] })
      const heading = () => screen.getByRole('gridcell', { name: /User/ })
      focus(heading())

      // Act / Assert — the User group holds two rows
      fireEvent.keyDown(heading(), { key: 'Enter' })
      expect(bodyCells('name')).toHaveLength(1)

      fireEvent.keyDown(heading(), { key: 'ArrowRight' })
      expect(bodyCells('name')).toHaveLength(3)

      fireEvent.keyDown(heading(), { key: 'ArrowLeft' })
      expect(bodyCells('name')).toHaveLength(1)
    })

    it('sorts a column from its heading with Enter, announced as a column header', () => {
      // Arrange
      renderPlain()
      const heading = screen.getByRole('columnheader', { name: /Name/ })

      // Act
      fireEvent.keyDown(heading, { key: 'Enter' })

      // Assert — dnd-kit's "draggable" description is not what is read
      expect(heading).toHaveAttribute('aria-sort', 'ascending')
      expect(heading).not.toHaveAttribute('aria-roledescription')
      expect(heading).toHaveAttribute('tabindex', '0')
    })

    it('places each header band at the first column it covers', () => {
      // Arrange / Act
      renderPlain({
        columns: [
          {
            id: 'details',
            header: 'Details',
            columns: [
              { id: 'name', accessorKey: 'name', header: 'Name' },
              { id: 'type', accessorKey: 'type', header: 'Type' },
            ],
          },
          {
            id: 'state',
            header: 'State',
            columns: [
              { id: 'isEnabled', accessorKey: 'isEnabled', header: 'Enabled' },
            ],
          },
        ],
      })

      // Assert — the second band starts at column 3, not 2
      expect(
        screen.getByRole('columnheader', { name: 'State' }),
      ).toHaveAttribute('aria-colindex', '3')
    })

    it('renders the empty message as a row of the grid', () => {
      // Arrange / Act
      renderPlain({ data: [], emptyMessage: 'Nothing here' })

      // Assert — a grid may own only rows
      const grid = screen.getByRole('grid')
      expect(
        within(grid).getByText('Nothing here').closest('[role="gridcell"]')
          ?.parentElement,
      ).toHaveAttribute('role', 'row')
    })

    it('exposes the grid, its headers and each row position to assistive tech', () => {
      // Arrange / Act — the advanced variant shows a floating filter row, so
      // two header rows sit above the body
      renderPlain({
        'aria-label': 'Feature flags',
        initialSorting: [{ id: 'name', desc: false }],
      })

      // Assert
      const grid = screen.getByRole('grid', { name: 'Feature flags' })
      expect(grid).toHaveAttribute('aria-rowcount', String(2 + DATA.length))
      expect(grid).toHaveAttribute('aria-colcount', '3')
      expect(
        screen.getByRole('columnheader', { name: /Name/ }),
      ).toHaveAttribute('aria-sort', 'ascending')
      const firstRow = bodyCells('name')[0].closest('tr')
      expect(firstRow).toHaveAttribute('aria-rowindex', '3')
      expect(bodyCells('type')[0]).toHaveAttribute('aria-colindex', '2')
    })
  })
})
