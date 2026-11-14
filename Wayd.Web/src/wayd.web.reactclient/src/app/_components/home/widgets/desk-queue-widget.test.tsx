import type { Mock } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import DeskQueueWidget, {
  ASSIGNED_TO_ME_HREF,
  countUnassigned,
  myDesks,
  RequestsAssignedToMeTile,
  UNASSIGNED_ON_MY_DESKS_HREF,
  UnassignedRequestsTile,
} from './desk-queue-widget'
import {
  HomeContextResponse,
  IntakeDeskDto,
  IntakeDeskRole,
  IntakeDeskWaitingDto,
  IntakeRequestSummaryDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import { tileValueStyle } from '../attention-tile-test-utils'
import { homeContext } from '../home-context-test-utils'
import { renderReportingWidget } from '../widget-display-test-utils'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

const mockPush = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}))

vi.mock('@/src/hooks', () => ({
  useLinkedEmployee: () => ({ employeeId: 'me', hasLinkedEmployee: true }),
}))

vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useGetIntakeDesksQuery: vi.fn(),
  useGetIntakeRequestsQuery: vi.fn(),
}))

import {
  useGetIntakeDesksQuery,
  useGetIntakeRequestsQuery,
} from '@/src/store/features/demand-management/demand-management-api'

const mockDesks = useGetIntakeDesksQuery as Mock
const mockRequests = useGetIntakeRequestsQuery as Mock

const desk = (
  key: number,
  name: string,
  members: [string, IntakeDeskRole][],
  waiting?: Partial<IntakeDeskWaitingDto>,
): IntakeDeskDto =>
  ({
    id: `desk-${key}`,
    key,
    name,
    members: members.map(([id, role]) => ({
      employee: { id, key: 1, name: id },
      role,
    })),
    waiting: waiting ? { count: 0, unassignedCount: 0, ...waiting } : undefined,
  }) as IntakeDeskDto

const request = (
  key: number,
  title: string,
  deskKey = 1,
): IntakeRequestSummaryDto =>
  ({
    id: `request-${key}`,
    key,
    title,
    status: { id: 's', name: 'In Review', category: StatusCategory.Proposed },
    desk: { id: `desk-${deskKey}`, key: deskKey, name: `Desk ${deskKey}` },
  }) as IntakeRequestSummaryDto

const loaded = <T,>(data: T) => ({ data, isLoading: false, isError: false })

/** Answers the assigned and participating lists by the filter each asks with. */
const requestsReturn = (
  assigned: IntakeRequestSummaryDto[],
  participating: IntakeRequestSummaryDto[] = [],
) =>
  mockRequests.mockImplementation((args: { assignedToMe?: boolean }) =>
    loaded(args.assignedToMe ? assigned : participating),
  )

describe('myDesks', () => {
  it('keeps the desks the employee is a member of, by name, with their role', () => {
    // Arrange
    const desks = [
      desk(1, 'Security', [['me', IntakeDeskRole.Admin]]),
      desk(2, 'Elsewhere', [['someone', IntakeDeskRole.Agent]]),
      desk(3, 'finance', [['me', IntakeDeskRole.Agent]]),
    ]

    // Act
    const result = myDesks(desks, 'me')

    // Assert
    expect(result.map((d) => [d.desk.name, d.role])).toEqual([
      ['finance', IntakeDeskRole.Agent],
      ['Security', IntakeDeskRole.Admin],
    ])
  })
})

describe('countUnassigned', () => {
  it('adds up the unassigned requests across the desks', () => {
    // Arrange
    const desks = myDesks(
      [
        desk(1, 'A', [['me', IntakeDeskRole.Agent]], { unassignedCount: 2 }),
        desk(2, 'B', [['me', IntakeDeskRole.Agent]], { unassignedCount: 3 }),
      ],
      'me',
    )

    // Act
    const count = countUnassigned(desks)

    // Assert
    expect(count).toBe(5)
  })
})

describe('DeskQueueWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockHome.context = undefined
  })

  it('skips every request when the home context says the caller is on no desk', () => {
    // Arrange
    mockHome.context = homeContext()
    mockDesks.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })
    requestsReturn([])

    // Act
    render(<DeskQueueWidget title="Desk queue" />)

    // Assert
    expect(mockDesks).toHaveBeenCalledWith(undefined, { skip: true })
    expect(mockRequests).toHaveBeenCalledWith(
      { assignedToMe: true, openOnly: true },
      { skip: true },
    )
    expect(mockRequests).toHaveBeenCalledWith(
      { participating: true, openOnly: true },
      { skip: true },
    )
    expect(
      screen.getByText(/You are not on any intake desk/),
    ).toBeInTheDocument()
  })

  it('shows each desk with what waits on it, leaving out counts the desk does not use', () => {
    // Arrange — the desk scores but does not estimate, which the server sends as null
    mockHome.context = homeContext({ intake: { deskCount: 1 } })
    mockDesks.mockReturnValue(
      loaded([
        desk(7, 'Finance', [['me', IntakeDeskRole.Admin]], {
          count: 4,
          unassignedCount: 2,
          unscoredCount: 1,
          unestimatedCount: null as unknown as undefined,
        }),
      ]),
    )
    requestsReturn([])

    // Act
    render(<DeskQueueWidget title="Desk queue" />)

    // Assert
    const row = screen.getByRole('link', { name: 'Finance' }).closest('li')!
    expect(screen.getByRole('link', { name: 'Finance' })).toHaveAttribute(
      'href',
      '/demand-management/desks/7',
    )
    expect(within(row).getByText('Desk Admin')).toBeInTheDocument()
    expect(within(row).getByText('4 open')).toBeInTheDocument()
    expect(within(row).getByText('2 unassigned')).toBeInTheDocument()
    expect(within(row).getByText('1 unscored')).toBeInTheDocument()
    expect(within(row).queryByText(/unestimated/)).not.toBeInTheDocument()
  })

  it('lists the requests assigned to the caller and those they take part in', () => {
    // Arrange
    mockHome.context = homeContext({ intake: { deskCount: 1 } })
    mockDesks.mockReturnValue(
      loaded([desk(1, 'Finance', [['me', IntakeDeskRole.Agent]], {})]),
    )
    requestsReturn(
      [request(42, 'Export invoices')],
      [request(43, 'New laptop')],
    )

    // Act
    render(<DeskQueueWidget title="Desk queue" />)

    // Assert
    expect(
      screen.getByRole('link', { name: '42 · Export invoices' }),
    ).toHaveAttribute('href', '/demand-management/requests/42')
    expect(
      screen.getByRole('link', { name: '43 · New laptop' }),
    ).toBeInTheDocument()
  })

  describe('reported state', () => {
    const onDesk = () =>
      mockDesks.mockReturnValue(
        loaded([desk(1, 'Finance', [['me', IntakeDeskRole.Agent]], {})]),
      )

    const list = (
      overrides: Partial<{
        data: IntakeRequestSummaryDto[] | undefined
        isLoading: boolean
        isError: boolean
      }> = {},
    ) => ({ data: [], isLoading: false, isError: false, ...overrides })

    /** Answers the assigned and participating lists with their own query state. */
    const listsReturn = (
      assigned: ReturnType<typeof list>,
      participating: ReturnType<typeof list>,
    ) =>
      mockRequests.mockImplementation((args: { assignedToMe?: boolean }) =>
        args.assignedToMe ? assigned : participating,
      )

    it('reports loading while the desks load', () => {
      // Arrange
      mockDesks.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
      })
      requestsReturn([])

      // Act
      const { reportedState } = renderReportingWidget(
        <DeskQueueWidget title="Desk queue" />,
      )

      // Assert
      expect(reportedState()).toBe('loading')
    })

    it.each([
      ['assigned to me', true],
      ['participating', false],
    ])(
      'reports loading while the %s list loads for someone on a desk',
      (_, assignedLoads) => {
        // Arrange
        onDesk()
        listsReturn(
          list({ data: undefined, isLoading: assignedLoads }),
          list({ data: undefined, isLoading: !assignedLoads }),
        )

        // Act
        const { reportedState } = renderReportingWidget(
          <DeskQueueWidget title="Desk queue" />,
        )

        // Assert
        expect(reportedState()).toBe('loading')
      },
    )

    it('reports failed, though there is no data, when the desks query errors', () => {
      // Arrange
      mockDesks.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
      })
      requestsReturn([])

      // Act
      const { reportedState } = renderReportingWidget(
        <DeskQueueWidget title="Desk queue" />,
      )

      // Assert
      expect(reportedState()).toBe('failed')
    })

    it('reports empty when the caller is on no desk', () => {
      // Arrange
      mockHome.context = homeContext()
      mockDesks.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: false,
      })
      requestsReturn([])

      // Act
      const { reportedState } = renderReportingWidget(
        <DeskQueueWidget title="Desk queue" />,
      )

      // Assert
      expect(reportedState()).toBe('empty')
    })

    it('reports empty when the caller is on a desk but both request lists are empty', () => {
      // Arrange
      onDesk()
      requestsReturn([], [])

      // Act
      const { reportedState } = renderReportingWidget(
        <DeskQueueWidget title="Desk queue" />,
      )

      // Assert
      expect(reportedState()).toBe('empty')
    })

    it.each([
      ['assigned to the caller', [request(1, 'One')], []],
      ['the caller takes part in', [], [request(2, 'Two')]],
    ])('reports rows when a request is %s', (_, assigned, participating) => {
      // Arrange
      onDesk()
      requestsReturn(assigned, participating)

      // Act
      const { reportedState } = renderReportingWidget(
        <DeskQueueWidget title="Desk queue" />,
      )

      // Assert
      expect(reportedState()).toBe('rows')
    })

    it.each([
      ['assigned to me', true],
      ['participating', false],
    ])(
      'reports rows, not empty, when the %s list errored and the other is empty',
      (_, assignedErrors) => {
        // Arrange
        onDesk()
        const errored = list({ data: undefined, isError: true })
        listsReturn(
          assignedErrors ? errored : list(),
          assignedErrors ? list() : errored,
        )

        // Act
        const { reportedState } = renderReportingWidget(
          <DeskQueueWidget title="Desk queue" />,
        )

        // Assert
        expect(reportedState()).toBe('rows')
      },
    )
  })
})

describe('UnassignedRequestsTile', () => {
  it('counts the unassigned requests on the caller’s desks in the warning colour', () => {
    // Arrange
    mockDesks.mockReturnValue(
      loaded([
        desk(1, 'A', [['me', IntakeDeskRole.Agent]], { unassignedCount: 2 }),
        desk(2, 'Not mine', [['other', IntakeDeskRole.Agent]], {
          unassignedCount: 9,
        }),
      ]),
    )

    // Act
    const { container } = render(<UnassignedRequestsTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Unassigned requests on your desks: 2',
      }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-warning)')
  })

  it('opens Requests filtered to the caller’s desks and no assignee', () => {
    // Arrange
    mockDesks.mockReturnValue(
      loaded([
        desk(1, 'A', [['me', IntakeDeskRole.Agent]], { unassignedCount: 1 }),
      ]),
    )
    render(<UnassignedRequestsTile />)

    // Act
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Unassigned requests on your desks: 1',
      }),
    )

    // Assert
    expect(mockPush).toHaveBeenCalledWith(UNASSIGNED_ON_MY_DESKS_HREF)
    expect(UNASSIGNED_ON_MY_DESKS_HREF).toBe(
      '/demand-management/requests?desk=mine&assignee=unassigned',
    )
  })
})

describe('RequestsAssignedToMeTile', () => {
  it('counts the open requests assigned to the caller', () => {
    // Arrange
    requestsReturn([request(1, 'One'), request(2, 'Two')])

    // Act
    render(<RequestsAssignedToMeTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Requests assigned to you: 2' }),
    ).toBeInTheDocument()
  })

  it('opens Requests filtered to the caller as assignee', () => {
    // Arrange
    requestsReturn([request(1, 'One')])
    render(<RequestsAssignedToMeTile />)

    // Act
    fireEvent.click(
      screen.getByRole('button', { name: 'Requests assigned to you: 1' }),
    )

    // Assert
    expect(mockPush).toHaveBeenCalledWith(ASSIGNED_TO_ME_HREF)
    expect(ASSIGNED_TO_ME_HREF).toBe('/demand-management/requests?assignee=me')
  })
})

describe('desk queue scope', () => {
  const finance = desk(1, 'Finance', [['me', IntakeDeskRole.Agent]], {
    unassignedCount: 2,
  })
  const legal = desk(2, 'Legal', [['me', IntakeDeskRole.Agent]], {
    unassignedCount: 5,
  })

  beforeEach(() => {
    vi.clearAllMocks()
    mockHome.context = homeContext({ intake: { deskCount: 2 } })
    mockDesks.mockReturnValue(loaded([finance, legal]))
    requestsReturn(
      [request(1, 'On finance', 1), request(2, 'On legal', 2)],
      [request(3, 'Taking part on legal', 2)],
    )
  })

  it('shows every desk and request when nothing is chosen', () => {
    // Act
    render(<DeskQueueWidget title="Desk queue" settings={{}} />)

    // Assert
    expect(screen.getByRole('link', { name: 'Finance' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Legal' })).toBeInTheDocument()
    expect(screen.getByText(/On finance/)).toBeInTheDocument()
    expect(screen.getByText(/^2 · On legal/)).toBeInTheDocument()
  })

  it('shows only the desks chosen and the requests on them', () => {
    // Act
    render(
      <DeskQueueWidget title="Desk queue" settings={{ desks: 'desk-1' }} />,
    )

    // Assert
    expect(screen.getByRole('link', { name: 'Finance' })).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Legal' }),
    ).not.toBeInTheDocument()
    expect(screen.getByText(/On finance/)).toBeInTheDocument()
    expect(screen.queryByText(/On legal/)).not.toBeInTheDocument()
    expect(
      screen.getByText('You take part in no open request.'),
    ).toBeInTheDocument()
  })

  it('ignores a chosen desk the caller no longer works', () => {
    // Act
    render(
      <DeskQueueWidget
        title="Desk queue"
        settings={{ desks: 'desk-1,desk-gone' }}
      />,
    )

    // Assert
    expect(screen.getByRole('link', { name: 'Finance' })).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Legal' }),
    ).not.toBeInTheDocument()
  })

  it('shows every desk when no chosen desk is still the caller’s', () => {
    // Act
    render(
      <DeskQueueWidget title="Desk queue" settings={{ desks: 'desk-gone' }} />,
    )

    // Assert
    expect(screen.getByRole('link', { name: 'Finance' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Legal' })).toBeInTheDocument()
  })

  it('leaves out the sections not chosen, and skips a list it does not show', () => {
    // Act
    render(
      <DeskQueueWidget
        title="Desk queue"
        settings={{ sections: 'assigned' }}
      />,
    )

    // Assert
    expect(
      screen.queryByRole('link', { name: 'Finance' }),
    ).not.toBeInTheDocument()
    expect(screen.getByText('Assigned to me')).toBeInTheDocument()
    expect(screen.queryByText('Participating')).not.toBeInTheDocument()
    expect(mockRequests).toHaveBeenCalledWith(
      { participating: true, openOnly: true },
      { skip: true },
    )
  })

  it('opens Requests filtered to the caller’s desks from View all while nothing is chosen', () => {
    // Act
    render(<DeskQueueWidget title="Desk queue" settings={{}} />)

    // Assert
    expect(screen.getByRole('link', { name: /View all/ })).toHaveAttribute(
      'href',
      '/demand-management/requests?desk=mine',
    )
  })

  it('carries the desks chosen into View all and the rest of the assigned requests', () => {
    // Arrange — more assigned on the chosen desk than the one row shown
    requestsReturn([request(1, 'First', 1), request(2, 'Second', 1)])

    // Act
    render(
      <DeskQueueWidget
        title="Desk queue"
        settings={{ desks: 'desk-1', rows: '1' }}
      />,
    )

    // Assert
    expect(screen.getByRole('link', { name: /View all/ })).toHaveAttribute(
      'href',
      '/demand-management/requests?desk=desk-1',
    )
    expect(screen.getByRole('link', { name: 'All 2' })).toHaveAttribute(
      'href',
      '/demand-management/requests?assignee=me&desk=desk-1',
    )
  })

  it('waits for the desks before counting assigned requests on the desks chosen', () => {
    // Arrange
    mockDesks.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    })

    // Act
    render(<RequestsAssignedToMeTile settings={{ desks: 'desk-1' }} />)

    // Assert
    expect(
      screen.queryByRole('button', { name: /Requests assigned to you: \d/ }),
    ).not.toBeInTheDocument()
  })

  it('counts unassigned requests only on the desks chosen, and links to them', () => {
    // Arrange
    render(<UnassignedRequestsTile settings={{ desks: 'desk-2' }} />)

    // Act
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Unassigned requests on your desks: 5',
      }),
    )

    // Assert
    expect(mockPush).toHaveBeenCalledWith(
      '/demand-management/requests?assignee=unassigned&desk=desk-2',
    )
  })

  it('counts every desk’s unassigned requests for a stale choice', () => {
    // Act
    render(<UnassignedRequestsTile settings={{ desks: 'desk-gone' }} />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Unassigned requests on your desks: 7',
      }),
    ).toBeInTheDocument()
  })

  it('counts no unassigned requests while the queue leaves out its desks', () => {
    // Act
    render(
      <UnassignedRequestsTile
        settings={{ sections: 'assigned,participating' }}
      />,
    )

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Unassigned requests on your desks: 0',
      }),
    ).toBeInTheDocument()
  })

  it('counts requests assigned on the desks chosen, and links to them', () => {
    // Arrange
    render(<RequestsAssignedToMeTile settings={{ desks: 'desk-1' }} />)

    // Act
    fireEvent.click(
      screen.getByRole('button', { name: 'Requests assigned to you: 1' }),
    )

    // Assert
    expect(mockPush).toHaveBeenCalledWith(
      '/demand-management/requests?assignee=me&desk=desk-1',
    )
  })

  it('counts no assigned requests while the queue leaves out that list', () => {
    // Act
    render(<RequestsAssignedToMeTile settings={{ sections: 'desks' }} />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Requests assigned to you: 0' }),
    ).toBeInTheDocument()
  })
})
