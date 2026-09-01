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
