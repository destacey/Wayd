import { render, screen } from '@testing-library/react'
import React from 'react'

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

jest.mock('antd', () => {
  const actual = jest.requireActual('antd')

  const renderMenuItems = (
    items: Array<{ key?: string; label?: React.ReactNode; children?: any[] }> = [],
    selectedKeys: string[] = [],
  ): React.ReactNode =>
    items.map((item) => {
      if (item.children?.length) {
        return (
          <li key={item.key}>
            <span>{item.label}</span>
            <ul>{renderMenuItems(item.children, selectedKeys)}</ul>
          </li>
        )
      }

      const selected = !!item.key && selectedKeys.includes(item.key)
      return (
        <li
          key={item.key}
          className={selected ? 'ant-menu-item-selected' : undefined}
        >
          {item.label}
        </li>
      )
    })

  return {
    ...actual,
    Menu: ({
      items,
      selectedKeys,
    }: {
      items?: Array<{ key?: string; label?: React.ReactNode; children?: any[] }>
      selectedKeys?: string[]
    }) => (
      <ul data-testid="mock-menu">
        {renderMenuItems(items ?? [], selectedKeys ?? [])}
      </ul>
    ),
  }
})

const mockUsePathname = jest.fn()

jest.mock('next/navigation', () => ({
  usePathname: () => mockUsePathname(),
}))

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalQuery: jest.fn(),
}))

jest.mock('./planning-interval-switcher', () => ({
  __esModule: true,
  default: () => <div data-testid="pi-switcher" />,
}))

jest.mock('@/src/components/common/planning', () => ({
  IterationStateTag: ({ state }: { state: number }) => (
    <span data-testid="pi-state-tag">{String(state)}</span>
  ),
}))

import { useGetPlanningIntervalQuery } from '@/src/store/features/planning/planning-interval-api'
import PlanningIntervalNav from './planning-interval-nav'

const mockQuery = useGetPlanningIntervalQuery as unknown as jest.Mock

const renderNav = (pathname: string, piKey = 42) => {
  mockUsePathname.mockReturnValue(pathname)
  mockQuery.mockReturnValue({
    data: { name: '2026 PI 1', state: { id: 2, name: 'Active' } },
  })
  return render(<PlanningIntervalNav piKey={piKey} />)
}

describe('PlanningIntervalNav', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders the planning interval name label', () => {
    renderNav('/planning/planning-intervals/42')

    expect(
      screen.getByText(/Planning Interval:\s*2026 PI 1/),
    ).toBeInTheDocument()
  })

  it('renders the PI switcher', () => {
    renderNav('/planning/planning-intervals/42')

    expect(screen.getByTestId('pi-switcher')).toBeInTheDocument()
  })

  it('renders the iteration state tag when state is present', () => {
    renderNav('/planning/planning-intervals/42')

    expect(screen.getByTestId('pi-state-tag')).toHaveTextContent('2')
  })

  it('does not render the state tag before the planning interval data loads', () => {
    mockUsePathname.mockReturnValue('/planning/planning-intervals/42')
    mockQuery.mockReturnValue({ data: undefined })

    render(<PlanningIntervalNav piKey={42} />)

    expect(screen.queryByTestId('pi-state-tag')).not.toBeInTheDocument()
  })

  it('renders links to each nav destination', () => {
    renderNav('/planning/planning-intervals/42')

    expect(screen.getByRole('link', { name: 'Overview' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/42/overview',
    )
    expect(screen.getByRole('link', { name: 'Details' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/42/details',
    )
    expect(screen.getByRole('link', { name: 'Plan Review' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/42/plan-review',
    )
    expect(screen.getByRole('link', { name: 'Objectives' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/42/objectives',
    )
    expect(screen.getByRole('link', { name: 'Risks' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/42/risks',
    )
  })

  const findSelectedMenuItem = () =>
    document.querySelector('.ant-menu-item-selected')

  it.each([
    ['/planning/planning-intervals/42/overview', 'Overview'],
    ['/planning/planning-intervals/42/details', 'Details'],
    ['/planning/planning-intervals/42/plan-review', 'Plan Review'],
    ['/planning/planning-intervals/42/objectives', 'Objectives'],
    ['/planning/planning-intervals/42/objectives/5', 'Objectives'],
    ['/planning/planning-intervals/42/risks', 'Risks'],
    ['/planning/planning-intervals/42/risks/7', 'Risks'],
  ])('highlights the %s tab', (pathname, label) => {
    renderNav(pathname)

    const selected = findSelectedMenuItem()
    expect(selected).not.toBeNull()
    expect(selected?.textContent).toContain(label)
  })

  it('does not highlight any tab on the root route before redirect', () => {
    renderNav('/planning/planning-intervals/42')

    expect(findSelectedMenuItem()).toBeNull()
  })

  it('does not highlight any tab for unknown routes', () => {
    renderNav('/planning/planning-intervals/42/something-else')

    expect(findSelectedMenuItem()).toBeNull()
  })
})
