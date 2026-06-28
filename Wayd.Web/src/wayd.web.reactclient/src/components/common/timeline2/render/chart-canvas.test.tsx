import { act, fireEvent, render, screen } from '@testing-library/react'
import type { TimeScale } from '../core/scale'
import type { TimelineItem } from '../core/types'
import { ChartCanvas } from './chart-canvas'

const DAY = 24 * 60 * 60 * 1000
const scale: TimeScale = {
  width: 500,
  pxPerMs: 500 / (10 * DAY),
  domain: [0, 10 * DAY],
  toX: (ms) => (ms / (10 * DAY)) * 500,
  toMs: (x) => (x / 500) * 10 * DAY,
  ticks: () => [],
  tiers: () => ({ upper: [], lower: [] }),
  weekends: () => [],
}

const rect = (
  left: number,
  top: number,
  width: number,
  height: number,
): DOMRect => ({
  x: left,
  y: top,
  left,
  top,
  width,
  height,
  right: left + width,
  bottom: top + height,
  toJSON: () => ({}),
})

const pointerMove = (
  element: Element,
  { clientX, clientY }: { clientX: number; clientY: number },
) => {
  const event = new Event('pointermove', { bubbles: true })
  Object.defineProperties(event, {
    clientX: { value: clientX },
    clientY: { value: clientY },
  })
  fireEvent(element, event)
}

describe('ChartCanvas', () => {
  beforeEach(() => {
    jest.useFakeTimers()
  })

  afterEach(() => {
    jest.runOnlyPendingTimers()
    jest.useRealTimers()
  })

  it('renders custom background tooltips on hover', () => {
    // Arrange
    const tooltip = 'Planning Timebox\nJan 1, 2026 - Jan 5, 2026'
    const background: TimelineItem = {
      id: 'timebox-1',
      kind: 'background',
      start: 0,
      end: 4 * DAY,
      label: 'Planning Timebox',
      tooltip,
    }

    // Act
    const { container } = render(
      <ChartCanvas
        rows={[]}
        totalHeight={120}
        scale={scale}
        geometry={{ laneHeight: 32, lanePadding: 3, rowPadding: 6 }}
        chartBackgrounds={[background]}
      />,
    )
    const canvas = container.firstElementChild as HTMLElement
    canvas.getBoundingClientRect = jest.fn(() => rect(0, 0, 500, 120))
    container.getBoundingClientRect = jest.fn(() => rect(0, 0, 500, 120))

    // Assert
    const label = screen.getByText('Planning Timebox')
    expect(label).not.toHaveAttribute('title')
    expect(label.parentElement).toHaveAttribute('data-tooltip', tooltip)
    expect(label.parentElement).not.toHaveAttribute('title')

    pointerMove(label, { clientX: 80, clientY: 90 })

    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument()

    act(() => {
      jest.advanceTimersByTime(500)
    })

    expect(screen.getByRole('tooltip')).toHaveTextContent('Planning Timebox')
    expect(screen.getByRole('tooltip')).toHaveTextContent(
      'Jan 1, 2026 - Jan 5, 2026',
    )
  })

  it('positions tooltips from the pointer instead of the hovered item center', () => {
    // Arrange
    const tooltip = 'Long Timebox\nOct 1, 2024 - Dec 15, 2024'
    const background: TimelineItem = {
      id: 'timebox-1',
      kind: 'background',
      start: 0,
      end: 10 * DAY,
      label: 'Long Timebox',
      tooltip,
    }

    const { container } = render(
      <ChartCanvas
        rows={[]}
        totalHeight={160}
        scale={scale}
        geometry={{ laneHeight: 32, lanePadding: 3, rowPadding: 6 }}
        chartBackgrounds={[background]}
      />,
    )
    const canvas = container.firstElementChild as HTMLElement
    canvas.getBoundingClientRect = jest.fn(() => rect(0, 0, 500, 160))
    container.getBoundingClientRect = jest.fn(() => rect(0, 0, 500, 160))

    // Act
    pointerMove(screen.getByText('Long Timebox'), {
      clientX: 80,
      clientY: 90,
    })

    act(() => {
      jest.advanceTimersByTime(500)
    })

    // Assert
    expect(screen.getByRole('tooltip')).toHaveStyle({
      left: '92px',
      top: '30px',
    })
  })

  it('cancels the pending tooltip when the pointer leaves before the delay', () => {
    // Arrange
    const background: TimelineItem = {
      id: 'timebox-1',
      kind: 'background',
      start: 0,
      end: 4 * DAY,
      label: 'Planning Timebox',
      tooltip: 'Planning Timebox\nJan 1, 2026 - Jan 5, 2026',
    }

    const { container } = render(
      <ChartCanvas
        rows={[]}
        totalHeight={120}
        scale={scale}
        geometry={{ laneHeight: 32, lanePadding: 3, rowPadding: 6 }}
        chartBackgrounds={[background]}
      />,
    )
    const canvas = container.firstElementChild as HTMLElement
    canvas.getBoundingClientRect = jest.fn(() => rect(0, 0, 500, 120))
    container.getBoundingClientRect = jest.fn(() => rect(0, 0, 500, 120))

    // Act
    pointerMove(screen.getByText('Planning Timebox'), {
      clientX: 80,
      clientY: 90,
    })
    fireEvent.pointerLeave(canvas)
    act(() => {
      jest.advanceTimersByTime(500)
    })

    // Assert
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument()
  })
})
