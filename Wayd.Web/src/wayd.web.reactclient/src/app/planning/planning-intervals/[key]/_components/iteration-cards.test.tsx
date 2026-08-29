import { render, screen } from '@testing-library/react'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: jest.fn().mockImplementation((query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: jest.fn(),
    removeListener: jest.fn(),
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
    dispatchEvent: jest.fn(),
  })),
})

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalIterationsQuery: jest.fn(),
  useGetPlanningIntervalIterationMetricsQuery: jest.fn(),
}))

import {
  useGetPlanningIntervalIterationsQuery,
  useGetPlanningIntervalIterationMetricsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import IterationsCards from './iteration-cards'

const mockQuery = useGetPlanningIntervalIterationsQuery as unknown as jest.Mock
const mockMetricsQuery =
  useGetPlanningIntervalIterationMetricsQuery as unknown as jest.Mock

// Build a Date from a YYYY-MM-DD string in the *local* timezone so dayjs
// formatting in the component matches the input (otherwise "2024-07-22" is
// parsed as UTC and drifts to Jul 21 in negative-offset zones).
const localDate = (isoDay: string) => {
  const [y, m, d] = isoDay.split('-').map(Number)
  return new Date(y, m - 1, d)
}

const mkIteration = (overrides: {
  key: number
  name: string
  start: string
  end: string
  state: 'Future' | 'Active' | 'Completed'
  categoryName?: string
}) => ({
  id: `id-${overrides.key}`,
  key: overrides.key,
  name: overrides.name,
  start: localDate(overrides.start) as unknown as Date,
  end: localDate(overrides.end) as unknown as Date,
  state: overrides.state,
  category: { id: 1, name: overrides.categoryName ?? 'Development' },
})

describe('IterationCards', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    jest.useRealTimers()
    mockMetricsQuery.mockReturnValue({ data: undefined })
  })

  afterEach(() => {
    jest.useRealTimers()
  })

  it('shows a loading skeleton while the query is pending', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: true })

    const { container } = render(<IterationsCards piKey={1} />)

    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders nothing when there are no iterations', () => {
    mockQuery.mockReturnValue({ data: [], isLoading: false })

    const { container } = render(<IterationsCards piKey={1} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders iterations ordered by start date', () => {
    mockQuery.mockReturnValue({
      data: [
        mkIteration({
          key: 3,
          name: '24.2.3',
          start: '2024-06-10',
          end: '2024-06-23',
          state: 'Active',
        }),
        mkIteration({
          key: 1,
          name: '24.2.1',
          start: '2024-05-13',
          end: '2024-05-26',
          state: 'Completed',
        }),
        mkIteration({
          key: 2,
          name: '24.2.2',
          start: '2024-05-27',
          end: '2024-06-09',
          state: 'Completed',
        }),
      ],
      isLoading: false,
    })

    render(<IterationsCards piKey={7} />)

    const cards = screen.getAllByRole('link')
    expect(cards.map((c) => c.getAttribute('href'))).toEqual([
      '/planning/planning-intervals/7/iterations/1',
      '/planning/planning-intervals/7/iterations/2',
      '/planning/planning-intervals/7/iterations/3',
    ])
  })

  it('shows "Day X/Y" progress for the active iteration', () => {
    jest.useFakeTimers().setSystemTime(localDate('2024-06-16').getTime())

    mockQuery.mockReturnValue({
      data: [
        mkIteration({
          key: 3,
          name: '24.2.3',
          start: '2024-06-10',
          end: '2024-06-23',
          state: 'Active',
        }),
      ],
      isLoading: false,
    })

    render(<IterationsCards piKey={1} />)

    // Jun 10..Jun 23 is 14 days inclusive; Jun 16 is day 7.
    expect(
      screen.getByText((content) => /Day\s+7\/14/i.test(content)),
    ).toBeInTheDocument()
  })

  it('does not show day progress for non-active iterations', () => {
    mockQuery.mockReturnValue({
      data: [
        mkIteration({
          key: 1,
          name: '24.2.1',
          start: '2024-05-13',
          end: '2024-05-26',
          state: 'Completed',
        }),
      ],
      isLoading: false,
    })

    render(<IterationsCards piKey={1} />)

    expect(screen.queryByText(/Day\s+\d+\/\d+/i)).not.toBeInTheDocument()
  })

  it('clamps the active day to the iteration bounds when the system clock is past the end', () => {
    jest.useFakeTimers().setSystemTime(localDate('2025-01-01').getTime())

    mockQuery.mockReturnValue({
      data: [
        mkIteration({
          key: 3,
          name: '24.2.3',
          start: '2024-06-10',
          end: '2024-06-23',
          state: 'Active',
        }),
      ],
      isLoading: false,
    })

    render(<IterationsCards piKey={1} />)

    expect(
      screen.getByText((content) => /Day\s+14\/14/i.test(content)),
    ).toBeInTheDocument()
  })

  it('renders the category name and date range on each card', () => {
    mockQuery.mockReturnValue({
      data: [
        mkIteration({
          key: 6,
          name: '24.2.6',
          start: '2024-07-22',
          end: '2024-08-04',
          state: 'Future',
          categoryName: 'Innovation & Planning',
        }),
      ],
      isLoading: false,
    })

    render(<IterationsCards piKey={1} />)

    expect(screen.getByText(/Innovation & Planning/i)).toBeInTheDocument()
    expect(
      screen.getByText((_, el) => el?.textContent === 'Jul 22 – Aug 4, 2024'),
    ).toBeInTheDocument()
  })

  it.each([['IP'], ['Innovation & Planning'], ['innovation']])(
    'tints IP iterations (%s) with the warning accent',
    (categoryName) => {
      mockQuery.mockReturnValue({
        data: [
          mkIteration({
            key: 6,
            name: '24.2.6',
            start: '2024-07-22',
            end: '2024-08-04',
            state: 'Future',
            categoryName,
          }),
        ],
        isLoading: false,
      })

      const { container } = render(<IterationsCards piKey={1} />)

      const cardBody = container.querySelector(
        'a .ant-card-body',
      ) as HTMLElement | null
      expect(cardBody?.style.backgroundColor).toBe(
        'var(--ant-color-warning-bg)',
      )
    },
  )

  it('does not tint a Development iteration', () => {
    mockQuery.mockReturnValue({
      data: [
        mkIteration({
          key: 1,
          name: '24.2.1',
          start: '2024-05-13',
          end: '2024-05-26',
          state: 'Future',
          categoryName: 'Development',
        }),
      ],
      isLoading: false,
    })

    const { container } = render(<IterationsCards piKey={1} />)

    const cardBody = container.querySelector(
      'a .ant-card-body',
    ) as HTMLElement | null
    expect(cardBody?.style.backgroundColor).toBe('')
  })

  describe('health flag', () => {
    const setupActive = () => {
      jest.useFakeTimers().setSystemTime(localDate('2024-06-16').getTime())
      mockQuery.mockReturnValue({
        data: [
          mkIteration({
            key: 3,
            name: '24.2.3',
            start: '2024-06-10',
            end: '2024-06-23',
            state: 'Active',
          }),
        ],
        isLoading: false,
      })
    }

    it('does not render a flag for the active card when metrics are still loading', () => {
      setupActive()
      mockMetricsQuery.mockReturnValue({ data: undefined })

      const { container } = render(<IterationsCards piKey={1} />)

      expect(container.querySelector('.anticon-flag')).toBeNull()
    })

    it('does not render a flag for non-active iterations even if metrics come back', () => {
      mockQuery.mockReturnValue({
        data: [
          mkIteration({
            key: 1,
            name: '24.2.1',
            start: '2024-05-13',
            end: '2024-05-26',
            state: 'Completed',
          }),
        ],
        isLoading: false,
      })
      mockMetricsQuery.mockReturnValue({
        data: { totalWorkItems: 10, completedWorkItems: 10 },
      })

      const { container } = render(<IterationsCards piKey={1} />)

      expect(container.querySelector('.anticon-flag')).toBeNull()
    })

    it.each([
      // Day 7/14: ideal remaining = total * (7/14) = 50%.
      // Actual remaining = total - completed.
      // variance% = (actual - ideal) / total * 100
      // On Track (<=10%): completed >= 40% of total (here 4 of 10 → variance = 10%)
      [4, 'var(--ant-color-success)'],
      // At Risk (>10, <=25): completed 3 of 10 → variance = 20%
      [3, 'var(--ant-color-warning)'],
      // Off Track (>25): completed 1 of 10 → variance = 40%
      [1, 'var(--ant-color-error)'],
    ])(
      'colors the flag based on health (completed=%i → %s)',
      (completed, expectedColor) => {
        setupActive()
        mockMetricsQuery.mockReturnValue({
          data: {
            totalWorkItems: 10,
            completedWorkItems: completed,
          },
        })

        const { container } = render(<IterationsCards piKey={1} />)

        const flag = container.querySelector(
          '.anticon-flag',
        ) as HTMLElement | null
        expect(flag).not.toBeNull()
        expect(flag?.style.color).toBe(expectedColor)
      },
    )

    it('fetches metrics scoped to the active iteration', () => {
      setupActive()
      mockMetricsQuery.mockReturnValue({
        data: { totalWorkItems: 10, completedWorkItems: 5 },
      })

      render(<IterationsCards piKey={99} />)

      expect(mockMetricsQuery).toHaveBeenCalledWith({
        planningIntervalKey: 99,
        iterationKey: 3,
      })
    })
  })
})

