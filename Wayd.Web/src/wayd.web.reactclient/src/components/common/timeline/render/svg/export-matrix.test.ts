// Matrix coverage for the SVG export across screen sizes and zoom levels.
//
// Zoom and resolution are the two axes that changed behaviour when the export
// regressed (it rendered the whole multi-year domain instead of the visible
// window). Zoom only moves scrollLeft/chartWidth; resolution only moves
// viewportWidth. Both feed the same projection, so the invariants below are
// asserted over a grid of them rather than at one arbitrary size.

// The axis calls scale.tiers(), which uses REAL dayjs (.startOf/.add).
jest.unmock('dayjs')

import { createExportScale } from './export-scale'
import { renderTimelineSvg } from './render-svg'
import { createTimeScale } from '../../core/scale'
import type { GeometryConfig } from '../../core/geometry'
import type { ResolvedRow, TimelineGroup, TimelineItem } from '../../core/types'
import type { SvgTheme } from './theme'
import { createApproximateMeasurer } from './measure-text'

// jsdom has no canvas 2D, so the renderer's canvas measurer would silently fall
// back to an estimate anyway. Inject it explicitly so these tests state which
// measurement they exercise; real-font behaviour is covered in
// measure-text.test.ts against a variable-width measurer.
const MEASURER = createApproximateMeasurer(0.55)

const THEME: SvgTheme = {
  text: '#111111',
  textSecondary: '#666666',
  border: '#dddddd',
  split: '#eeeeee',
  bgContainer: '#ffffff',
  bgElevated: '#fafafa',
  fillQuaternary: '#f5f5f5',
  fillSecondary: '#ebebeb',
  error: '#ff0000',
  primary: '#0000ff',
}

const GEOMETRY: GeometryConfig = {
  laneHeight: 28,
  lanePadding: 2,
  rowPadding: 8,
}

/** Chart width available after the 220px group column, at each screen width. */
const RESOLUTIONS = [
  { name: '1080p', viewportWidth: 1920 - 220 },
  { name: '1440p', viewportWidth: 2560 - 220 },
  { name: '4K', viewportWidth: 3840 - 220 },
  { name: 'narrow laptop', viewportWidth: 1366 - 220 },
]

/** Zoom factors over the fit-to-window base, as the toolbar produces. */
const ZOOMS = [1, 2, 4, 12]

// A ~3.5 year domain, like the roadmap that exposed the original bug.
const DOMAIN_START = Date.UTC(2023, 6, 20)
const DOMAIN_END = Date.UTC(2027, 1, 21)

const GROUPS: TimelineGroup[] = [
  { id: 'g1', label: 'Project Portfolio Management 1' },
  { id: 'g2', label: 'Core Functionality and UI' },
]

/** Items spanning a range of durations, including ones outside the window. */
const ITEMS: TimelineItem[] = [
  {
    id: 'i1',
    kind: 'range',
    start: Date.UTC(2024, 8, 1),
    end: Date.UTC(2025, 2, 1),
    label: 'Project Management',
    groupId: 'g1',
    color: '#ffec3d',
  },
  {
    id: 'i2',
    kind: 'range',
    start: Date.UTC(2023, 7, 1),
    end: Date.UTC(2023, 9, 1),
    label: 'Long Before Window',
    groupId: 'g1',
    color: '#666666',
  },
  {
    id: 'i3',
    kind: 'range',
    start: Date.UTC(2026, 10, 1),
    end: Date.UTC(2027, 0, 1),
    label: 'Long After Window',
    groupId: 'g2',
    color: '#1f83d2',
  },
]

function buildRows(): ResolvedRow[] {
  return [
    {
      rowKey: 'g1',
      groupId: 'g1',
      top: 0,
      height: 90,
      laneCount: 2,
      items: [
        { item: ITEMS[0], lane: 0 },
        { item: ITEMS[1], lane: 1 },
      ],
      depth: 0,
    },
    {
      rowKey: 'g2',
      groupId: 'g2',
      top: 90,
      height: 34,
      laneCount: 1,
      items: [{ item: ITEMS[2], lane: 0 }],
      depth: 0,
    },
  ]
}

/**
 * Reproduce the component's geometry for a given screen size and zoom:
 * the canvas is the viewport scaled by the zoom factor, and the scroll offset
 * centres the window the way panning to the middle of the roadmap would.
 */
function buildChart(viewportWidth: number, zoom: number) {
  const chartWidth = viewportWidth * zoom
  const scale = createTimeScale(DOMAIN_START, DOMAIN_END, chartWidth)
  const scrollLeft = Math.max(0, (chartWidth - viewportWidth) / 2)
  return { scale, scrollLeft, chartWidth }
}

function renderAt(viewportWidth: number, zoom: number) {
  const { scale, scrollLeft } = buildChart(viewportWidth, zoom)
  const exportScale = createExportScale({ scale, scrollLeft, viewportWidth })
  const rows = buildRows()
  const svg = renderTimelineSvg({
    rows,
    scale: exportScale,
    geometry: GEOMETRY,
    totalHeight: 124,
    axisHeight: 48,
    groupPaneWidth: 220,
    groupsById: new Map(GROUPS.map((g) => [g.id, g])),
    theme: THEME,
    fontFamily: 'system-ui, sans-serif',
    laneHeight: 28,
    showGridlines: true,
    showCurrentTime: true,
    nowMs: Date.UTC(2024, 10, 1),
    measurer: MEASURER,
  })
  return { svg, exportScale, scale, scrollLeft }
}

describe.each(RESOLUTIONS)('SVG export at $name', ({ viewportWidth }) => {
  describe.each(ZOOMS)('at zoom %sx', (zoom) => {
    test('is sized to the viewport, not the scrollable canvas', () => {
      // Arrange / Act
      const { svg } = renderAt(viewportWidth, zoom)

      // Assert — the regression this guards: exporting the full domain gave a
      // ~28,000px image of a mostly-empty multi-year canvas.
      expect(svg).toContain(`width="${220 + viewportWidth}"`)
    })

    test('covers exactly the visible time window', () => {
      // Arrange
      const { exportScale, scale, scrollLeft } = renderAt(viewportWidth, zoom)

      // Act
      const [start, end] = exportScale.domain

      // Assert — the exported window must be the slice under the viewport.
      expect(start).toBeCloseTo(scale.toMs(scrollLeft), -3)
      expect(end).toBeCloseTo(scale.toMs(scrollLeft + viewportWidth), -3)
    })

    test('shows a narrower time span the further in the user zooms', () => {
      // Arrange
      const { exportScale } = renderAt(viewportWidth, zoom)
      const { exportScale: atBase } = renderAt(viewportWidth, 1)

      // Act
      const span = exportScale.domain[1] - exportScale.domain[0]
      const baseSpan = atBase.domain[1] - atBase.domain[0]

      // Assert
      expect(span).toBeLessThanOrEqual(baseSpan)
      if (zoom > 1) expect(span).toBeLessThan(baseSpan)
    })

    test('produces a well-formed document', () => {
      // Arrange / Act
      const { svg } = renderAt(viewportWidth, zoom)

      // Assert
      expect(svg.startsWith('<?xml version="1.0" encoding="UTF-8"?>')).toBe(
        true,
      )
      expect(svg.trimEnd().endsWith('</svg>')).toBe(true)
      // Every <rect>/<line>/<text> must be closed; a stray open tag would make
      // the file unopenable in a strict SVG consumer.
      expect(svg.match(/<g[ >]/g)?.length ?? 0).toBe(
        svg.match(/<\/g>/g)?.length ?? 0,
      )
    })

    test('emits no NaN or Infinity in any coordinate', () => {
      // Arrange / Act
      const { svg } = renderAt(viewportWidth, zoom)

      // Assert — a degenerate scale silently produces NaN attributes, which
      // render as nothing at all rather than erroring.
      expect(svg).not.toMatch(/NaN|Infinity/)
    })

    test('keeps every drawn bar inside the chart area', () => {
      // Arrange
      const { svg } = renderAt(viewportWidth, zoom)

      // Act — bar rects carry rx="2"; read their x and width.
      const bars = [
        ...svg.matchAll(/<rect x="([\d.-]+)"[^>]*width="([\d.]+)"[^>]*rx="2"/g),
      ]

      // Assert — geometry clamps to the domain, so nothing may start left of 0
      // or extend past the viewport width.
      bars.forEach(([, x, w]) => {
        expect(Number(x)).toBeGreaterThanOrEqual(0)
        expect(Number(x) + Number(w)).toBeLessThanOrEqual(viewportWidth + 1)
      })
    })

    test('draws a bar for exactly the items overlapping the window', () => {
      // Arrange — which items are visible legitimately depends on the zoom, so
      // assert against the window rather than naming an item. Match on the
      // bar's fill, not its label: a narrow bar truncates its text (as the live
      // view does with CSS ellipsis), so the label may not appear in full.
      const { svg, exportScale } = renderAt(viewportWidth, zoom)
      const [windowStart, windowEnd] = exportScale.domain

      ITEMS.forEach((item) => {
        // Act
        const overlaps = item.end >= windowStart && item.start <= windowEnd
        const drawn = svg.includes(`rx="2" fill="${item.color}"`)

        // Assert — an item inside the window must be drawn, and one entirely
        // outside it must not be (geometry clamps it to zero width).
        expect(drawn).toBe(overlaps)
      })
    })

    test('never renders a label wider than the bar holding it', () => {
      // Arrange — SVG has no text-overflow, so an over-long label would spill
      // across neighbouring bars instead of being clipped.
      const { svg } = renderAt(viewportWidth, zoom)
      const bars = [
        ...svg.matchAll(
          /<rect x="([\d.]+)"[^>]*width="([\d.]+)"[^>]*rx="2"[^>]*\/><text x="([\d.]+)"[^>]*>([^<]*)<\/text>/g,
        ),
      ]

      // Act / Assert — approximate the drawn width the same way the renderer
      // budgets it; a label must fit within its bar's right edge. Count
      // RENDERED glyphs: `&#8230;` is seven characters of markup but one glyph.
      bars.forEach(([, x, w, textX, label]) => {
        // Count RENDERED glyphs: `&#8230;` is seven characters of markup but
        // one glyph. Measure with the same measurer the renderer used.
        const glyphs = label.replace(/&#\d+;|&\w+;/g, 'x')
        const drawnWidth = MEASURER.width(glyphs, 13)
        expect(Number(textX) + drawnWidth).toBeLessThanOrEqual(
          Number(x) + Number(w) + 1,
        )
      })
    })
  })
})

describe('createExportScale', () => {
  test('falls back to the full scale before the viewport is measured', () => {
    // Arrange — the first render, before the ResizeObserver fires.
    const scale = createTimeScale(DOMAIN_START, DOMAIN_END, 5000)

    // Act
    const exportScale = createExportScale({
      scale,
      scrollLeft: 0,
      viewportWidth: 0,
    })

    // Assert — exporting the whole domain beats exporting nothing.
    expect(exportScale).toBe(scale)
  })

  test('clamps an overscrolled position back inside the domain', () => {
    // Arrange — a bounce/overscroll can push scrollLeft past the canvas end.
    const scale = createTimeScale(DOMAIN_START, DOMAIN_END, 5000)

    // Act
    const exportScale = createExportScale({
      scale,
      scrollLeft: 99_999,
      viewportWidth: 1000,
    })

    // Assert — the window stays within the roadmap's real dates.
    expect(exportScale.domain[1]).toBeLessThanOrEqual(DOMAIN_END)
    expect(exportScale.domain[0]).toBeGreaterThanOrEqual(DOMAIN_START)
  })

  test('covers the whole domain when zoomed fully out', () => {
    // Arrange — at the zoom floor the canvas equals the viewport.
    const viewportWidth = 1700
    const scale = createTimeScale(DOMAIN_START, DOMAIN_END, viewportWidth)

    // Act
    const exportScale = createExportScale({
      scale,
      scrollLeft: 0,
      viewportWidth,
    })

    // Assert
    expect(exportScale.domain[0]).toBeCloseTo(DOMAIN_START, -3)
    expect(exportScale.domain[1]).toBeCloseTo(DOMAIN_END, -3)
  })

  test('gives the same time window whatever the screen width, at equal zoom', () => {
    // Arrange — a wider monitor shows MORE time at the same zoom factor, but
    // the window must still start where the user has scrolled to.
    const results = RESOLUTIONS.map(({ viewportWidth }) => {
      const { scale, scrollLeft } = buildChart(viewportWidth, 4)
      const exportScale = createExportScale({
        scale,
        scrollLeft,
        viewportWidth,
      })
      return { viewportWidth, start: exportScale.domain[0] }
    })

    // Assert — all centred the same way, so all start at the same instant.
    const [first, ...rest] = results
    rest.forEach((r) => expect(r.start).toBeCloseTo(first.start, -5))
  })
})
