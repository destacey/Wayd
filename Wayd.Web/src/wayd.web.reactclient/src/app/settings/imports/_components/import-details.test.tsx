import { act, render, screen } from '@testing-library/react'
import {
  ImportAtomicity,
  ImportProcessDto,
  ImportProcessStatus,
  ImportRowStatus,
} from '@/src/services/wayd-api'
import ImportDetails from './import-details'

const mockUseGetImportProcessRowsQuery = vi.fn()

vi.mock('@/src/store/features/admin/imports-api', () => ({
  useGetImportProcessRowsQuery: (...args: unknown[]) =>
    mockUseGetImportProcessRowsQuery(...args),
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

/** Steps the pager forward from the first page to the third. */
const toPage3 = () => {
  act(() => screen.getByTitle('Next Page').click())
  act(() => screen.getByTitle('Next Page').click())
}

/** The arguments of the most recent rows request. */
const lastRequest = () =>
  mockUseGetImportProcessRowsQuery.mock.calls.at(-1)![0] as {
    status?: ImportRowStatus
    page: number
  }

describe('ImportDetails', () => {
  beforeEach(() => {
    mockUseGetImportProcessRowsQuery.mockReset()
    const page = {
      items: [],
      pageNumber: 1,
      pageSize: 50,
      totalCount: 400,
      totalPages: 8,
      hasPreviousPage: false,
      hasNextPage: true,
    }
    mockUseGetImportProcessRowsQuery.mockReturnValue({
      data: page,
      currentData: page,
      isFetching: false,
    })
  })

  it('goes back to the first page when the run rejects its first row and the view moves to the rejected rows', () => {
    // Arrange — reading page 3 of every row while the run is still going
    const { rerender } = render(<ImportDetails importProcess={run()} />)
    toPage3()
    expect(lastRequest()).toMatchObject({ status: undefined, page: 3 })

    // Act
    rerender(<ImportDetails importProcess={run({ failedRowCount: 1 })} />)

    // Assert — page 3 of every row is not page 3 of the rejected ones
    expect(lastRequest()).toMatchObject({
      status: ImportRowStatus.Failed,
      page: 1,
    })
  })

  it('keeps the page while the filter in effect stays the same', () => {
    // Arrange
    const { rerender } = render(<ImportDetails importProcess={run()} />)
    toPage3()

    // Act — progress moves on, but nothing was rejected
    rerender(<ImportDetails importProcess={run({ succeededRowCount: 180 })} />)

    // Assert
    expect(lastRequest()).toMatchObject({ status: undefined, page: 3 })
  })

  it('goes back to the first page when the reader picks another filter', () => {
    // Arrange
    render(<ImportDetails importProcess={run({ failedRowCount: 4 })} />)
    toPage3()

    // Act
    act(() => screen.getByText('All').click())

    // Assert
    expect(lastRequest()).toMatchObject({ status: undefined, page: 1 })
  })
})
