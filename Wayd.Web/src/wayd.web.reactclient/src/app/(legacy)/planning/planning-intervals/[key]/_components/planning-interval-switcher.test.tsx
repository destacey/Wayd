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
const mockUsePathname = jest.fn()

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => mockUsePathname(),
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

describe('PlanningIntervalSwitcher', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUsePathname.mockReturnValue('/planning/planning-intervals/2')
  })

  it('renders the trigger button even before the list has loaded', () => {
    mockQuery.mockReturnValue({ data: undefined })

    render(<PlanningIntervalSwitcher piKey={1} />)

    expect(screen.getByRole('button')).toBeInTheDocument()
  })

  it('skips the query until the dropdown is opened', () => {
    mockQuery.mockReturnValue({ data: undefined })

    render(<PlanningIntervalSwitcher piKey={1} />)

    expect(mockQuery).toHaveBeenCalled()
    const lastCall = mockQuery.mock.calls[mockQuery.mock.calls.length - 1]
    expect(lastCall[1]).toEqual({ skip: true })
  })

  it('enables the query after the dropdown is opened', async () => {
    mockQuery.mockReturnValue({ data: undefined })
    const user = userEvent.setup()

    render(<PlanningIntervalSwitcher piKey={1} />)

    await user.click(screen.getByRole('button'))

    const lastCall = mockQuery.mock.calls[mockQuery.mock.calls.length - 1]
    expect(lastCall[1]).toEqual({ skip: false })
  })

  it('renders each planning interval as a menu option sorted by most recent start date', async () => {
    mockQuery.mockReturnValue({
      data: [
        { key: 1, name: '2025 PI 1', start: '2025-01-01', state: { name: 'Completed' } },
        { key: 2, name: '2026 PI 1', start: '2026-01-01', state: { name: 'Active' } },
        { key: 3, name: '2025 PI 2', start: '2025-06-01', state: { name: 'Completed' } },
      ],
    })
    const user = userEvent.setup()

    render(<PlanningIntervalSwitcher piKey={2} />)
    await user.click(screen.getByRole('button'))

    const options = await screen.findAllByRole('menuitem')
    expect(options.map((o) => o.textContent)).toEqual([
      expect.stringContaining('2026 PI 1'),
      expect.stringContaining('2025 PI 2'),
      expect.stringContaining('2025 PI 1'),
    ])
  })

  it('navigates to the selected PI when an option is clicked', async () => {
    mockQuery.mockReturnValue({
      data: [
        { key: 1, name: '2025 PI 1', start: '2025-01-01', state: { name: 'Completed' } },
        { key: 2, name: '2026 PI 1', start: '2026-01-01', state: { name: 'Active' } },
      ],
    })
    const user = userEvent.setup()

    render(<PlanningIntervalSwitcher piKey={2} />)
    await user.click(screen.getByRole('button'))
    await user.click(await screen.findByText('2025 PI 1'))

    expect(mockPush).toHaveBeenCalledWith('/planning/planning-intervals/1')
  })

  const clickSwitchTo = async (target: string) => {
    mockQuery.mockReturnValue({
      data: [
        { key: 1, name: '2025 PI 1', start: '2025-01-01', state: { name: 'Completed' } },
        { key: 2, name: '2026 PI 1', start: '2026-01-01', state: { name: 'Active' } },
      ],
    })
    const user = userEvent.setup()

    render(<PlanningIntervalSwitcher piKey={2} />)
    await user.click(screen.getByRole('button'))
    await user.click(await screen.findByText(target))
  }

  it.each([
    ['/planning/planning-intervals/2/overview', '/planning/planning-intervals/1/overview'],
    ['/planning/planning-intervals/2/details', '/planning/planning-intervals/1/details'],
    ['/planning/planning-intervals/2/plan-review', '/planning/planning-intervals/1/plan-review'],
    ['/planning/planning-intervals/2/objectives', '/planning/planning-intervals/1/objectives'],
    ['/planning/planning-intervals/2/risks', '/planning/planning-intervals/1/risks'],
    [
      '/planning/planning-intervals/2/objectives/health-report',
      '/planning/planning-intervals/1/objectives/health-report',
    ],
  ])('preserves the top-level sub-path when switching (%s)', async (pathname, expected) => {
    mockUsePathname.mockReturnValue(pathname)

    await clickSwitchTo('2025 PI 1')

    expect(mockPush).toHaveBeenCalledWith(expected)
  })

  it('falls back to the PI root when the sub-path is an unknown or deep detail route', async () => {
    mockUsePathname.mockReturnValue(
      '/planning/planning-intervals/2/objectives/abc-123',
    )

    await clickSwitchTo('2025 PI 1')

    // "objectives" is preserved; the deep segment "abc-123" is dropped so we
    // don't land on a non-existent page on the other PI.
    expect(mockPush).toHaveBeenCalledWith(
      '/planning/planning-intervals/1/objectives',
    )
  })

  it('falls back to the PI root when on an unrecognized sub-route', async () => {
    mockUsePathname.mockReturnValue(
      '/planning/planning-intervals/2/something-else',
    )

    await clickSwitchTo('2025 PI 1')

    expect(mockPush).toHaveBeenCalledWith('/planning/planning-intervals/1')
  })
})
