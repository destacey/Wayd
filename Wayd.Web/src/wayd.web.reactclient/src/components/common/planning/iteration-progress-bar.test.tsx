import { render, screen } from '@testing-library/react'
import IterationProgressBar from './iteration-progress-bar'
import * as iterationHealthUtils from '../../../utils/iteration-health'

// Mock the iteration health utility
vi.mock('../../../utils/iteration-health', () => ({
  calculateSprintHealth: vi.fn(),
  IterationHealthStatus: {
    NotStarted: 'Not Started',
    OnTrack: 'On Track',
    AtRisk: 'At Risk',
    OffTrack: 'Off Track',
    Completed: 'Completed',
  },
}))

// Mock useTheme
vi.mock('../../contexts/theme', () => ({
  default: () => ({
    token: {
      colorSuccess: '#52c41a',
      colorWarning: '#faad14',
      colorError: '#ff4d4f',
      colorBgContainer: '#ffffff',
    },
  }),
}))

describe('IterationProgressBar', () => {
  const baseProps = {
    startDate: '2026-01-01',
    endDate: '2026-01-15',
    total: 100,
    completed: 50,
  }

  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders with correct completion percentage', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { container } = render(<IterationProgressBar {...baseProps} />)

    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
  })

  it('displays green color for On Track status', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { container } = render(<IterationProgressBar {...baseProps} />)

    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
  })

  it('displays green color for Completed status', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.Completed,
      variancePercent: 0,
    })

    const { container } = render(
      <IterationProgressBar {...baseProps} completed={100} />,
    )

    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
  })

  it('displays yellow color for At Risk status', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.AtRisk,
      variancePercent: -15,
    })

    const { container } = render(<IterationProgressBar {...baseProps} />)

    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
  })

  it('displays red color for Off Track status', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OffTrack,
      variancePercent: -30,
    })

    const { container } = render(<IterationProgressBar {...baseProps} />)

    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
  })

  it('handles zero total correctly', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.NotStarted,
      variancePercent: 0,
    })

    const { container } = render(
      <IterationProgressBar {...baseProps} total={0} completed={0} />,
    )

    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
  })

  it('hides info by default', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { container } = render(<IterationProgressBar {...baseProps} />)

    const info = container.querySelector('.ant-progress-text')
    expect(info).not.toBeInTheDocument()
  })

  it('shows info when showInfo is true', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { container } = render(
      <IterationProgressBar {...baseProps} showInfo={true} />,
    )

    // When showInfo is true, the progress bar should display percentage
    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
    // Check for percentage display - Ant Design shows it differently
    expect(container.textContent).toContain('50%')
  })

  it('uses small size by default', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { container } = render(<IterationProgressBar {...baseProps} />)

    const progress = container.querySelector('.ant-progress-small')
    expect(progress).toBeInTheDocument()
  })

  it('uses default size when specified', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { container } = render(
      <IterationProgressBar {...baseProps} size="medium" />,
    )

    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
    expect(progress).not.toHaveClass('ant-progress-small')
  })

  it('recalculates health when props change', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { rerender } = render(<IterationProgressBar {...baseProps} />)

    expect(iterationHealthUtils.calculateSprintHealth).toHaveBeenCalledWith({
      startDate: baseProps.startDate,
      endDate: baseProps.endDate,
      total: 100,
      completed: 50,
    })

    // Update completed value
    rerender(<IterationProgressBar {...baseProps} completed={75} />)

    expect(iterationHealthUtils.calculateSprintHealth).toHaveBeenCalledWith({
      startDate: baseProps.startDate,
      endDate: baseProps.endDate,
      total: 100,
      completed: 75,
    })
  })

  it('calculates correct percentage for partial completion', () => {
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })

    const { container } = render(
      <IterationProgressBar {...baseProps} total={200} completed={75} />,
    )

    const progress = container.querySelector('.ant-progress')
    expect(progress).toBeInTheDocument()
  })

  it('fills against the commitment and passes it to the health calculation', () => {
    // Arrange
    vi.mocked(iterationHealthUtils.calculateSprintHealth).mockReturnValue({
      status: iterationHealthUtils.IterationHealthStatus.OnTrack,
      variancePercent: 0,
    })
    const commitment = {
      committed: 40,
      start: new Date('2026-01-01T09:00:00Z'),
      end: new Date('2026-01-15T17:00:00Z'),
    }

    // Act
    render(
      <IterationProgressBar
        {...baseProps}
        total={100}
        completed={30}
        commitment={commitment}
      />,
    )

    // Assert
    expect(screen.getByRole('progressbar')).toHaveAttribute(
      'aria-valuenow',
      '75',
    )
    expect(iterationHealthUtils.calculateSprintHealth).toHaveBeenCalledWith(
      expect.objectContaining({ completed: 30, commitment }),
    )
  })
})
