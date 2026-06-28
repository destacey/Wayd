import { render, screen } from '@testing-library/react'
import type { ItemBox } from '../core/geometry'
import type { TimelineItem } from '../core/types'
import { ItemBar } from './item-bar'

const DAY = 24 * 60 * 60 * 1000

const rangeItem = (overrides: Partial<TimelineItem> = {}): TimelineItem => ({
  id: 'item-1',
  kind: 'range',
  start: 0,
  end: 5 * DAY,
  label: 'Feature window',
  color: '#1677ff',
  ...overrides,
})

const boxFor = (
  item: TimelineItem,
  overrides: Partial<ItemBox> = {},
): ItemBox => ({
  item,
  left: 40,
  top: 12,
  width: 160,
  height: 32,
  ...overrides,
})

describe('ItemBar', () => {
  it('renders one-day ranges as a fixed circle centered in the day span', () => {
    // Arrange
    const item = rangeItem({
      end: DAY,
      label: 'Launch day',
    })

    // Act
    const { container } = render(
      <ItemBar box={boxFor(item)} selected={false} />,
    )

    // Assert
    const marker = container.querySelector('[data-timeline-item]')
    expect(marker).toHaveStyle({
      left: '104px',
      top: '12px',
      width: '32px',
      height: '32px',
    })

    const label = screen.getByText('Launch day')
    expect(label).toHaveStyle({
      left: '140px',
      top: '12px',
      height: '32px',
      lineHeight: '32px',
    })
  })

  it('truncates one-day outside labels while preserving the full tooltip', () => {
    // Arrange
    const item = rangeItem({
      end: DAY,
      label: 'This one-day item name is too long',
    })

    // Act
    const { container } = render(
      <ItemBar box={boxFor(item)} selected={false} />,
    )

    // Assert
    expect(screen.getByText('This one-day item…')).toBeInTheDocument()
    expect(container.querySelector('[data-timeline-item]')).toHaveAttribute(
      'data-tooltip',
      'This one-day item name is too long',
    )
    expect(screen.getByText('This one-day item…')).toHaveAttribute(
      'data-tooltip',
      'This one-day item name is too long',
    )
    expect(container.querySelector('[data-timeline-item]')).not.toHaveAttribute(
      'title',
    )
    expect(screen.getByText('This one-day item…')).not.toHaveAttribute('title')
  })

  it('keeps multi-day ranges at their full timeline width with the label inside', () => {
    // Arrange
    const item = rangeItem()

    // Act
    const { container } = render(
      <ItemBar box={boxFor(item)} selected={false} />,
    )

    // Assert
    const marker = container.querySelector('[data-timeline-item]')
    expect(marker).toHaveStyle({
      left: '40px',
      top: '12px',
      width: '160px',
      height: '32px',
    })
    expect(screen.getByText('Feature window')).toBe(marker?.firstElementChild)
  })

  it('uses explicit tooltips for range bars', () => {
    // Arrange
    const item = rangeItem({
      tooltip: 'Feature window\nJan 1, 2026 - Jan 5, 2026',
    })

    // Act
    const { container } = render(
      <ItemBar box={boxFor(item)} selected={false} />,
    )

    // Assert
    expect(container.querySelector('[data-timeline-item]')).toHaveAttribute(
      'data-tooltip',
      'Feature window\nJan 1, 2026 - Jan 5, 2026',
    )
    expect(container.querySelector('[data-timeline-item]')).toHaveAttribute(
      'aria-label',
      'Feature window\nJan 1, 2026 - Jan 5, 2026',
    )
    expect(container.querySelector('[data-timeline-item]')).not.toHaveAttribute(
      'title',
    )
  })

  it('uses explicit tooltips for milestone markers', () => {
    // Arrange
    const item: TimelineItem = {
      id: 'milestone-1',
      kind: 'milestone',
      start: DAY,
      end: DAY,
      label: 'Launch',
      tooltip: 'Launch\nJan 1, 2026',
    }

    // Act
    const { container } = render(
      <ItemBar box={boxFor(item)} selected={false} />,
    )

    // Assert
    expect(container.querySelector('[data-timeline-item]')).toHaveAttribute(
      'data-tooltip',
      'Launch\nJan 1, 2026',
    )
    expect(container.querySelector('[data-timeline-item]')).toHaveAttribute(
      'aria-label',
      'Launch\nJan 1, 2026',
    )
    expect(container.querySelector('[data-timeline-item]')).not.toHaveAttribute(
      'title',
    )
  })
})
