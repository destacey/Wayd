import { render, screen } from '@testing-library/react'
import IterationHealthIndicator from './iteration-health-indicator'
import * as iterationHealthUtils from '../../../utils/iteration-health'

// Mock the iteration health utility
jest.mock('../../../utils/iteration-health', () => ({
  calculateSprintHealth: jest.fn(),
  IterationHealthStatus: {
    NotStarted: 'Not Started',
    OnTrack: 'On Track',
    AtRisk: 'At Risk',
    OffTrack: 'Off Track',
    Completed: 'Completed',
  },
}))

describe('IterationHealthIndicator', () => {
  const baseProps = {
    startDate: '2026-01-01',
    endDate: '2026-01-15',
    total: 100,
    completed: 50,
  }

  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders with On Track status', () => {
    jest.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    render(<IterationHealthIndicator {...baseProps} />)

    expect(screen.getByText('On Track')).toBeInTheDocument()
  })

  it('renders with At Risk status', () => {
    jest.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.AtRisk,
      variancePercent: -15,
    })

    render(<IterationHealthIndicator {...baseProps} />)

    expect(screen.getByText('At Risk')).toBeInTheDocument()
  })

  it('renders with Off Track status', () => {
    jest.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OffTrack,
      variancePercent: -30,
    })

    render(<IterationHealthIndicator {...baseProps} />)

    expect(screen.getByText('Off Track')).toBeInTheDocument()
  })

  it('renders with Not Started status', () => {
    jest.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.NotStarted,
      variancePercent: 0,
    })

    render(<IterationHealthIndicator {...baseProps} />)

    expect(screen.getByText('Not Started')).toBeInTheDocument()
  })

  it('renders with Completed status', () => {
    jest.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.Completed,
      variancePercent: 0,
    })

    render(<IterationHealthIndicator {...baseProps} />)

    expect(screen.getByText('Completed')).toBeInTheDocument()
  })

  it('hides label when showLabel is false', () => {
    jest.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    render(<IterationHealthIndicator {...baseProps} showLabel={false} />)

    expect(screen.queryByText('On Track')).not.toBeInTheDocument()
  })

  it('renders with tooltip wrapper', () => {
    jest.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { container } = render(<IterationHealthIndicator {...baseProps} />)

    // Tooltip is wrapped in a span, check for the badge component
    const badge = container.querySelector('.ant-badge')
    expect(badge).toBeInTheDocument()
  })

  it('recalculates health when props change', () => {
    jest.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { rerender } = render(<IterationHealthIndicator {...baseProps} />)

    expect(iterationHealthUtils.calculateSprintHealth).toHaveBeenCalledWith({
      startDate: baseProps.startDate,
      endDate: baseProps.endDate,
      total: 100,
      completed: 50,
    })

    // Update completed value
    rerender(<IterationHealthIndicator {...baseProps} completed={75} />)

    expect(iterationHealthUtils.calculateSprintHealth).toHaveBeenCalledWith({
      startDate: baseProps.startDate,
      endDate: baseProps.endDate,
      total: 100,
      completed: 75,
    })
  })
})
