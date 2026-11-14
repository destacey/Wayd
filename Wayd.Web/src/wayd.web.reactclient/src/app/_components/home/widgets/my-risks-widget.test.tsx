import type { Mock } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import MyRisksWidget, {
  countFollowUpsDue,
  RiskFollowUpsTile,
  sortByFollowUp,
} from './my-risks-widget'
import { HomeContextResponse, RiskListDto } from '@/src/services/wayd-api'
import { tileValueStyle } from '../attention-tile-test-utils'
import { homeContext } from '../home-context-test-utils'
import { renderReportingWidget } from '../widget-display-test-utils'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ user: { username: 'alex@example.com' } }),
}))

vi.mock('@/src/store/features/planning/risks-api', () => ({
  useGetMyRisksQuery: vi.fn(),
}))

vi.mock(import('@/src/utils'), async (importOriginal) => ({
  ...(await importOriginal()),
  todayCalendarDate: () => '2026-11-08',
}))

import { useGetMyRisksQuery } from '@/src/store/features/planning/risks-api'

const mockRisks = useGetMyRisksQuery as Mock

const risk = (
  key: number,
  summary: string,
  followUpDate?: string,
): RiskListDto => ({
  id: `risk-${key}`,
  key,
  summary,
  reportedOn: new Date('2026-10-01T00:00:00Z'),
  status: 'Open',
  category: 'Delivery',
  exposure: 'High',
  followUpDate,
})

const loaded = (risks: RiskListDto[]) => ({
  data: risks,
  isLoading: false,
  isError: false,
})

describe('countFollowUpsDue', () => {
  it('counts follow-ups due today or past, not future or undated ones', () => {
    // Arrange
    const risks = [
      risk(1, 'Past', '2026-11-01'),
      risk(2, 'Today', '2026-11-08'),
      risk(3, 'Tomorrow', '2026-11-09'),
      risk(4, 'Undated'),
    ]

    // Act
    const count = countFollowUpsDue(risks, '2026-11-08')

    // Assert
    expect(count).toBe(2)
  })
})

describe('sortByFollowUp', () => {
  it('puts the soonest follow-up first and undated risks last, by summary', () => {
    // Arrange
    const risks = [
      risk(1, 'zeta undated'),
      risk(2, 'Late', '2026-12-01'),
      risk(3, 'Alpha undated'),
      risk(4, 'Soon', '2026-11-02'),
    ]

    // Act
    const sorted = sortByFollowUp(risks)

    // Assert
    expect(sorted.map((r) => r.summary)).toEqual([
      'Soon',
      'Late',
      'Alpha undated',
      'zeta undated',
    ])
  })
})

describe('MyRisksWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockHome.context = undefined
  })

  it('asks for the risks while the home context is not known', () => {
    // Arrange
    mockRisks.mockReturnValue(loaded([]))

    // Act
    render(<MyRisksWidget title="My risks" />)

    // Assert
    expect(mockRisks).toHaveBeenCalledWith('alex@example.com', { skip: false })
  })

  it('skips the request when the home context says none are assigned', () => {
    // Arrange
    mockHome.context = homeContext({ assignedRiskCount: 0 })
    mockRisks.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<MyRisksWidget title="My risks" />)

    // Assert
    expect(mockRisks).toHaveBeenCalledWith('alex@example.com', { skip: true })
    expect(
      screen.getByText(/No open risks are assigned to you/),
    ).toBeInTheDocument()
  })

  it('says how it fills when no risk is assigned', () => {
    // Arrange
    mockRisks.mockReturnValue(loaded([]))

    // Act
    render(<MyRisksWidget title="My risks" />)

    // Assert
    expect(
      screen.getByText(/No open risks are assigned to you/),
    ).toBeInTheDocument()
  })

  it('lists risks soonest follow-up first, marking past due, today and later', () => {
    // Arrange — served latest first, so the widget has to sort
    mockRisks.mockReturnValue(
      loaded([
        risk(14, 'Capacity', '2026-11-20'),
        risk(13, 'Licensing', '2026-11-08'),
        risk(12, 'Vendor slips', '2026-11-03'),
      ]),
    )

    // Act
    render(<MyRisksWidget title="My risks" />)

    // Assert
    const items = screen.getAllByRole('listitem')
    expect(
      within(items[0]).getByRole('link', { name: 'Vendor slips' }),
    ).toHaveAttribute('href', '/planning/risks/12')
    expect(within(items[0]).getByText(/past due/)).toBeInTheDocument()
    expect(within(items[1]).getByText(/Licensing/)).toBeInTheDocument()
    expect(within(items[1]).getByText(/\(today\)/)).toBeInTheDocument()
    expect(within(items[2]).getByText(/Capacity/)).toBeInTheDocument()
    expect(
      within(items[2]).queryByText(/past due|today/),
    ).not.toBeInTheDocument()
  })

  it('draws a skeleton while the risks load', () => {
    // Arrange
    mockRisks.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    })

    // Act
    const { container } = render(<MyRisksWidget title="My risks" />)

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    expect(screen.queryByText(/No open risks/)).not.toBeInTheDocument()
  })

  describe('reported state', () => {
    it('reports loading while the risks load', () => {
      // Arrange
      mockRisks.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
      })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyRisksWidget title="My risks" />,
      )

      // Assert
      expect(reportedState()).toBe('loading')
    })

    it('reports failed, though there is no data, when the query errors', () => {
      // Arrange
      mockRisks.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
      })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyRisksWidget title="My risks" />,
      )

      // Assert
      expect(reportedState()).toBe('failed')
    })

    it('reports empty when no risk is assigned', () => {
      // Arrange
      mockRisks.mockReturnValue(loaded([]))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyRisksWidget title="My risks" />,
      )

      // Assert
      expect(reportedState()).toBe('empty')
    })

    it('reports empty when the home context says none are assigned and the query is skipped', () => {
      // Arrange
      mockHome.context = homeContext({ assignedRiskCount: 0 })
      mockRisks.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: false,
      })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyRisksWidget title="My risks" />,
      )

      // Assert
      expect(mockRisks).toHaveBeenCalledWith('alex@example.com', { skip: true })
      expect(reportedState()).toBe('empty')
    })

    it('reports rows when a risk is assigned', () => {
      // Arrange
      mockRisks.mockReturnValue(loaded([risk(1, 'Vendor slips')]))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyRisksWidget title="My risks" />,
      )

      // Assert
      expect(reportedState()).toBe('rows')
    })
  })
})

describe('RiskFollowUpsTile', () => {
  it('counts follow-ups due in the warning colour', () => {
    // Arrange
    mockRisks.mockReturnValue(
      loaded([
        risk(1, 'Past', '2026-11-01'),
        risk(2, 'Today', '2026-11-08'),
        risk(3, 'Later', '2026-12-01'),
      ]),
    )

    // Act
    const { container } = render(<RiskFollowUpsTile title="Risk follow-ups" />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Risk follow-ups: 2' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-warning)')
  })

  it('leaves no follow-ups due uncoloured', () => {
    // Arrange
    mockRisks.mockReturnValue(loaded([risk(3, 'Later', '2026-12-01')]))

    // Act
    const { container } = render(<RiskFollowUpsTile title="Risk follow-ups" />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Risk follow-ups: 0' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).not.toContain('var(--ant-color')
  })
})
