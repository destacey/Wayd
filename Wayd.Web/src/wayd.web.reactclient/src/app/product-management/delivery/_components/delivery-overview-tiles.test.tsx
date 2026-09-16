import { CutToReleasedDto, ReleaseFrequencyDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import DeliveryOverviewTiles from './delivery-overview-tiles'

/**
 * Overrides admit `null` where the generated DTO says `number | undefined`.
 *
 * NSwag maps a C# `double?` to an optional number, but the API sends JSON `null` for "no earlier
 * window" — so null is what the component actually receives and what these tests must supply.
 */
const frequency = (
  overrides: Partial<Omit<ReleaseFrequencyDto, 'previousPerWeek'>> & {
    previousPerWeek?: number | null
  } = {},
): ReleaseFrequencyDto =>
  ({
    count: 14,
    windowDays: 14,
    perWeek: 7,
    previousPerWeek: 8,
    ...overrides,
  }) as ReleaseFrequencyDto

const cutToReleased = (
  overrides: Partial<
    Omit<CutToReleasedDto, 'averageDays' | 'previousAverageDays'>
  > & {
    averageDays?: number | null
    previousAverageDays?: number | null
  } = {},
): CutToReleasedDto =>
  ({
    averageDays: 3.79,
    measuredCount: 14,
    releasedCount: 14,
    previousAverageDays: 4.2,
    ...overrides,
  }) as CutToReleasedDto

const renderTiles = (
  f: Parameters<typeof frequency>[0] = {},
  c: Parameters<typeof cutToReleased>[0] = {},
) =>
  render(
    <DeliveryOverviewTiles
      frequency={frequency(f)}
      cutToReleased={cutToReleased(c)}
    />,
  )

/**
 * The rendered statistic values, in order.
 *
 * Read from the element rather than by text: antd splits a value into separate integer and decimal
 * spans, so "7.0" never exists as one text node.
 */
const statistics = () =>
  Array.from(document.querySelectorAll('.ant-statistic-content')).map(
    (el) => el.textContent ?? '',
  )

describe('DeliveryOverviewTiles', () => {
  it('reports the rate alongside the count and window it came from', () => {
    // Arrange / Act — two windows combine by summing the parts, not by averaging the rates, so the
    // parts have to be on the tile.
    renderTiles()

    // Assert
    expect(statistics()[0]).toContain('7.0')
    expect(statistics()[0]).toContain('/ week')
    expect(screen.getByText('14 versions over 14 days')).toBeInTheDocument()
  })

  it('says there is nothing to compare against rather than showing a rise from zero', () => {
    // Arrange — a first fortnight has no baseline. Rendering one would invent a trend.
    // Act
    renderTiles({ previousPerWeek: null })

    // Assert
    expect(screen.getByText('No earlier window to compare')).toBeInTheDocument()
  })

  it('treats a drop in frequency as the unwelcome direction', () => {
    // Arrange / Act — 7 a week against 8 the fortnight before.
    renderTiles()

    // Assert
    expect(screen.getByText(/1\.0 \/ week vs previous/)).toBeInTheDocument()
  })

  it('treats a drop in latency as the welcome direction', () => {
    // Arrange — shorter is better for cut-to-released, the opposite of frequency, so the two tiles
    // cannot share one rule about which way is good.
    // Act
    renderTiles({}, { averageDays: 2, previousAverageDays: 5 })

    // Assert
    expect(screen.getByText(/3\.0 days vs previous/)).toBeInTheDocument()
  })

  it('shows a dash rather than zero days when nothing measurable shipped', () => {
    // Arrange — zero would claim same-day releases, which is a different statement from "unknown".
    // Act
    renderTiles({}, { averageDays: null, measuredCount: 0, releasedCount: 3 })

    // Assert
    expect(statistics()[1]).toContain('—')
    expect(screen.getByText('0 of 3 versions measurable')).toBeInTheDocument()
  })

  it('says how much of the window the latency figure speaks for', () => {
    // Arrange — versions released without a cut carry no latency and are excluded, so the reader
    // needs the denominator to judge the number.
    // Act
    renderTiles({}, { measuredCount: 9, releasedCount: 14 })

    // Assert
    expect(screen.getByText('9 of 14 versions measurable')).toBeInTheDocument()
  })

  it('says unchanged rather than showing a zero delta', () => {
    // Arrange / Act
    renderTiles({ perWeek: 7, previousPerWeek: 7 })

    // Assert
    expect(screen.getByText('Unchanged')).toBeInTheDocument()
  })
})
