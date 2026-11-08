import { render, screen } from '@testing-library/react'
import PlanningIntervalsWidget, {
  countdownText,
  currentPlanningIntervals,
} from './planning-intervals-widget'
import { PlanningIntervalListDto } from '@/src/services/wayd-api'
import { IterationState } from '@/src/components/types'

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalsQuery: jest.fn(),
  useGetPlanningIntervalIterationsQuery: jest.fn(() => ({ data: undefined })),
}))

import { useGetPlanningIntervalsQuery } from '@/src/store/features/planning/planning-interval-api'

const mockPlanningIntervals = useGetPlanningIntervalsQuery as jest.Mock

const pi = (
  key: number,
  state: IterationState,
  start: string,
  end: string,
): PlanningIntervalListDto =>
  ({
    id: `pi-${key}`,
    key,
    name: `PI ${key}`,
    start,
    end,
    state: { id: state, name: IterationState[state] },
  }) as unknown as PlanningIntervalListDto

describe('currentPlanningIntervals', () => {
  it('keeps active ones and future ones starting within two weeks, soonest first', () => {
    // Arrange
    const today = '2026-11-08'
    const all = [
      pi(4, IterationState.Future, '2026-11-30', '2027-02-01'),
      pi(3, IterationState.Future, '2026-11-22', '2027-01-31'),
      pi(1, IterationState.Completed, '2026-05-01', '2026-08-01'),
      pi(2, IterationState.Active, '2026-08-02', '2026-11-21'),
    ]

    // Act
    const current = currentPlanningIntervals(all, today)

    // Assert
    expect(current.map((p) => p.key)).toEqual([2, 3])
  })
})

describe('countdownText', () => {
  it.each([
    ['2026-11-21', '1 day left'],
    ['2026-11-22', '2 days left'],
  ])(
    'counts today among the days left of an active one ending %s',
    (end, expected) => {
      // Arrange
      const active = pi(2, IterationState.Active, '2026-08-02', end)

      // Act
      const text = countdownText(active, '2026-11-21')

      // Assert
      expect(text).toBe(expected)
    },
  )

  it('says how long until a future one starts', () => {
    // Arrange
    const future = pi(3, IterationState.Future, '2026-11-22', '2027-01-31')

    // Act
    const text = countdownText(future, '2026-11-08')

    // Assert
    expect(text).toBe('starts in 14 days')
  })
})

describe('PlanningIntervalsWidget', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('says what it shows when no planning interval is current', () => {
    // Arrange
    mockPlanningIntervals.mockReturnValue({
      data: [pi(1, IterationState.Completed, '2025-05-01', '2025-08-01')],
      isLoading: false,
      isError: false,
    })

    // Act
    render(<PlanningIntervalsWidget title="Planning intervals" />)

    // Assert
    expect(
      screen.getByText(/No planning interval is active or starts/),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'All planning intervals' }),
    ).toHaveAttribute('href', '/planning/planning-intervals')
  })

  it('links each current planning interval to its overview and plan review', () => {
    // Arrange
    mockPlanningIntervals.mockReturnValue({
      data: [pi(7, IterationState.Active, '2020-01-01', '2099-01-01')],
      isLoading: false,
      isError: false,
    })

    // Act
    render(<PlanningIntervalsWidget title="Planning intervals" />)

    // Assert
    expect(screen.getByRole('link', { name: 'PI 7' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/7',
    )
    expect(screen.getByRole('link', { name: 'Plan Review' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/7?section=plan-review',
    )
  })
})
