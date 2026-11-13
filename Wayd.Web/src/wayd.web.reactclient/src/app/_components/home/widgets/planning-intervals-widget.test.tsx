import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import PlanningIntervalsWidget, {
  countdownText,
  currentPlanningIntervals,
} from './planning-intervals-widget'
import { PlanningIntervalListDto } from '@/src/services/wayd-api'
import { IterationState } from '@/src/components/types'
import { renderReportingWidget } from '../widget-display-test-utils'

vi.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalsQuery: vi.fn(),
  useGetPlanningIntervalIterationsQuery: vi.fn(() => ({ data: undefined })),
}))

import { useGetPlanningIntervalsQuery } from '@/src/store/features/planning/planning-interval-api'

const mockPlanningIntervals = useGetPlanningIntervalsQuery as Mock

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

  it('keeps future ones starting within a longer window when given one', () => {
    // Arrange
    const today = '2026-11-08'
    const all = [
      pi(4, IterationState.Future, '2026-12-08', '2027-02-01'),
      pi(5, IterationState.Future, '2026-12-09', '2027-03-01'),
    ]

    // Act
    const current = currentPlanningIntervals(all, today, 30)

    // Assert
    expect(current.map((p) => p.key)).toEqual([4])
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
    vi.clearAllMocks()
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

  it('names the window it is set to when no planning interval is current', () => {
    // Arrange
    mockPlanningIntervals.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
    })

    // Act
    render(
      <PlanningIntervalsWidget
        title="Planning intervals"
        settings={{ 'upcoming-window': '30' }}
      />,
    )

    // Assert
    expect(
      screen.getByText(
        'No planning interval is active or starts in the next 30 days.',
      ),
    ).toBeInTheDocument()
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

  it.each([
    [
      'loading',
      { data: undefined, isLoading: true, isError: false },
      'loading',
    ],
    ['failed', { data: undefined, isLoading: false, isError: true }, 'failed'],
    [
      'empty when the query returned nothing',
      { data: [], isLoading: false, isError: false },
      'empty',
    ],
    [
      'empty when only past and far-future planning intervals came back',
      {
        data: [
          pi(1, IterationState.Completed, '2025-05-01', '2025-08-01'),
          pi(2, IterationState.Future, '2099-01-01', '2099-04-01'),
        ],
        isLoading: false,
        isError: false,
      },
      'empty',
    ],
    [
      'rows when a planning interval is active',
      {
        data: [pi(7, IterationState.Active, '2020-01-01', '2099-01-01')],
        isLoading: false,
        isError: false,
      },
      'rows',
    ],
  ])('reports %s to the page', (_name, query, expected) => {
    // Arrange
    mockPlanningIntervals.mockReturnValue(query)

    // Act
    const { reportedState } = renderReportingWidget(
      <PlanningIntervalsWidget title="Planning intervals" />,
    )

    // Assert
    expect(reportedState()).toBe(expected)
  })
})
