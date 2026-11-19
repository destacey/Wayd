import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ColumnDef } from '../wayd-grid-core'
import WaydGrid from './wayd-grid'
import type { WaydGridColumnMeta, WaydGridServerSource } from './types'
import type { ServerPageRequest } from '../wayd-grid-core/server/server-page-cache'
import { downloadCsvWithTimestamp } from '@/src/utils/csv-utils'

vi.mock(import('@/src/utils/csv-utils'), async (importOriginal) => ({
  ...(await importOriginal()),
  downloadCsvWithTimestamp: vi.fn(),
}))

interface Item {
  id: string
  name: string
  stage: string
  note: string
}

const columns: ColumnDef<Item, unknown>[] = [
  { id: 'name', accessorKey: 'name', header: 'Name' },
  {
    id: 'stage',
    accessorKey: 'stage',
    header: 'Stage',
    meta: {
      filterType: 'set',
      filterOptions: [
        { value: 'Draft', label: 'Draft' },
        { value: 'Active', label: 'Active' },
      ],
    } satisfies WaydGridColumnMeta,
  },
  { id: 'note', accessorKey: 'note', header: 'Note' },
]

const TOTAL = 1000

/** A server of {@link TOTAL} rows; only page 1 is answered unless `pages` says otherwise. */
const serverOf = (
  answered: (page: number) => boolean = (page) => page === 1,
) => {
  const fetchPage = vi.fn(
    ({ page, pageSize }: ServerPageRequest) =>
      new Promise<{ items: Item[]; totalCount: number }>((resolve) => {
        if (!answered(page)) return
        resolve({
          items: Array.from({ length: pageSize }, (_, i) => {
            const n = (page - 1) * pageSize + i + 1
            return {
              id: `item-${n}`,
              name: `Item ${n}`,
              stage: 'Draft',
              note: '',
            }
          }),
          totalCount: TOTAL,
        })
      }),
  )
  return fetchPage
}

const renderServerGrid = (
  source: Partial<WaydGridServerSource<Item>> & {
    fetchPage: WaydGridServerSource<Item>['fetchPage']
  },
) =>
  render(
    <WaydGrid<Item>
      aria-label="Items"
      columns={columns}
      server={{ fields: ['name', 'stage'], pageSize: 5, ...source }}
    />,
  )

const settle = () => act(async () => {})

const bodyCells = (columnId: string) =>
  Array.from(
    document.querySelectorAll(`tbody td[data-column-id="${columnId}"]`),
  ) as HTMLTableCellElement[]

/** The `sort` and `filter` of each request so far. */
const sent = (fetchPage: ReturnType<typeof serverOf>) =>
  fetchPage.mock.calls.map(([request]) => ({
    page: request.page,
    sort: request.sort,
    filter: request.filter,
  }))

describe('WaydGrid — server mode', () => {
  it('counts the server’s total, as rows of the grid and in the toolbar', async () => {
    // Arrange / Act
    renderServerGrid({ fetchPage: serverOf(), noun: 'items' })
    await settle()

    // Assert — two header rows above the body (leaf headers, floating filters)
    expect(screen.getByRole('grid', { name: 'Items' })).toHaveAttribute(
      'aria-rowcount',
      String(2 + TOTAL),
    )
    expect(screen.getByText('1,000 items')).toBeInTheDocument()
  })

  it('shows fetched rows and stands placeholders in for the rest', async () => {
    // Arrange / Act — page 2 never arrives
    renderServerGrid({ fetchPage: serverOf() })
    await settle()

    // Assert
    const names = bodyCells('name')
    expect(names[0]).toHaveTextContent('Item 1')
    expect(names[4]).toHaveTextContent('Item 5')
    const placeholder = names[5].closest('tr')!
    expect(placeholder).toHaveAttribute('data-grid-placeholder')
    expect(placeholder).toHaveAttribute('aria-busy', 'true')
    expect(placeholder).toHaveAttribute('aria-rowindex', String(2 + 6))
  })

  it('fetches the pages under the rendered rows', async () => {
    // Arrange / Act
    const fetchPage = serverOf(() => true)
    renderServerGrid({ fetchPage })
    await settle()

    // Assert — jsdom renders the overscan rows, which span pages 1 to 3
    expect(sent(fetchPage).map((r) => r.page)).toEqual(
      expect.arrayContaining([1, 2, 3]),
    )
    expect(bodyCells('name')[7]).toHaveTextContent('Item 8')
  })

  it('sends a sort to the server and starts the list over', async () => {
    // Arrange
    const fetchPage = serverOf()
    renderServerGrid({ fetchPage })
    await settle()
    fetchPage.mockClear()

    // Act
    fireEvent.keyDown(screen.getByRole('columnheader', { name: /Name/ }), {
      key: 'Enter',
    })
    await settle()

    // Assert
    expect(sent(fetchPage)[0]).toMatchObject({ page: 1, sort: 'name asc' })
  })

  it('turns off sorting and filtering on a column the server does not map', async () => {
    // Arrange / Act
    renderServerGrid({ fetchPage: serverOf() })
    await settle()

    // Assert
    expect(
      screen.getByRole('columnheader', { name: /Note/ }),
    ).not.toHaveAttribute('aria-sort')
    fireEvent.keyDown(screen.getByRole('columnheader', { name: /Note/ }), {
      key: 'Enter',
    })
    expect(
      screen.getByRole('columnheader', { name: /Note/ }),
    ).not.toHaveAttribute('aria-sort', 'ascending')
  })

  it('sends a typed filter once the typing pauses', async () => {
    // Arrange
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      const fetchPage = serverOf()
      renderServerGrid({ fetchPage })
      await settle()
      fetchPage.mockClear()
      const input = document.querySelector(
        'tr[data-role="floating-filters"] input',
      ) as HTMLInputElement

      // Act
      fireEvent.change(input, { target: { value: 'it' } })
      fireEvent.change(input, { target: { value: 'item 4' } })
      // The input's own debounce commits the filter; the grid's then sends it.
      await act(async () => {
        vi.advanceTimersByTime(1000)
      })
      await act(async () => {
        vi.advanceTimersByTime(1000)
      })
      await settle()

      // Assert — the finished value only, from the top
      expect(sent(fetchPage)[0]).toMatchObject({
        page: 1,
        filter: 'name=*item 4',
      })
      expect(sent(fetchPage).every((r) => r.filter === 'name=*item 4')).toBe(
        true,
      )
    } finally {
      vi.useRealTimers()
    }
  })

  it('offers no quick search unless the list takes one', async () => {
    // Arrange / Act
    const { unmount } = renderServerGrid({ fetchPage: serverOf() })
    await settle()

    // Assert
    expect(screen.queryByPlaceholderText('Search')).toBeNull()
    unmount()

    renderServerGrid({ fetchPage: serverOf(), search: true })
    await settle()
    expect(screen.getByPlaceholderText('Search')).toBeInTheDocument()
  })

  it('exports the whole list under its filter, not only the rows in view', async () => {
    // Arrange — 1,000 rows on the server, a window of 5 in view
    const fetchPage = serverOf(() => true)
    renderServerGrid({ fetchPage, exportLimit: 12 })
    await settle()
    fireEvent.change(
      document.querySelector('tr[data-role="floating-filters"] input')!,
      { target: { value: 'Item' } },
    )
    await waitFor(() =>
      expect(fetchPage.mock.calls.at(-1)?.[0].filter).toBe('name=*Item'),
    )
    fetchPage.mockClear()

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Export to CSV' }))

    // Assert
    await waitFor(() => expect(downloadCsvWithTimestamp).toHaveBeenCalled())
    const csv = vi.mocked(downloadCsvWithTimestamp).mock.calls.at(-1)![0]
    const lines = csv.split('\n')
    expect(lines[0]).toBe('Name,Stage,Note')
    expect(lines).toHaveLength(13)
    expect(lines[12]).toBe('Item 12,Draft,')
    expect(fetchPage.mock.calls.map(([r]) => r)).toEqual([
      expect.objectContaining({ page: 1, pageSize: 500, filter: 'name=*Item' }),
    ])
  })

  it('cancels an export when its button is pressed again', async () => {
    // Arrange — page 1 answers the view; the export's read never does
    let viewAnswered = false
    const fetchPage = vi.fn(({ pageSize, signal }: ServerPageRequest) => {
      if (!viewAnswered) {
        viewAnswered = true
        return Promise.resolve({
          items: Array.from({ length: pageSize }, (_, i) => ({
            id: `item-${i + 1}`,
            name: `Item ${i + 1}`,
            stage: 'Draft',
            note: '',
          })),
          totalCount: TOTAL,
        })
      }
      return new Promise<never>((_, reject) =>
        signal.addEventListener('abort', () => reject(signal.reason)),
      )
    })
    renderServerGrid({ fetchPage })
    await settle()
    vi.mocked(downloadCsvWithTimestamp).mockClear()
    await userEvent.click(screen.getByRole('button', { name: 'Export to CSV' }))
    const exportRead = fetchPage.mock.calls.at(-1)![0]

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Cancel export' }))

    // Assert
    expect(exportRead.signal.aborted).toBe(true)
    expect(
      await screen.findByRole('button', { name: 'Export to CSV' }),
    ).toBeInTheDocument()
    expect(downloadCsvWithTimestamp).not.toHaveBeenCalled()
  })

  it('starts over when the list key changes, and not when only the fetcher does', async () => {
    // Arrange
    const fetchPage = serverOf()
    const { rerender } = renderServerGrid({ fetchPage, listKey: 'a' })
    await settle()
    fetchPage.mockClear()
    const grid = (listKey: string) => (
      <WaydGrid<Item>
        columns={columns}
        server={{
          fields: ['name'],
          pageSize: 5,
          listKey,
          fetchPage: (request) => fetchPage(request),
        }}
      />
    )

    // Act
    rerender(grid('a'))
    await settle()
    const afterSameKey = fetchPage.mock.calls.length
    rerender(grid('b'))
    await settle()

    // Assert
    expect(afterSameKey).toBe(0)
    expect(sent(fetchPage)[0]).toMatchObject({ page: 1 })
  })

  it('reads a column’s filter type from its declaration, never from the loaded rows', async () => {
    // Arrange — the rows hold numbers; inferring from them would flip the
    // filter to text whenever a new query empties the window
    type Scored = { id: string; score: number; rank: number }
    const fetchPage = () =>
      Promise.resolve({
        items: [{ id: 'a', score: 1, rank: 2 }] as Scored[],
        totalCount: 1,
      })

    // Act
    render(
      <WaydGrid<Scored>
        columns={[
          { id: 'score', accessorKey: 'score', header: 'Score' },
          {
            id: 'rank',
            accessorKey: 'rank',
            header: 'Rank',
            meta: { filterType: 'number' },
          },
        ]}
        server={{ fields: ['score', 'rank'], fetchPage }}
      />,
    )
    await settle()

    // Assert
    const filters = document.querySelectorAll(
      'tr[data-role="floating-filters"] input',
    )
    expect(filters[0]).not.toHaveAttribute('role', 'spinbutton')
    expect(filters[1]).toHaveAttribute('role', 'spinbutton')
  })

  it('filters by a value its set filter loads, and names it in the header', async () => {
    // Arrange
    type Owned = { id: string; owner: string }
    const fetchPage = vi.fn((_request: ServerPageRequest) =>
      Promise.resolve({
        items: [{ id: 'a', owner: 'Ada' }] as Owned[],
        totalCount: 1,
      }),
    )
    const load = vi.fn(async () => ({
      options: [
        { value: 'id-ada', label: 'Ada Lovelace' },
        { value: 'id-grace', label: 'Grace Hopper' },
      ],
      hasMore: false,
    }))
    render(
      <WaydGrid<Owned>
        columns={[
          {
            id: 'owner',
            accessorKey: 'owner',
            header: 'Owner',
            meta: { filterType: 'set', filterOptionsSource: { load } },
          },
        ]}
        server={{ fields: ['owner'], fetchPage }}
      />,
    )
    await settle()
    expect(load).not.toHaveBeenCalled()

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Filter column' }))
    const option = await screen.findByText('Grace Hopper')
    await userEvent.click(
      within(option.closest('label')!).getByRole('checkbox'),
    )

    // Assert
    await waitFor(() =>
      expect(fetchPage.mock.calls.at(-1)?.[0]).toMatchObject({
        filter: 'owner=id\\-grace',
      }),
    )
    expect(
      screen.getByRole('button', { name: 'Filter column (active)' }),
    ).toHaveTextContent('Grace Hopper')
  })

  it('shows the server’s reasons when it refuses the filter', async () => {
    // Arrange / Act
    renderServerGrid({
      fetchPage: () =>
        Promise.reject({
          kind: 'api-response',
          status: 422,
          errors: { Filter: ["'filter' could not be read."] },
          url: '/api',
          headers: {},
        }),
    })
    await settle()

    // Assert
    expect(
      screen.getByText(
        "The list cannot be filtered or sorted that way: 'filter' could not be read. Change the filter or sort to try again.",
      ),
    ).toBeInTheDocument()
  })

  it('says so when the list could not be loaded', async () => {
    // Arrange / Act
    renderServerGrid({
      fetchPage: () => Promise.reject(new Error('down')),
    })
    await settle()

    // Assert
    expect(
      screen.getByText('The list could not be loaded. Refresh to try again.'),
    ).toBeInTheDocument()
  })
})
