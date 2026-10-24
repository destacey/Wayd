import { render, screen, within } from '@testing-library/react'
import { StatusCategory, TimeInStatusReportDto } from '@/src/services/wayd-api'
import TimeInStatusReport from './time-in-status-report'

let mockPermissions: string[] = []
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasPermissionClaim: (permission: string) =>
      mockPermissions.includes(permission),
  }),
}))

const mockQueryArgs = jest.fn()
let mockReport: TimeInStatusReportDto | undefined
jest.mock('@/src/store/features/product-management/time-in-status-api', () => ({
  useGetTimeInStatusQuery: (arg: unknown, options: unknown) => {
    mockQueryArgs(arg, options)
    return { data: mockReport, isFetching: false, error: undefined }
  },
}))

const DAY = 24 * 60 * 60

describe('TimeInStatusReport', () => {
  beforeEach(() => {
    mockQueryArgs.mockClear()
    mockReport = {
      from: new Date('2026-03-01T00:00:00Z'),
      to: new Date('2026-03-31T00:00:00Z'),
      statuses: [
        {
          statusId: 'ready',
          name: 'Ready',
          category: StatusCategory.Active,
          inCurrentWorkflow: true,
          visits: 3,
          averageSeconds: 5 * DAY,
          medianSeconds: 4 * DAY,
        },
        {
          statusId: 'released',
          name: 'Released',
          category: StatusCategory.Done,
          inCurrentWorkflow: true,
          visits: 0,
        },
        {
          statusId: 'live',
          name: 'Live',
          category: StatusCategory.Active,
          inCurrentWorkflow: false,
          visits: 1,
          averageSeconds: 7 * DAY,
          medianSeconds: 7 * DAY,
        },
      ],
    }
  })

  it('renders each status with its stays, average and median', () => {
    // Arrange
    mockPermissions = ['Permissions.Delivery.View']

    // Act
    render(
      <TimeInStatusReport
        from="2026-03-01T00:00:00Z"
        to="2026-03-31T00:00:00Z"
      />,
    )

    // Assert
    const ready = screen.getByText('Ready').closest('tr')!
    expect(within(ready).getByText('3')).toBeInTheDocument()
    expect(within(ready).getByText('5 days')).toBeInTheDocument()
    expect(within(ready).getByText('4 days')).toBeInTheDocument()

    const released = screen.getByText('Released').closest('tr')!
    expect(within(released).getAllByText('—')).toHaveLength(2)

    const live = screen.getByText('Live').closest('tr')!
    expect(within(live).getByText('(earlier)')).toBeInTheDocument()
  })

  it('asks for the first record type the reader may see, over the given window', () => {
    // Arrange — no delivery permission, so versions, packages and deployments are not offered.
    mockPermissions = ['Permissions.Releases.View']

    // Act
    render(
      <TimeInStatusReport
        from="2026-03-01T00:00:00Z"
        to="2026-03-31T00:00:00Z"
      />,
    )

    // Assert
    expect(mockQueryArgs).toHaveBeenLastCalledWith(
      {
        recordType: 'releases',
        from: '2026-03-01T00:00:00Z',
        to: '2026-03-31T00:00:00Z',
      },
      { skip: false },
    )
  })

  it('renders nothing and asks for nothing without any record type permission', () => {
    // Arrange
    mockPermissions = []

    // Act
    const { container } = render(
      <TimeInStatusReport
        from="2026-03-01T00:00:00Z"
        to="2026-03-31T00:00:00Z"
      />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
    expect(mockQueryArgs).toHaveBeenLastCalledWith(expect.anything(), {
      skip: true,
    })
  })
})
