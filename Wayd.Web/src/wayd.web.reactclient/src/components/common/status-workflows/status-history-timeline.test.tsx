import { render, screen } from '@testing-library/react'
import { StatusCategory, StatusTransitionDto } from '@/src/services/wayd-api'
import StatusHistoryTimeline from './status-history-timeline'

const transition = (
  overrides: Partial<StatusTransitionDto> = {},
): StatusTransitionDto =>
  ({
    id: '11111111-1111-1111-1111-111111111111',
    sequence: 0,
    toStatus: {
      id: '22222222-2222-2222-2222-222222222222',
      name: 'Active',
      category: StatusCategory.Active,
      alias: 0,
    },
    workflowId: '33333333-3333-3333-3333-333333333333',
    actorKind: { id: 0, name: 'User' },
    changedBySystem: false,
    changedOn: '2026-04-01T09:00:00Z',
    durationSeconds: 0,
    statusVisitCount: 1,
    statusTotalSeconds: 0,
    ...overrides,
  }) as StatusTransitionDto

describe('StatusHistoryTimeline', () => {
  it('renders a skeleton while loading', () => {
    // Arrange / Act
    const { container } = render(
      <StatusHistoryTimeline transitions={undefined} isLoading={true} />,
    )

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders the empty description when there is no history', () => {
    // Arrange / Act
    render(
      <StatusHistoryTimeline
        transitions={[]}
        isLoading={false}
        emptyDescription="Nothing recorded."
      />,
    )

    // Assert
    expect(screen.getByText('Nothing recorded.')).toBeInTheDocument()
  })

  it('reports how long a past status was held', () => {
    // Arrange — Active from 1 March to 1 June.
    const transitions = [
      transition({
        leftOn: new Date('2026-06-01T00:00:00Z'),
        durationSeconds: 92 * 24 * 60 * 60,
      }),
    ]

    // Act
    render(
      <StatusHistoryTimeline transitions={transitions} isLoading={false} />,
    )

    // Assert
    expect(screen.getByText('92 days in Active')).toBeInTheDocument()
  })

  it('reports the current status as still running', () => {
    // Arrange — no leftOn: the record is still in this status.
    const transitions = [
      transition({
        toStatus: {
          id: '22222222-2222-2222-2222-222222222222',
          name: 'Sunset',
          category: StatusCategory.Active,
          alias: 0,
        },
        durationSeconds: 41 * 24 * 60 * 60 + 3600,
      }),
    ]

    // Act
    render(
      <StatusHistoryTimeline transitions={transitions} isLoading={false} />,
    )

    // Assert
    expect(screen.getByText('In Sunset for 41 days')).toBeInTheDocument()
  })

  it('totals the stays in a status the record returned to', () => {
    // Arrange — the second of two stays in Active: 6 days now, 10 days in all.
    const transitions = [
      transition({
        durationSeconds: 6 * 24 * 60 * 60,
        statusVisitCount: 2,
        statusTotalSeconds: 10 * 24 * 60 * 60,
      }),
    ]

    // Act
    render(
      <StatusHistoryTimeline transitions={transitions} isLoading={false} />,
    )

    // Assert
    expect(
      screen.getByText('In Active for 6 days (10 days across 2 stays)'),
    ).toBeInTheDocument()
  })

  it('renders both sides of a transition', () => {
    // Arrange
    const transitions = [
      transition({
        fromStatus: {
          id: '44444444-4444-4444-4444-444444444444',
          name: 'Proposed',
          category: StatusCategory.Proposed,
        },
      }),
    ]

    // Act
    render(
      <StatusHistoryTimeline transitions={transitions} isLoading={false} />,
    )

    // Assert
    expect(screen.getByText('Proposed')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('names the employee in preference to the account', () => {
    // Arrange — an import records both, and they are different people.
    const transitions = [
      transition({
        changedBy: {
          id: '55555555-5555-5555-5555-555555555555',
          key: 42,
          name: 'Grace Hopper',
        },
        changedByUser: {
          id: '66666666-6666-6666-6666-666666666666',
          userName: 'operator',
          name: 'Import Operator',
        },
      }),
    ]

    // Act
    render(
      <StatusHistoryTimeline transitions={transitions} isLoading={false} />,
    )

    // Assert
    expect(screen.getByText(/Grace Hopper/)).toBeInTheDocument()
    expect(screen.queryByText(/Import Operator/)).not.toBeInTheDocument()
  })

  it('reports the system only when the recorded account says so', () => {
    // Arrange — an account deleted since the change also resolves to no name, and must read as
    // Unknown rather than as the platform acting.
    const transitions = [
      transition({ id: 'a', changedBySystem: true }),
      transition({ id: 'b', changedBySystem: false }),
    ]

    // Act
    render(
      <StatusHistoryTimeline transitions={transitions} isLoading={false} />,
    )

    // Assert
    expect(screen.getByText(/by System/)).toBeInTheDocument()
    expect(screen.getByText(/by Unknown/)).toBeInTheDocument()
  })
})
