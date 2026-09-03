// The axis calls scale.tiers(), which uses REAL dayjs (.startOf/.add); the
// global mock only stubs format. Same opt-out as scale.test.ts.
jest.unmock('dayjs')

import { renderTimelineSvg, escapeXml, fitLabel, wrapLabel } from './render-svg'
import { createTimeScale } from '../../core/scale'
import type { GeometryConfig } from '../../core/geometry'
import type { ResolvedRow, TimelineGroup, TimelineItem } from '../../core/types'
import type { SvgTheme } from './theme'

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

const JAN = Date.UTC(2026, 0, 1)
const FEB = Date.UTC(2026, 1, 1)
const MAR = Date.UTC(2026, 2, 1)

function buildInput(
  overrides: Partial<Parameters<typeof renderTimelineSvg>[0]> = {},
) {
  const item: TimelineItem = {
    id: 'i1',
    kind: 'range',
    start: JAN,
    end: FEB,
    label: 'Alpha',
    groupId: 'g1',
  }
  const row: ResolvedRow = {
    rowKey: 'g1',
    groupId: 'g1',
    top: 0,
    height: 36,
    laneCount: 1,
    items: [{ item, lane: 0 }],
    depth: 0,
  }
  const groups: TimelineGroup[] = [{ id: 'g1', label: 'Team One' }]

  return {
    rows: [row],
    scale: createTimeScale(JAN, MAR, 800),
    geometry: GEOMETRY,
    totalHeight: 36,
    axisHeight: 48,
    groupPaneWidth: 200,
    groupsById: new Map(groups.map((g) => [g.id, g])),
    theme: THEME,
    fontFamily: 'system-ui, sans-serif',
    laneHeight: 28,
    ...overrides,
  }
}

describe('escapeXml', () => {
  test('escapes every XML-significant character', () => {
    // Arrange / Act
    const escaped = escapeXml(`<a href="x">Tom & Jerry's</a>`)

    // Assert
    expect(escaped).toBe(
      '&lt;a href=&quot;x&quot;&gt;Tom &amp; Jerry&apos;s&lt;/a&gt;',
    )
  })

  test('emits non-ASCII as numeric references so nothing can mis-decode', () => {
    // Arrange — an ellipsis from a truncated label and an accented project name.
    const label = 'Café…'

    // Act
    const escaped = escapeXml(label)

    // Assert — a consumer that falls back to Latin-1 turns raw UTF-8 into
    // mojibake; numeric references are pure ASCII and survive.
    expect(escaped).toBe('Caf&#233;&#8230;')
    expect(/^[\x00-\x7F]*$/.test(escaped)).toBe(true)
  })

  test('emits an emoji as a single reference, not two broken halves', () => {
    // Arrange — a surrogate pair; charCodeAt would split it into invalid halves.
    const escaped = escapeXml('🚀')

    // Assert
    expect(escaped).toBe('&#128640;')
  })
})

describe('fitLabel', () => {
  test('returns a short label unchanged', () => {
    // Arrange / Act / Assert
    expect(fitLabel('Alpha', 500, 12)).toBe('Alpha')
  })

  test('truncates with an ellipsis when the label overruns its box', () => {
    // Arrange
    const label = 'A very long project name that will not fit'

    // Act
    const fitted = fitLabel(label, 60, 12)

    // Assert — SVG has no text-overflow, so overflow must be cut here.
    expect(fitted.endsWith('…')).toBe(true)
    expect(fitted.length).toBeLessThan(label.length)
  })

  test('returns nothing when there is no room at all', () => {
    // Arrange / Act / Assert
    expect(fitLabel('Alpha', 0, 12)).toBe('')
  })
})

describe('wrapLabel', () => {
  test('keeps a short label on one line', () => {
    // Arrange / Act / Assert
    expect(wrapLabel('Team One', 500, 12, 3)).toEqual(['Team One'])
  })

  test('wraps at word boundaries across the lines available', () => {
    // Arrange — the live group column wraps rather than truncating, and rows
    // were grown to fit the wrapped result.
    const label = 'Project Portfolio Management 1'

    // Act
    const lines = wrapLabel(label, 120, 13, 3)

    // Assert
    expect(lines.length).toBeGreaterThan(1)
    expect(lines.join(' ')).toBe(label)
  })

  test('ellipsises the last line when it runs out of lines', () => {
    // Arrange
    const label = 'A very long group name that cannot possibly fit in one line'

    // Act
    const lines = wrapLabel(label, 80, 13, 2)

    // Assert
    expect(lines).toHaveLength(2)
    expect(lines[1].endsWith('…')).toBe(true)
  })

  test('breaks mid-word when a single word overruns the column', () => {
    // Arrange — matches the live column's `overflow-wrap: anywhere`.
    const label = 'Supercalifragilisticexpialidocious'

    // Act
    const lines = wrapLabel(label, 60, 13, 3)

    // Assert
    expect(lines.length).toBeGreaterThan(1)
  })
})

describe('renderTimelineSvg', () => {
  test('produces a standalone SVG document sized to the content', () => {
    // Arrange
    const input = buildInput()

    // Act
    const svg = renderTimelineSvg(input)

    // Assert — group pane + chart wide, axis + rows tall.
    expect(svg).toContain('<svg xmlns="http://www.w3.org/2000/svg"')
    expect(svg).toContain('width="1000"')
    expect(svg).toContain('height="84"')
    expect(svg.trimEnd().endsWith('</svg>')).toBe(true)
  })

  test('draws the item bar and its label', () => {
    // Arrange
    const input = buildInput()

    // Act
    const svg = renderTimelineSvg(input)

    // Assert
    expect(svg).toContain('Alpha')
    expect(svg).toContain('<rect')
  })

  test('draws the group label in the left column', () => {
    // Arrange
    const input = buildInput()

    // Act
    const svg = renderTimelineSvg(input)

    // Assert
    expect(svg).toContain('Team One')
  })

  test('declares UTF-8 so non-ASCII labels are not mis-decoded', () => {
    // Arrange
    const input = buildInput()

    // Act
    const svg = renderTimelineSvg(input)

    // Assert — without the declaration a Latin-1 fallback turns the ellipsis in
    // a truncated label into mojibake.
    expect(svg.startsWith('<?xml version="1.0" encoding="UTF-8"?>')).toBe(true)
  })

  test('wraps a long group label over the lines its row height allows', () => {
    // Arrange — a tall row means the live column wrapped the label; the export
    // must do the same rather than stranding one short line in a tall cell.
    const groups: TimelineGroup[] = [
      { id: 'g1', label: 'Project Portfolio Management 1' },
    ]
    const input = buildInput({
      rows: [
        {
          rowKey: 'g1',
          groupId: 'g1',
          top: 0,
          height: 90,
          laneCount: 1,
          items: [],
          depth: 0,
        },
      ],
      totalHeight: 90,
      groupsById: new Map(groups.map((g) => [g.id, g])),
    })

    // Act
    const svg = renderTimelineSvg(input)

    // Assert — more than one <text> in the group column means it wrapped.
    const groupTexts = svg.match(/Project|Portfolio|Management/g) ?? []
    expect(groupTexts.length).toBeGreaterThan(1)
  })

  test('omits the group column when its width is zero', () => {
    // Arrange — an ungrouped timeline renders a flat, full-width chart.
    const input = buildInput({ groupPaneWidth: 0 })

    // Act
    const svg = renderTimelineSvg(input)

    // Assert
    expect(svg).not.toContain('Team One')
    expect(svg).toContain('width="800"')
  })

  test('escapes labels so they cannot break the document', () => {
    // Arrange — a project legitimately named with an ampersand and angle bracket.
    const item: TimelineItem = {
      id: 'i1',
      kind: 'range',
      start: JAN,
      end: FEB,
      label: 'R&D <phase 1>',
      groupId: 'g1',
    }
    const input = buildInput({
      rows: [
        {
          rowKey: 'g1',
          groupId: 'g1',
          top: 0,
          height: 36,
          laneCount: 1,
          items: [{ item, lane: 0 }],
          depth: 0,
        },
      ],
    })

    // Act
    const svg = renderTimelineSvg(input)

    // Assert — raw markup here would produce an unopenable file.
    expect(svg).toContain('&amp;')
    expect(svg).not.toContain('<phase 1>')
  })

  test('draws the current-time line only when enabled and in range', () => {
    // Arrange
    const inRange = buildInput({ showCurrentTime: true, nowMs: FEB })
    const disabled = buildInput({ showCurrentTime: false, nowMs: FEB })

    // Act
    const withLine = renderTimelineSvg(inRange)
    const withoutLine = renderTimelineSvg(disabled)

    // Assert
    expect(withLine).toContain(THEME.error)
    expect(withoutLine).not.toContain(THEME.error)
  })

  test('omits the current-time line when now falls outside the domain', () => {
    // Arrange — "now" a year past the chart's end.
    const input = buildInput({
      showCurrentTime: true,
      nowMs: Date.UTC(2027, 5, 1),
    })

    // Act
    const svg = renderTimelineSvg(input)

    // Assert
    expect(svg).not.toContain(THEME.error)
  })

  test('renders a milestone as a rotated square', () => {
    // Arrange
    const item: TimelineItem = {
      id: 'm1',
      kind: 'milestone',
      start: FEB,
      end: FEB,
      label: 'Launch',
      groupId: 'g1',
    }
    const input = buildInput({
      rows: [
        {
          rowKey: 'g1',
          groupId: 'g1',
          top: 0,
          height: 36,
          laneCount: 1,
          items: [{ item, lane: 0 }],
          depth: 0,
        },
      ],
    })

    // Act
    const svg = renderTimelineSvg(input)

    // Assert
    expect(svg).toContain('rotate(45')
  })

  test('honours an item colour over the theme default', () => {
    // Arrange
    const item: TimelineItem = {
      id: 'i1',
      kind: 'range',
      start: JAN,
      end: FEB,
      label: 'Alpha',
      color: '#abcdef',
      groupId: 'g1',
    }
    const input = buildInput({
      rows: [
        {
          rowKey: 'g1',
          groupId: 'g1',
          top: 0,
          height: 36,
          laneCount: 1,
          items: [{ item, lane: 0 }],
          depth: 0,
        },
      ],
    })

    // Act
    const svg = renderTimelineSvg(input)

    // Assert
    expect(svg).toContain('#abcdef')
  })

  test('renders every row, however many there are', () => {
    // Arrange — the case that broke the raster export: far more rows than fit
    // on screen. The vector path has no size ceiling at all.
    const rows: ResolvedRow[] = Array.from({ length: 400 }, (_, i) => ({
      rowKey: `g${i}`,
      groupId: `g${i}`,
      top: i * 36,
      height: 36,
      laneCount: 1,
      items: [
        {
          item: {
            id: `i${i}`,
            kind: 'range' as const,
            start: JAN,
            end: FEB,
            label: `Item ${i}`,
            groupId: `g${i}`,
          },
          lane: 0,
        },
      ],
      depth: 0,
    }))
    const groups = rows.map((r) => ({
      id: r.groupId as string,
      label: `Row ${r.groupId}`,
    }))
    const input = buildInput({
      rows,
      totalHeight: 400 * 36,
      groupsById: new Map(groups.map((g) => [g.id, g])),
    })

    // Act
    const svg = renderTimelineSvg(input)

    // Assert
    expect(svg).toContain('Item 0')
    expect(svg).toContain('Item 399')
    expect(svg).toContain(`height="${48 + 400 * 36}"`)
  })
})
