import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

// jsdom does not implement scrollIntoView, which IconMenu calls on open.
Element.prototype.scrollIntoView = jest.fn()

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

const mockPush = jest.fn()
let mockSearchParams = new URLSearchParams()

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  useSearchParams: () => mockSearchParams,
}))

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({
    token: {
      colorTextQuaternary: 'rgba(0, 0, 0, 0.25)',
      colorBgElevated: '#ffffff',
    },
  }),
}))

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalsQuery: jest.fn(),
}))

import { useGetPlanningIntervalsQuery } from '@/src/store/features/planning/planning-interval-api'
import PlanningIntervalSwitcher from './planning-interval-switcher'

const mockQuery = useGetPlanningIntervalsQuery as unknown as jest.Mock

const twoPlanningIntervals = [
  {
    key: 1,
    name: '2025 PI 1',
    start: '2025-01-01',
    state: { name: 'Completed' },
  },
  { key: 2, name: '2026 PI 1', start: '2026-01-01', state: { name: 'Active' } },
]

describe('PlanningIntervalSwitcher', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockSearchParams = new URLSearchParams()
  })

  it('renders the trigger button even before the list has loaded', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: undefined })

    // Act
    render(<PlanningIntervalSwitcher piKey={1} />)

    // Assert
    expect(screen.getByRole('button')).toBeInTheDocument()
  })

  it('skips the query until the dropdown is opened', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: undefined })

    // Act
    render(<PlanningIntervalSwitcher piKey={1} />)

    // Assert
    const lastCall = mockQuery.mock.calls[mockQuery.mock.calls.length - 1]
    expect(lastCall[1]).toEqual({ skip: true })
  })

  it('enables the query after the dropdown is opened', async () => {
    // Arrange
    mockQuery.mockReturnValue({ data: undefined })
    const user = userEvent.setup()
    render(<PlanningIntervalSwitcher piKey={1} />)

    // Act
    await user.click(screen.getByRole('button'))

    // Assert
    const lastCall = mockQuery.mock.calls[mockQuery.mock.calls.length - 1]
    expect(lastCall[1]).toEqual({ skip: false })
  })

  it('renders each planning interval as a menu option sorted by most recent start date', async () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [
        ...twoPlanningIntervals,
        {
          key: 3,
          name: '2025 PI 2',
          start: '2025-06-01',
          state: { name: 'Completed' },
        },
      ],
    })
    const user = userEvent.setup()
    render(<PlanningIntervalSwitcher piKey={2} />)

    // Act
    await user.click(screen.getByRole('button'))

    // Assert
    const options = await screen.findAllByRole('menuitem')
    expect(options.map((o) => o.textContent)).toEqual([
      expect.stringContaining('2026 PI 1'),
      expect.stringContaining('2025 PI 2'),
      expect.stringContaining('2025 PI 1'),
    ])
  })

  const switchToOldestPi = async () => {
    mockQuery.mockReturnValue({ data: twoPlanningIntervals })
    const user = userEvent.setup()

    render(<PlanningIntervalSwitcher piKey={2} />)
    await user.click(screen.getByRole('button'))
    await user.click(await screen.findByText('2025 PI 1'))
  }

  it('navigates to the selected PI when an option is clicked', async () => {
    // Arrange / Act
    await switchToOldestPi()

    // Assert
    expect(mockPush).toHaveBeenCalledWith('/planning/planning-intervals/1')
  })

  it('stays on the same section when switching', async () => {
    // Arrange
    mockSearchParams = new URLSearchParams('section=plan-review')

    // Act
    await switchToOldestPi()

    // Assert
    expect(mockPush).toHaveBeenCalledWith(
      '/planning/planning-intervals/1?section=plan-review',
    )
  })

  it('drops params that name this PI’s own data', async () => {
    // Arrange — the team on Plan Review belongs to the PI being left, so
    // carrying it over would select a team the next PI may not have.
    mockSearchParams = new URLSearchParams('section=plan-review&team=alpha')

    // Act
    await switchToOldestPi()

    // Assert
    expect(mockPush).toHaveBeenCalledWith(
      '/planning/planning-intervals/1?section=plan-review',
    )
  })
})
