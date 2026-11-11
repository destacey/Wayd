import type { Mock } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import MyRequestsWidget, {
  deliverableProgress,
  recentlyClosedSince,
  sortMyRequests,
} from './my-requests-widget'
import {
  DeliverySignal,
  IntakeRequestOutcomeDto,
  IntakeRequestOutcomeKind,
  IntakeRequestOutcomesDto,
  IntakeRequestSummaryDto,
  ProjectDeliverableStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'

vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useGetIntakeRequestsQuery: vi.fn(),
  useGetIntakeRequestOutcomesQuery: vi.fn(),
}))

import {
  useGetIntakeRequestOutcomesQuery,
  useGetIntakeRequestsQuery,
} from '@/src/store/features/demand-management/demand-management-api'

const mockRequests = useGetIntakeRequestsQuery as Mock
const mockOutcomes = useGetIntakeRequestOutcomesQuery as Mock

const request = (
  key: number,
  title: string,
  category: StatusCategory,
  statusChangedOn: string,
): IntakeRequestSummaryDto =>
  ({
    id: `request-${key}`,
    key,
    title,
    status: {
      id: 's',
      name: category === StatusCategory.Done ? 'Fulfilled' : 'In Review',
      category,
    },
    desk: { id: 'desk-1', key: 1, name: 'Finance' },
    statusChangedOn: new Date(statusChangedOn),
  }) as IntakeRequestSummaryDto

let deliverableCount = 0

const deliverable = (
  alias: ProjectDeliverableStatusAlias,
  isOnProject = true,
) => ({
  id: `d-${++deliverableCount}`,
  name: 'Deliverable',
  statusAlias: alias,
  statusCategory: StatusCategory.Active,
  isOnProject,
})

const projectOutcome = (
  overrides: Partial<IntakeRequestOutcomeDto> = {},
): IntakeRequestOutcomeDto =>
  ({
    kind: IntakeRequestOutcomeKind.ExistingProjectScope,
    targetId: 'project-1',
    project: { id: 'project-1', key: 'ATLAS', name: 'Atlas' },
    deliverables: [],
    ...overrides,
  }) as IntakeRequestOutcomeDto

const loaded = <T,>(data: T) => ({ data, isLoading: false, isError: false })

describe('recentlyClosedSince', () => {
  it('starts at the beginning of the day thirty days back', () => {
    // Arrange
    const now = new Date(2026, 10, 10, 15, 30)

    // Act
    const since = new Date(recentlyClosedSince(now))

    // Assert
    expect(since).toEqual(new Date(2026, 9, 11, 0, 0, 0, 0))
  })
})

describe('sortMyRequests', () => {
  it('puts open requests first, each group most recently moved first', () => {
    // Arrange
    const requests = [
      request(1, 'Closed lately', StatusCategory.Done, '2026-11-09'),
      request(2, 'Open, older', StatusCategory.Proposed, '2026-10-01'),
      request(3, 'Open, newer', StatusCategory.Active, '2026-11-01'),
    ]

    // Act
    const sorted = sortMyRequests(requests)

    // Assert
    expect(sorted.map((r) => r.title)).toEqual([
      'Open, newer',
      'Open, older',
      'Closed lately',
    ])
  })
})

describe('deliverableProgress', () => {
  it('counts the accepted deliverables among those still on the project', () => {
    // Arrange
    const outcome = projectOutcome({
      deliverables: [
        deliverable(ProjectDeliverableStatusAlias.Accepted),
        deliverable(ProjectDeliverableStatusAlias.InProgress),
        deliverable(ProjectDeliverableStatusAlias.Accepted, false),
      ],
    })

    // Act
    const progress = deliverableProgress(outcome)

    // Assert
    expect(progress).toBe('1 of 2 deliverables accepted')
  })

  it('says nothing for an outcome that maps no deliverable', () => {
    // Arrange / Act / Assert
    expect(deliverableProgress(projectOutcome())).toBeUndefined()
  })
})

describe('MyRequestsWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('asks for the caller’s requests, open or closed since thirty days ago, with their outcomes', () => {
    // Arrange
    mockRequests.mockReturnValue(loaded([]))
    mockOutcomes.mockReturnValue(loaded([]))

    // Act
    render(<MyRequestsWidget title="My requests" />)

    // Assert
    const args = { mine: true, closedSince: recentlyClosedSince() }
    expect(mockRequests).toHaveBeenCalledWith(args)
    expect(mockOutcomes).toHaveBeenCalledWith(args)
  })

  it('says how it fills when the caller has no recent request', () => {
    // Arrange
    mockRequests.mockReturnValue(loaded([]))
    mockOutcomes.mockReturnValue(loaded([]))

    // Act
    render(<MyRequestsWidget title="My requests" />)

    // Assert
    expect(
      screen.getByText(/no open or recently closed requests/),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Submit a request' }),
    ).toHaveAttribute('href', '/demand-management/submit')
  })

  it('shows each request with its status and what it became', () => {
    // Arrange
    mockRequests.mockReturnValue(
      loaded([
        request(42, 'Export invoices', StatusCategory.Active, '2026-11-01'),
      ]),
    )
    const outcomes: IntakeRequestOutcomesDto[] = [
      {
        requestId: 'request-42',
        outcomes: [
          projectOutcome({
            isMet: false,
            deliverables: [
              deliverable(ProjectDeliverableStatusAlias.Accepted),
              deliverable(ProjectDeliverableStatusAlias.NotStarted),
            ],
          }),
          {
            kind: IntakeRequestOutcomeKind.WorkItem,
            targetId: 'wi-1',
            workItem: {
              id: 'wi-1',
              key: 'FIN-12',
              title: 'Add CSV export',
              workspaceKey: 'FIN',
              status: 'Doing',
              statusCategory: StatusCategory.Active,
            },
            deliverables: [],
          } as unknown as IntakeRequestOutcomeDto,
        ],
      },
    ]
    mockOutcomes.mockReturnValue(loaded(outcomes))

    // Act
    render(<MyRequestsWidget title="My requests" />)

    // Assert
    const row = screen.getByRole('listitem')
    expect(
      within(row).getByRole('link', { name: '42 · Export invoices' }),
    ).toHaveAttribute('href', '/demand-management/requests/42')
    expect(within(row).getByText('In Review')).toBeInTheDocument()
    expect(
      within(row).getByRole('link', { name: 'ATLAS · Atlas' }),
    ).toHaveAttribute('href', '/ppm/projects/ATLAS')
    expect(
      within(row).getByText('1 of 2 deliverables accepted'),
    ).toBeInTheDocument()
    expect(
      within(row).getByRole('link', { name: 'FIN-12 · Add CSV export' }),
    ).toHaveAttribute('href', '/work/workspaces/FIN/work-items/FIN-12')
    expect(within(row).getByText('Doing')).toBeInTheDocument()
  })

  it('marks an outcome whose deliverables are all accepted as met', () => {
    // Arrange
    mockRequests.mockReturnValue(
      loaded([
        request(42, 'Export invoices', StatusCategory.Done, '2026-11-01'),
      ]),
    )
    mockOutcomes.mockReturnValue(
      loaded([
        {
          requestId: 'request-42',
          outcomes: [
            projectOutcome({
              isMet: true,
              deliverables: [
                deliverable(ProjectDeliverableStatusAlias.Accepted),
              ],
            }),
          ],
          deliverablesMet: true,
        },
      ]),
    )

    // Act
    render(<MyRequestsWidget title="My requests" />)

    // Assert
    expect(screen.getByText('Met')).toBeInTheDocument()
    expect(screen.queryByText(/deliverables accepted/)).not.toBeInTheDocument()
  })

  it('shows each request’s delivery state, and none for a request without one', () => {
    // Arrange
    mockRequests.mockReturnValue(
      loaded([
        request(42, 'Export invoices', StatusCategory.Active, '2026-11-02'),
        request(43, 'Import ledgers', StatusCategory.Active, '2026-11-01'),
      ]),
    )
    mockOutcomes.mockReturnValue(
      loaded([
        {
          requestId: 'request-42',
          outcomes: [projectOutcome({ deliverySignal: DeliverySignal.Done })],
          deliveryState: DeliverySignal.InProgress,
        },
      ]),
    )

    // Act
    render(<MyRequestsWidget title="My requests" />)

    // Assert
    const [withState, without] = screen.getAllByRole('listitem')
    expect(within(withState).getByText('In Progress')).toBeInTheDocument()
    expect(within(without).queryByText('In Progress')).not.toBeInTheDocument()
  })

  it('leaves the delivery state off a request that was declined or withdrawn', () => {
    // Arrange
    mockRequests.mockReturnValue(
      loaded([
        request(42, 'Export invoices', StatusCategory.Removed, '2026-11-02'),
      ]),
    )
    mockOutcomes.mockReturnValue(
      loaded([
        {
          requestId: 'request-42',
          outcomes: [projectOutcome()],
          deliveryState: DeliverySignal.InProgress,
        },
      ]),
    )

    // Act
    render(<MyRequestsWidget title="My requests" />)

    // Assert
    expect(screen.getByRole('listitem')).toBeInTheDocument()
    expect(screen.queryByText('In Progress')).not.toBeInTheDocument()
  })
})
