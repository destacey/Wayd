import { render, screen } from '@testing-library/react'
import { WaydTimeline2 } from './wayd-timeline2'
import type { TimelineItem } from './core/types'

// jsdom has no ResizeObserver; the chart body's callback ref constructs one.
beforeAll(() => {
  global.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver
})

// The loading/empty branches return before the chart body mounts, so the heavy
// dependencies (Splitter layout, ResizeObserver, html2canvas) are never reached.

const DAY = 86_400_000
const windowStart = 0
const windowEnd = 30 * DAY

const item = (id: string): TimelineItem => ({
  id,
  kind: 'range',
  start: 2 * DAY,
  end: 5 * DAY,
})

describe('WaydTimeline2 — loading & empty states', () => {
  it('renders a spinner while loading with no data', () => {
    // Arrange / Act
    const { container } = render(
      <WaydTimeline2
        items={[]}
        windowStart={windowStart}
        windowEnd={windowEnd}
        isLoading
      />,
    )
    // Assert — antd Spin is present (so it's not a blank zero-height chart).
    expect(container.querySelector('.ant-spin')).toBeInTheDocument()
  })

  it('renders the empty message when not loading and there are no items', () => {
    // Arrange / Act
    render(
      <WaydTimeline2
        items={[]}
        windowStart={windowStart}
        windowEnd={windowEnd}
        emptyMessage="Nothing here"
      />,
    )
    // Assert
    expect(screen.getByText('Nothing here')).toBeInTheDocument()
  })

  it('does not show the spinner once items are present, even while loading', () => {
    // Arrange / Act — loading is true but data has arrived (refetch case).
    const { container } = render(
      <WaydTimeline2
        items={[item('a')]}
        windowStart={windowStart}
        windowEnd={windowEnd}
        isLoading
      />,
    )
    // Assert — the loading early-return is skipped; the chart renders instead.
    expect(container.querySelector('.ant-spin')).not.toBeInTheDocument()
  })
})
