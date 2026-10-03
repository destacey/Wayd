import { render, screen } from '@testing-library/react'
import TimelineProgress from './timeline-progress'

// Mock Ant Design Grid useBreakpoint hook
jest.mock('antd', () => {
  const actualAntd = jest.requireActual('antd')
  return {
    ...actualAntd,
    Grid: {
      ...actualAntd.Grid,
      useBreakpoint: jest.fn(() => ({
        xs: true,
        sm: true,
        md: true, // Default to desktop (md and above)
        lg: true,
        xl: true,
        xxl: true,
      })),
    },
  }
})

// Get reference to the mocked function after module is loaded
const { Grid } = jest.requireMock<typeof import('antd')>('antd')
const mockUseBreakpoint = Grid.useBreakpoint as jest.MockedFunction<
  typeof Grid.useBreakpoint
>

// Real dayjs for parsing and formatting; the global mock only stubs format.
jest.unmock('dayjs')

// Only Date is faked, so antd's timers still run.
const setNow = (value: string) => {
  jest.useFakeTimers({
    doNotFake: [
      'hrtime',
      'nextTick',
      'performance',
      'queueMicrotask',
      'requestAnimationFrame',
      'cancelAnimationFrame',
      'requestIdleCallback',
      'cancelIdleCallback',
      'setImmediate',
      'clearImmediate',
      'setInterval',
      'clearInterval',
      'setTimeout',
      'clearTimeout',
    ],
  })
  jest.setSystemTime(new Date(value))
}

describe('TimelineProgress', () => {
  afterEach(() => {
    jest.useRealTimers()
  })

  const startDate = '2025-10-26'
  const endDate = '2025-11-08'

  beforeEach(() => {
    // Set "now" to Nov 1, which is day 7 of 14 (50%)
    setNow('2025-11-01T12:00:00')

    // Reset the mock to default desktop breakpoints
    mockUseBreakpoint.mockReturnValue({
      xs: true,
      sm: true,
      md: true, // Default to desktop (md and above)
      lg: true,
      xl: true,
      xxl: true,
    })
  })

  it('renders title, dates, and progress info', () => {
    render(<TimelineProgress start={startDate} end={endDate} />)

    expect(screen.getByText('Timeline')).toBeInTheDocument()
    expect(screen.getByText('Oct 26')).toBeInTheDocument()
    expect(screen.getByText('Nov 8')).toBeInTheDocument()
    expect(screen.getByText('Day 7 of 14 (50%)')).toBeInTheDocument()
  })

  it('renders progress bar', () => {
    const { container } = render(
      <TimelineProgress start={startDate} end={endDate} />,
    )

    const progressBar = container.querySelector('.ant-progress')
    expect(progressBar).toBeInTheDocument()
  })

  it('returns null when start date is null', () => {
    const { container } = render(
      <TimelineProgress start={null} end={endDate} />,
    )

    expect(container.firstChild).toBeNull()
  })

  it('returns null when end date is null', () => {
    const { container } = render(
      <TimelineProgress start={startDate} end={null} />,
    )

    expect(container.firstChild).toBeNull()
  })

  it('returns null when both dates are null', () => {
    const { container } = render(<TimelineProgress start={null} end={null} />)

    expect(container.firstChild).toBeNull()
  })

  it('calculates total days correctly (inclusive)', () => {
    render(<TimelineProgress start={startDate} end={endDate} />)

    // Oct 26 to Nov 8 is 14 calendar days
    expect(screen.getByText(/of 14/)).toBeInTheDocument()
  })

  it('handles single day duration', () => {
    const sameDate = '2025-11-01'
    render(<TimelineProgress start={sameDate} end={sameDate} />)

    expect(screen.getByText('Day 1 of 1 (100%)')).toBeInTheDocument()
  })

  it('renders future timeline with 0% progress and starts-in text', () => {
    // Set "now" to 6 days before the start date
    setNow('2025-10-20T12:00:00')

    render(<TimelineProgress start={startDate} end={endDate} />)

    expect(screen.getByText('Timeline')).toBeInTheDocument()
    expect(screen.getByText('Oct 26')).toBeInTheDocument()
    expect(screen.getByText('Nov 8')).toBeInTheDocument()
    expect(
      screen.getByText('Starts in 6 days · 14 day project'),
    ).toBeInTheDocument()
  })

  it('renders singular day for future timeline starting tomorrow', () => {
    // Set "now" to 1 day before the start date
    setNow('2025-10-25T12:00:00')

    render(<TimelineProgress start={startDate} end={endDate} />)

    expect(
      screen.getByText('Starts in 1 day · 14 day project'),
    ).toBeInTheDocument()
  })

  it('clamps current day to total when after end date', () => {
    // Set "now" to after the end date
    setNow('2025-11-15T12:00:00')

    render(<TimelineProgress start={startDate} end={endDate} />)

    expect(screen.getByText('Day 14 of 14 (100%)')).toBeInTheDocument()
  })

  it('calculates progress at start of timeline', () => {
    // Set "now" to start date
    setNow('2025-10-26T12:00:00')

    render(<TimelineProgress start={startDate} end={endDate} />)

    expect(screen.getByText('Day 1 of 14 (7%)')).toBeInTheDocument()
  })

  it('calculates progress at end of timeline', () => {
    // Set "now" to end date
    setNow('2025-11-08T12:00:00')

    render(<TimelineProgress start={startDate} end={endDate} />)

    expect(screen.getByText('Day 14 of 14 (100%)')).toBeInTheDocument()
  })

  it('uses custom dateFormat when provided', () => {
    render(
      <TimelineProgress
        start={startDate}
        end={endDate}
        dateFormat="MMM D, YYYY"
      />,
    )

    expect(screen.getByText('Oct 26, 2025')).toBeInTheDocument()
    expect(screen.getByText('Nov 8, 2025')).toBeInTheDocument()
  })

  it('uses default date format without time', () => {
    render(<TimelineProgress start={startDate} end={endDate} />)

    // Default format is 'MMM D'
    expect(screen.getByText('Oct 26')).toBeInTheDocument()
    expect(screen.getByText('Nov 8')).toBeInTheDocument()
  })

  it('applies default minWidth style on desktop', () => {
    const { container } = render(
      <TimelineProgress start={startDate} end={endDate} />,
    )

    const card = container.querySelector('.ant-card')
    expect(card).toHaveStyle({ minWidth: '275px', width: 'fit-content' })
  })

  it('applies full width on mobile', () => {
    // Mock mobile breakpoint (md is false when screen is < 768px)
    mockUseBreakpoint.mockReturnValueOnce({
      xs: true,
      sm: true,
      md: false, // Mobile/tablet
      lg: false,
      xl: false,
      xxl: false,
    })

    const { container } = render(
      <TimelineProgress start={startDate} end={endDate} />,
    )

    const card = container.querySelector('.ant-card')
    expect(card).toHaveStyle({ width: '100%' })
  })

  it('applies custom styles', () => {
    const { container } = render(
      <TimelineProgress
        start={startDate}
        end={endDate}
        style={{ width: '100%', padding: '10px' }}
      />,
    )

    const card = container.querySelector('.ant-card')
    expect(card).toHaveStyle({ width: '100%' })
    expect(card).toHaveStyle({ padding: '10px' })
  })

  it('uses fontSize 11 when size is small', () => {
    render(<TimelineProgress start={startDate} end={endDate} size="small" />)

    const textElements = screen.getAllByText(/Timeline|Oct 26|Nov 8|Day/)
    textElements.forEach((el) => {
      expect(el).toHaveStyle({ fontSize: '11px' })
    })
  })

  it('uses fontSize 12 by default', () => {
    render(<TimelineProgress start={startDate} end={endDate} />)

    const textElements = screen.getAllByText(/Oct 26|Nov 8|Day/)
    textElements.forEach((el) => {
      expect(el).toHaveStyle({ fontSize: '12px' })
    })
  })

  it('custom styles override default styles', () => {
    const { container } = render(
      <TimelineProgress
        start={startDate}
        end={endDate}
        style={{ minWidth: '500px' }}
      />,
    )

    const card = container.querySelector('.ant-card')
    expect(card).toHaveStyle({ minWidth: '500px' })
  })

  it('does not render a card when borderless', () => {
    const { container } = render(
      <TimelineProgress start={startDate} end={endDate} variant="borderless" />,
    )

    expect(container.querySelector('.ant-card')).toBeNull()
  })

  it('applies custom style to the flex wrapper when borderless', () => {
    const { container } = render(
      <TimelineProgress
        start={startDate}
        end={endDate}
        variant="borderless"
        style={{ width: '100%' }}
      />,
    )

    const flex = container.firstChild as HTMLElement
    expect(flex).toHaveStyle({ width: '100%' })
  })

  it('renders a card when outlined', () => {
    const { container } = render(
      <TimelineProgress start={startDate} end={endDate} variant="outlined" />,
    )

    expect(container.querySelector('.ant-card')).toBeInTheDocument()
  })
})
