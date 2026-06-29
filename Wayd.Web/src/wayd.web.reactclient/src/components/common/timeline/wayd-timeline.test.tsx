import { render, screen } from '@testing-library/react'
import { WaydTimeline } from './wayd-timeline'
import type { TimelineItem } from './core/types'

// jsdom has no ResizeObserver; the chart body's callback ref constructs one.
beforeAll(() => {
  global.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver
})

// The loading branch returns before the chart body mounts, so the heavy
// dependencies (Splitter layout, ResizeObserver, html2canvas) are not reached
// until the timeline has either data or an in-frame empty state.

const DAY = 86_400_000
const windowStart = 0
const windowEnd = 30 * DAY

const item = (id: string): TimelineItem => ({
  id,
  kind: 'range',
  start: 2 * DAY,
  end: 5 * DAY,
})

describe('WaydTimeline — loading & empty states', () => {
  it('renders a spinner while loading with no data', () => {
    // Arrange / Act
    const { container } = render(
      <WaydTimeline
        items={[]}
        windowStart={windowStart}
        windowEnd={windowEnd}
        isLoading
      />,
    )
    // Assert — antd Spin is present (so it's not a blank zero-height chart).
    expect(container.querySelector('.ant-spin')).toBeInTheDocument()
  })

  it('keeps the timeline frame mounted when not loading and there are no items', () => {
    // Arrange / Act
    render(
      <WaydTimeline
        items={[]}
        windowStart={windowStart}
        windowEnd={windowEnd}
        emptyMessage="Nothing here"
        toolbarLeftSlot={<span>Timeline tools</span>}
        footerSlot={<div>Timeline footer</div>}
      />,
    )
    // Assert
    expect(screen.getByText('Timeline tools')).toBeInTheDocument()
    expect(screen.getByText('Nothing here')).toBeInTheDocument()
    expect(screen.getByText('Timeline footer')).toBeInTheDocument()
  })

  it('does not show the spinner once items are present, even while loading', () => {
    // Arrange / Act — loading is true but data has arrived (refetch case).
    const { container } = render(
      <WaydTimeline
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
