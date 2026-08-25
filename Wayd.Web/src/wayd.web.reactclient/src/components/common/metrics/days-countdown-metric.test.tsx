jest.mock('../../../utils', () => ({
  daysRemaining: jest.fn(),
  percentageElapsed: jest.fn(),
}))

import { render, screen } from '@testing-library/react'
import DaysCountdownMetric from './days-countdown-metric'
import { daysRemaining, percentageElapsed } from '../../../utils'
import { IterationState } from '../../types'

const mockedDaysRemaining = daysRemaining as jest.MockedFunction<
  typeof daysRemaining
>
const mockedPercentageElapsed = percentageElapsed as jest.MockedFunction<
  typeof percentageElapsed
>

describe('DaysCountdownMetric', () => {
  const futureDate = new Date('2025-12-31')
  const pastDate = new Date('2025-01-01')

  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders "Days Until Start" for future state', () => {
    mockedDaysRemaining.mockReturnValue(30)

    render(
      <DaysCountdownMetric
        state={IterationState.Future}
        startDate={futureDate}
        endDate={pastDate}
      />,
    )

    expect(screen.getByText('Days Until Start')).toBeInTheDocument()
    expect(screen.getByText('30')).toBeInTheDocument()
    expect(screen.getByText('days')).toBeInTheDocument()
    expect(mockedDaysRemaining).toHaveBeenCalledWith(futureDate)
  })

  it('renders "Days Remaining" for active state', () => {
    mockedDaysRemaining.mockReturnValue(15)
    mockedPercentageElapsed.mockReturnValue(50.0)

    render(
      <DaysCountdownMetric
        state={IterationState.Active}
        startDate={pastDate}
        endDate={futureDate}
      />,
    )

    expect(screen.getByText('Days Remaining')).toBeInTheDocument()
    expect(screen.getByText('15')).toBeInTheDocument()
    expect(screen.getByText('days')).toBeInTheDocument()
    expect(mockedDaysRemaining).toHaveBeenCalledWith(futureDate)
    expect(mockedPercentageElapsed).toHaveBeenCalledWith(pastDate, futureDate)
  })

  it('renders nothing for completed state', () => {
    const { container } = render(
      <DaysCountdownMetric
        state={IterationState.Completed}
        startDate={pastDate}
        endDate={futureDate}
      />,
    )

    expect(container.firstChild).toBeNull()
    expect(mockedDaysRemaining).not.toHaveBeenCalled()
  })

  it('uses custom label for future state', () => {
    mockedDaysRemaining.mockReturnValue(45)

    render(
      <DaysCountdownMetric
        state={IterationState.Future}
        startDate={futureDate}
        endDate={pastDate}
        labels={{ future: 'Days Until Launch' }}
      />,
    )

    expect(screen.getByText('Days Until Launch')).toBeInTheDocument()
    expect(screen.getByText('45')).toBeInTheDocument()
  })

  it('uses custom label for active state', () => {
    mockedDaysRemaining.mockReturnValue(7)
    mockedPercentageElapsed.mockReturnValue(75.0)

    render(
      <DaysCountdownMetric
        state={IterationState.Active}
        startDate={pastDate}
        endDate={futureDate}
        labels={{ active: 'Days Left in Sprint' }}
      />,
    )

    expect(screen.getByText('Days Left in Sprint')).toBeInTheDocument()
    expect(screen.getByText('7')).toBeInTheDocument()
  })

  it('handles Date objects for startDate and endDate', () => {
    const startDateObj = new Date('2025-01-01')
    const endDateObj = new Date('2025-12-31')
    mockedDaysRemaining.mockReturnValue(20)
    mockedPercentageElapsed.mockReturnValue(60.0)

    render(
      <DaysCountdownMetric
        state={IterationState.Active}
        startDate={startDateObj}
        endDate={endDateObj}
      />,
    )

    expect(screen.getByText('Days Remaining')).toBeInTheDocument()
    expect(mockedDaysRemaining).toHaveBeenCalledWith(endDateObj)
    expect(mockedPercentageElapsed).toHaveBeenCalledWith(
      startDateObj,
      endDateObj,
    )
  })

  it('recalculates when state changes', () => {
    mockedDaysRemaining.mockReturnValue(10)

    const { rerender } = render(
      <DaysCountdownMetric
        state={IterationState.Future}
        startDate={futureDate}
        endDate={pastDate}
      />,
    )

    expect(screen.getByText('Days Until Start')).toBeInTheDocument()

    mockedDaysRemaining.mockReturnValue(5)
    mockedPercentageElapsed.mockReturnValue(80.0)

    rerender(
      <DaysCountdownMetric
        state={IterationState.Active}
        startDate={pastDate}
        endDate={futureDate}
      />,
    )

    expect(screen.getByText('Days Remaining')).toBeInTheDocument()
  })

  describe('Secondary percentage value', () => {
    it('displays percentage complete for active state', () => {
      const startDate = new Date('2025-01-01T00:00:00')
      const endDate = new Date('2025-01-15T00:00:00')

      mockedDaysRemaining.mockReturnValue(7)
      mockedPercentageElapsed.mockReturnValue(50.0)

      render(
        <DaysCountdownMetric
          state={IterationState.Active}
          startDate={startDate}
          endDate={endDate}
        />,
      )

      expect(screen.getByText('Days Remaining')).toBeInTheDocument()
      expect(screen.getByText('7')).toBeInTheDocument()
      expect(screen.getByText('50%')).toBeInTheDocument()
      expect(mockedPercentageElapsed).toHaveBeenCalledWith(startDate, endDate)
    })

    it('does not display percentage for future state', () => {
      const startDate = new Date('2025-12-01T00:00:00')
      const endDate = new Date('2025-12-15T00:00:00')

      mockedDaysRemaining.mockReturnValue(30)

      const { container } = render(
        <DaysCountdownMetric
          state={IterationState.Future}
          startDate={startDate}
          endDate={endDate}
        />,
      )

      expect(screen.getByText('Days Until Start')).toBeInTheDocument()
      expect(screen.getByText('30')).toBeInTheDocument()

      // No percentage should be displayed for future state
      const meta = container.querySelector('.ant-card-meta')
      expect(meta).not.toBeInTheDocument()
    })

    it('calculates percentage at start of iteration', () => {
      const startDate = new Date('2025-01-01T00:00:00')
      const endDate = new Date('2025-01-11T00:00:00')

      mockedDaysRemaining.mockReturnValue(10)
      mockedPercentageElapsed.mockReturnValue(0.0)

      render(
        <DaysCountdownMetric
          state={IterationState.Active}
          startDate={startDate}
          endDate={endDate}
        />,
      )

      expect(screen.getByText('0%')).toBeInTheDocument()
    })

    it('calculates percentage near end of iteration', () => {
      const startDate = new Date('2025-01-01T00:00:00')
      const endDate = new Date('2025-01-11T00:00:00')

      mockedDaysRemaining.mockReturnValue(1)
      mockedPercentageElapsed.mockReturnValue(90)

      render(
        <DaysCountdownMetric
          state={IterationState.Active}
          startDate={startDate}
          endDate={endDate}
        />,
      )

      expect(screen.getByText('90%')).toBeInTheDocument()
    })

    it('caps percentage at 100% if past end date', () => {
      const startDate = new Date('2025-01-01T00:00:00')
      const endDate = new Date('2025-01-11T00:00:00')

      mockedDaysRemaining.mockReturnValue(-4)
      mockedPercentageElapsed.mockReturnValue(100)

      render(
        <DaysCountdownMetric
          state={IterationState.Active}
          startDate={startDate}
          endDate={endDate}
        />,
      )

      expect(screen.getByText('100%')).toBeInTheDocument()
    })

    it('includes tooltip explaining the percentage', () => {
      const startDate = new Date('2025-01-01T00:00:00')
      const endDate = new Date('2025-01-11T00:00:00')

      mockedDaysRemaining.mockReturnValue(5)
      mockedPercentageElapsed.mockReturnValue(50)

      render(
        <DaysCountdownMetric
          state={IterationState.Active}
          startDate={startDate}
          endDate={endDate}
        />,
      )

      // The tooltip is passed to MetricCard
      // We can't easily test Tooltip content without triggering hover,
      // but we can verify the component renders successfully
      expect(screen.getByText('Days Remaining')).toBeInTheDocument()
      expect(screen.getByText('50%')).toBeInTheDocument()
    })
  })

  it('applies cardStyle to the card, like the other metrics', () => {
    // Arrange / Act — it forwarded a `style` prop that MetricCard does not
    // have, so sizing was silently dropped and this card came out narrower
    // than the rest of its row.
    const { container } = render(
      <DaysCountdownMetric
        state={IterationState.Active}
        startDate={new Date('2026-08-17')}
        endDate={new Date('2026-08-30')}
        cardStyle={{ minWidth: 150 }}
      />,
    )

    // Assert
    const card = container.querySelector('.ant-card') as HTMLElement
    expect(card).toHaveStyle({ minWidth: '150px' })
  })
})
