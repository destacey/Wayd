import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  SizingMethod,
  SprintDetailsDto,
  SprintScopeDto,
} from '@/src/services/wayd-api'
import { useGetSprintScopeQuery } from '@/src/store/features/work-management/sprints-api'
import SprintScopeSummary, {
  sprintScopeWindowText,
} from './sprint-scope-summary'

jest.mock('@/src/store/features/work-management/sprints-api', () => ({
  useGetSprintScopeQuery: jest.fn(),
}))

jest.mock('@/src/components/common/metrics', () => ({
  METRIC_CARD_FLEX: {},
  MetricCard: ({
    title,
    value,
    secondaryValue,
  }: {
    title: string
    value: number
    secondaryValue?: string
  }) => (
    <div>
      <span data-testid={`value-${title}`}>{value}</span>
      {secondaryValue && (
        <span data-testid={`secondary-${title}`}>{secondaryValue}</span>
      )}
    </div>
  ),
}))

const sprint = { key: 201 } as SprintDetailsDto

const measure = (count: number, estimate: number) => ({ count, estimate })

const scope: SprintScopeDto = {
  sprintId: 's1',
  sizingMethod: SizingMethod.StoryPoints,
  effectiveStart: '2026-09-15T05:00:00Z' as unknown as Date,
  startIsActual: false,
  effectiveEnd: '2026-09-25T20:00:00Z' as unknown as Date,
  endIsActual: true,
  lastDay: '2026-09-25T05:00:00Z' as unknown as Date,
  timeZone: 'America/Chicago',
  hasTeam: true,
  historyIncomplete: false,
  totals: {
    total: measure(6, 21),
    committed: measure(4, 13),
    added: measure(2, 8),
    completed: measure(3, 10),
    removed: measure(1, 2),
    carriedOver: measure(2, 8),
    descoped: measure(1, 3),
    remaining: measure(0, 0),
    completedOfCommitted: measure(2, 5),
    sayDoCount: 0.5,
    sayDoEstimate: 5 / 13,
    unestimated: 0,
  },
  items: [],
}

const mockScope = (data: SprintScopeDto) =>
  (useGetSprintScopeQuery as jest.Mock).mockReturnValue({
    data,
    isLoading: false,
  })

describe('sprintScopeWindowText', () => {
  it('says where each end came from, in the team’s zone', () => {
    // Arrange / Act
    const result = sprintScopeWindowText(scope)

    // Assert
    expect(result).toEqual([
      'Committed at Sep 15, 2026, 12:00 AM CDT, the end of the commitment grace period after the planned start.',
      'Ends at Sep 25, 2026, 3:00 PM CDT, when the team completed the sprint.',
      'Times are in the team’s zone, America/Chicago.',
    ])
  })

  it('labels a sprint with no team as using the system defaults', () => {
    // Arrange / Act
    const result = sprintScopeWindowText({
      ...scope,
      hasTeam: false,
      timeZone: 'UTC',
    })

    // Assert
    expect(result[2]).toBe(
      'This sprint has no team, so it uses the system default zone (UTC) and grace period, and counts work items.',
    )
  })
})

describe('SprintScopeSummary', () => {
  it('shows the scope in the sprint’s estimate, with the Removed sub-count and say/do', () => {
    // Arrange
    mockScope(scope)

    // Act
    render(<SprintScopeSummary sprint={sprint} />)

    // Assert
    expect(screen.getByTestId('value-Committed')).toHaveTextContent('13')
    expect(screen.getByTestId('value-Added')).toHaveTextContent('8')
    expect(screen.getByTestId('value-Completed')).toHaveTextContent('10')
    expect(screen.getByTestId('secondary-Completed')).toHaveTextContent(
      '2 as Removed',
    )
    expect(screen.getByTestId('value-Carried Over')).toHaveTextContent('8')
    expect(screen.getByTestId('value-Descoped')).toHaveTextContent('3')
    expect(screen.getByTestId('value-Say/Do')).toHaveTextContent(
      String((5 / 13) * 100),
    )
    expect(screen.queryByTestId('value-Remaining')).not.toBeInTheDocument()
  })

  it('counts work items when switched to Count', async () => {
    // Arrange
    mockScope(scope)
    render(<SprintScopeSummary sprint={sprint} />)

    // Act
    await userEvent.click(screen.getByText('Count'))

    // Assert
    expect(screen.getByTestId('value-Committed')).toHaveTextContent('4')
    expect(screen.getByTestId('value-Say/Do')).toHaveTextContent('50')
  })

  it('shows remaining work and say/do so far while the sprint runs', () => {
    // Arrange
    mockScope({
      ...scope,
      totals: { ...scope.totals, remaining: measure(2, 5) },
    })

    // Act
    render(<SprintScopeSummary sprint={sprint} />)

    // Assert
    expect(screen.getByTestId('value-Remaining')).toHaveTextContent('5')
    expect(screen.getByTestId('value-Say/Do so far')).toBeInTheDocument()
  })

  it('shows only the incomplete-history warning when history is incomplete', () => {
    // Arrange
    mockScope({ ...scope, historyIncomplete: true })

    // Act
    render(<SprintScopeSummary sprint={sprint} />)

    // Assert
    expect(
      screen.getByText('History incomplete — run a full sync'),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('value-Committed')).not.toBeInTheDocument()
  })
})
