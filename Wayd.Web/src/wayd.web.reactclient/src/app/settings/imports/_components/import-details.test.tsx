import { act, render, screen } from '@testing-library/react'
import {
  ImportAtomicity,
  ImportProcessDto,
  ImportProcessStatus,
  ImportRowStatus,
} from '@/src/services/wayd-api'
import ImportDetails from './import-details'

const mockGetRows = vi.fn()

vi.mock(import('@/src/services/clients'), async (importOriginal) => ({
  ...(await importOriginal()),
  getImportsClient: () =>
    ({
      getRows: (...args: unknown[]) => mockGetRows(...args),
    }) as never,
}))

const run = (overrides: Partial<ImportProcessDto> = {}): ImportProcessDto => ({
  id: 'run-1',
  importType: 'organization.employees',
  displayName: 'Employees',
  atomicity: ImportAtomicity.PerRow,
  status: ImportProcessStatus.Processing,
  isPreflight: false,
  submittedByUserId: 'user-1',
  submittedOn: new Date('2026-10-01T09:00:00Z'),
  totalRowCount: 400,
  succeededRowCount: 120,
  failedRowCount: 0,
  canManage: true,
  unappliedRowCount: 280,
  isTerminal: false,
  ...overrides,
})

/** The status, page and filter of each rows request so far. */
const requests = () =>
  mockGetRows.mock.calls.map(([, status, page, , filter, sort]) => ({
    status,
    page,
    filter,
    sort,
  }))

/** Lets the pending fetches settle and the grid render what they returned. */
const settle = () => act(async () => {})

describe('ImportDetails', () => {
  beforeEach(() => {
    mockGetRows.mockReset()
    mockGetRows.mockImplementation(
      async (
        _id: string,
        _status: unknown,
        page: number,
        pageSize: number,
      ) => ({
        items: Array.from({ length: pageSize }, (_, i) => ({
          id: `row-${page}-${i}`,
          importId: `r${(page - 1) * pageSize + i + 1}`,
          rowNumber: (page - 1) * pageSize + i + 1,
          status: ImportRowStatus.Succeeded,
        })),
        totalCount: 400,
      }),
    )
  })

  it('reads every row from the server, a page at a time, with the total in the toolbar', async () => {
    // Act
    render(<ImportDetails importProcess={run()} />)
    await settle()

    // Assert
    expect(requests()[0]).toMatchObject({ status: undefined, page: 1 })
    expect(screen.getByText('400 rows')).toBeInTheDocument()
    expect(screen.getByText('r1')).toBeInTheDocument()
  })

  it('starts over on the rejected rows when the run rejects its first row', async () => {
    // Arrange
    const { rerender } = render(<ImportDetails importProcess={run()} />)
    await settle()
    mockGetRows.mockClear()

    // Act
    rerender(<ImportDetails importProcess={run({ failedRowCount: 1 })} />)
    await settle()

    // Assert
    expect(requests()[0]).toMatchObject({
      status: ImportRowStatus.Failed,
      page: 1,
    })
  })

  it('does not start over while the filter in effect stays the same', async () => {
    // Arrange
    const { rerender } = render(<ImportDetails importProcess={run()} />)
    await settle()
    mockGetRows.mockClear()

    // Act — progress moves on, but nothing was rejected
    rerender(<ImportDetails importProcess={run({ succeededRowCount: 180 })} />)
    await settle()

    // Assert
    expect(mockGetRows).not.toHaveBeenCalled()
  })

  it('reads the rows again when the run finishes', async () => {
    // Arrange
    const { rerender } = render(<ImportDetails importProcess={run()} />)
    await settle()
    mockGetRows.mockClear()

    // Act
    rerender(
      <ImportDetails
        importProcess={run({
          status: ImportProcessStatus.Succeeded,
          isTerminal: true,
        })}
      />,
    )
    await settle()

    // Assert — the pages in view, not the list from the top
    expect(requests().map((r) => r.page)).toContain(1)
  })

  it('starts over on the reader’s choice of filter', async () => {
    // Arrange
    render(<ImportDetails importProcess={run({ failedRowCount: 4 })} />)
    await settle()
    mockGetRows.mockClear()

    // Act
    act(() => screen.getByText('All').click())
    await settle()

    // Assert
    expect(requests()[0]).toMatchObject({ status: undefined, page: 1 })
  })
})
