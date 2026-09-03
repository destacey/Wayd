// timeline/render/svg/render-svg.ts
// Renders the timeline model directly to SVG — no DOM screenshot, no clone.
//
// This mirrors the z-order chart-canvas.tsx paints in (weekends -> gridlines ->
// row stripes -> backgrounds -> bars -> now-line), reading the SAME pure
// geometry functions the live chart uses. Nothing here touches the DOM, so it is
// fully unit-testable; the one thing it cannot derive is measured text height,
// which the caller supplies via already-grown rows.
//
// Interaction (drag, tooltips, hover handles) has no meaning in a static export
// and is deliberately absent.

import {
  itemBox,
  backgroundBox,
  type GeometryConfig,
} from '../../core/geometry'
import { contrastText } from '../../core/color'
import { truncateOneDayLabel } from '../../core/labels'
import type { TimeScale } from '../../core/scale'
import type { ResolvedRow, TimelineGroup, TimelineItem } from '../../core/types'
import type { SvgTheme } from './theme'
import {
  createTextMeasurer,
  truncateToWidth,
  wrapToWidth,
  type TextMeasurer,
} from './measure-text'

/** Milliseconds in a day — matches item-bar.tsx's one-day treatment. */
const ONE_DAY_MS = 24 * 60 * 60 * 1000
/** Gap between a one-day bar and its outside label (from item-bar.tsx). */
const ONE_DAY_LABEL_GAP = 4
/** Group-label indent per depth level (from group-column.tsx). */
const INDENT_PER_DEPTH = 14
/** Horizontal padding inside a bar before its label (from .barLabel). */
const BAR_LABEL_PAD = 6
/** Left padding of a group cell — .groupCell's --ant-padding-sm. */
const GROUP_CELL_PAD_LEFT = 12
/**
 * Space consumed to the RIGHT of a group label: .groupCell's padding-right
 * (--ant-padding-xs) plus .groupLabel's own padding-right (also
 * --ant-padding-xs). Verified against the live DOM — the label's border box is
 * exactly 8px wider than its measured text, on top of the cell's own 8.
 */
const GROUP_LABEL_PAD_RIGHT = 16
/** Fallback measurer for the exported helpers when no measurer is supplied.
 *  Built once, against the generic sans stack. */
let fallbackMeasurer: TextMeasurer | null = null
function defaultMeasurer(): TextMeasurer {
  fallbackMeasurer ??= createTextMeasurer('sans-serif')
  return fallbackMeasurer
}

export interface RenderSvgInput {
  /** Rows AFTER label-driven growth, so tops/heights match the screen. */
  rows: ResolvedRow[]
  scale: TimeScale
  geometry: GeometryConfig
  /** Total stacked height of all rows, px. */
  totalHeight: number
  /** Height of the two-tier axis header, px. */
  axisHeight: number
  /** Width of the left group-label column, px. 0 hides the column entirely. */
  groupPaneWidth: number
  groupsById: Map<string, TimelineGroup>
  chartBackgrounds?: TimelineItem[]
  theme: SvgTheme
  fontFamily: string
  showGridlines?: boolean
  showWeekends?: boolean
  showCurrentTime?: boolean
  /** Explicit "now" so the export does not inherit whenever the chart mounted. */
  nowMs?: number
  /** Lane height, used to scale label fonts as the live renderers do. */
  laneHeight: number
  /**
   * Measures label widths. Defaults to a canvas measurer for `fontFamily`,
   * which matches real DOM layout; inject an approximate one where no canvas
   * exists (SSR, jsdom).
   */
  measurer?: TextMeasurer
}

/**
 * Escape the five XML-significant characters, then emit every remaining
 * non-ASCII character as a numeric reference.
 *
 * The numeric references matter: a consumer that ignores both the blob's
 * charset and the XML declaration decodes the file as Latin-1, and any raw
 * UTF-8 byte sequence — an ellipsis in a truncated label, an accent in a
 * project name, an en dash — becomes mojibake. `&#8230;` is plain ASCII and
 * survives that intact, so the document carries no bytes above 0x7F at all.
 */
export function escapeXml(value: string): string {
  return (
    value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;')
      // `u` flag + codePointAt so an emoji (a surrogate pair) becomes ONE
      // reference rather than two invalid halves.
      .replace(/[^\x00-\x7F]/gu, (ch) => `&#${ch.codePointAt(0)};`)
  )
}

/**
 * Truncate `label` with an ellipsis so it fits `maxWidth` px.
 * Thin wrapper over the measurer — SVG has no `text-overflow: ellipsis`, so
 * overflow has to be cut here rather than by the renderer.
 */
export function fitLabel(
  label: string,
  maxWidth: number,
  fontSize: number,
  measurer: TextMeasurer = defaultMeasurer(),
): string {
  return truncateToWidth(label, maxWidth, fontSize, measurer)
}

/**
 * Break `label` into lines that each fit `maxWidth` px, up to `maxLines`.
 *
 * The live group column wraps (`white-space: normal; overflow-wrap: anywhere`)
 * and rows were GROWN to fit the wrapped result, so a single truncated line
 * would leave a tall row with one short label floating in it.
 */
export function wrapLabel(
  label: string,
  maxWidth: number,
  fontSize: number,
  maxLines: number,
  measurer: TextMeasurer = defaultMeasurer(),
): string[] {
  return wrapToWidth(label, maxWidth, fontSize, maxLines, measurer)
}

/** Font size for a label inside a box of `height` px (matches item-bar.tsx). */
function labelFontSize(height: number): number {
  return Math.max(9, Math.min(Math.floor(height / 1.2), 13))
}

/** Render the timeline to a standalone SVG document string. */
export function renderTimelineSvg(input: RenderSvgInput): string {
  const {
    rows,
    scale,
    geometry,
    totalHeight,
    axisHeight,
    groupPaneWidth,
    groupsById,
    chartBackgrounds,
    theme,
    fontFamily,
    showGridlines = true,
    showWeekends = false,
    showCurrentTime = false,
    nowMs,
    laneHeight,
  } = input

  // Measure with the font the export actually declares, so truncation matches
  // what a viewer will see rather than a generic estimate.
  const measurer = input.measurer ?? createTextMeasurer(fontFamily)

  const chartWidth = scale.width
  const width = groupPaneWidth + chartWidth
  const height = axisHeight + totalHeight
  const parts: string[] = []

  // Page background — an SVG has no default paint, so without this the export
  // is transparent and unreadable on a dark backdrop.
  parts.push(
    `<rect x="0" y="0" width="${width}" height="${height}" fill="${theme.bgContainer}"/>`,
  )

  parts.push(renderAxis(input, chartWidth, measurer))

  // Everything below the axis is drawn in one translated group, so the child
  // geometry can use the same row coordinates the live chart does.
  parts.push(`<g transform="translate(0,${axisHeight})">`)
  if (groupPaneWidth > 0) {
    parts.push(renderGroupColumn(input, measurer))
  }
  // Chart body, clipped so bars can't spill into the group column or past the
  // right edge (the live view clips via overflow:hidden).
  parts.push(
    `<g transform="translate(${groupPaneWidth},0)" clip-path="url(#chartClip)">`,
  )
  parts.push(renderChartBody(input, chartWidth, measurer))
  parts.push('</g>')
  parts.push('</g>')

  const defs =
    `<defs><clipPath id="chartClip">` +
    `<rect x="0" y="0" width="${chartWidth}" height="${totalHeight}"/>` +
    `</clipPath></defs>`

  // The XML declaration is required: without it a consumer that ignores the
  // blob's charset falls back to Latin-1 and every non-ASCII character in a
  // label (an ellipsis, an accent, a dash) renders as mojibake.
  return (
    `<?xml version="1.0" encoding="UTF-8"?>` +
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" ` +
    `viewBox="0 0 ${width} ${height}" font-family="${escapeXml(fontFamily)}">` +
    defs +
    parts.join('') +
    `</svg>`
  )
}

/** Two-tier timescale header (upper: month/year, lower: week/day). */
function renderAxis(
  input: RenderSvgInput,
  chartWidth: number,
  measurer: TextMeasurer,
): string {
  const { scale, axisHeight, groupPaneWidth, theme } = input
  const { upper, lower } = scale.tiers()
  const tierHeight = axisHeight / 2
  const fontSize = 12
  const parts: string[] = []

  parts.push(
    `<rect x="0" y="0" width="${groupPaneWidth + chartWidth}" height="${axisHeight}" fill="${theme.bgElevated}"/>`,
  )

  const tier = (segments: typeof upper, top: number) => {
    segments.forEach((seg) => {
      const left = scale.toX(seg.startMs)
      const segWidth = scale.toX(seg.endMs) - left
      if (segWidth <= 0) return
      const x = groupPaneWidth + left
      // Tier separator, matching .axisCell's left border.
      parts.push(
        `<line x1="${x}" y1="${top}" x2="${x}" y2="${top + tierHeight}" stroke="${theme.split}" stroke-width="1"/>`,
      )
      const label = fitLabel(seg.label, segWidth - 4, fontSize, measurer)
      if (!label) return
      parts.push(
        `<text x="${x + 4}" y="${top + tierHeight / 2}" dominant-baseline="central" ` +
          `font-size="${fontSize}" fill="${theme.textSecondary}">${escapeXml(label)}</text>`,
      )
    })
  }

  tier(upper, 0)
  tier(lower, tierHeight)

  // Header underline.
  parts.push(
    `<line x1="0" y1="${axisHeight}" x2="${groupPaneWidth + chartWidth}" y2="${axisHeight}" ` +
      `stroke="${theme.border}" stroke-width="1"/>`,
  )
  return parts.join('')
}

/** Left label column: one cell per row, indented by depth. */
function renderGroupColumn(
  input: RenderSvgInput,
  measurer: TextMeasurer,
): string {
  const { rows, groupsById, groupPaneWidth, theme, laneHeight } = input
  const fontSize = Math.max(9, Math.min(Math.floor(laneHeight / 1.2), 13))
  const parts: string[] = []

  parts.push(
    `<rect x="0" y="0" width="${groupPaneWidth}" height="${input.totalHeight}" fill="${theme.bgContainer}"/>`,
  )

  rows.forEach((row, i) => {
    if (i % 2 === 1) {
      parts.push(
        `<rect x="0" y="${row.top}" width="${groupPaneWidth}" height="${row.height}" fill="${theme.fillQuaternary}"/>`,
      )
    }
    parts.push(
      `<line x1="0" y1="${row.top + row.height}" x2="${groupPaneWidth}" y2="${row.top + row.height}" ` +
        `stroke="${theme.split}" stroke-width="1"/>`,
    )

    const group = row.groupId ? groupsById.get(row.groupId) : undefined
    const raw = group?.label ?? row.groupId ?? ''
    if (!raw) return
    const indent = GROUP_CELL_PAD_LEFT + row.depth * INDENT_PER_DEPTH
    // Wrap to the same width the live column wraps at, across however many
    // lines this row's MEASURED height allows — the row was grown to fit the
    // wrapped label, so a single line would leave it stranded in a tall cell.
    const lineHeight = fontSize * 1.4
    const maxLines = Math.max(1, Math.floor((row.height - 8) / lineHeight))
    const lines = wrapLabel(
      raw,
      groupPaneWidth - indent - GROUP_LABEL_PAD_RIGHT,
      fontSize,
      maxLines,
      measurer,
    )
    if (lines.length === 0) return
    // Centre the wrapped block vertically within the row.
    const blockTop = row.top + (row.height - lines.length * lineHeight) / 2
    lines.forEach((line, lineIndex) => {
      parts.push(
        `<text x="${indent}" y="${blockTop + lineHeight * (lineIndex + 0.5)}" ` +
          `dominant-baseline="central" font-size="${fontSize}" ` +
          `fill="${theme.text}">${escapeXml(line)}</text>`,
      )
    })
  })

  // Column separator.
  parts.push(
    `<line x1="${groupPaneWidth}" y1="0" x2="${groupPaneWidth}" y2="${input.totalHeight}" ` +
      `stroke="${theme.border}" stroke-width="1"/>`,
  )
  return parts.join('')
}

/** Chart body, painted in chart-canvas.tsx's z-order. */
function renderChartBody(
  input: RenderSvgInput,
  chartWidth: number,
  measurer: TextMeasurer,
): string {
  const {
    rows,
    scale,
    geometry,
    totalHeight,
    chartBackgrounds,
    theme,
    showGridlines = true,
    showWeekends = false,
    showCurrentTime = false,
    nowMs,
  } = input
  const parts: string[] = []

  // 1. Weekend shading
  if (showWeekends) {
    scale.weekends().forEach((box) => {
      parts.push(
        `<rect x="${box.left}" y="0" width="${box.width}" height="${totalHeight}" fill="${theme.fillQuaternary}"/>`,
      )
    })
  }

  // 2. Vertical gridlines, aligned to the lower axis tier
  if (showGridlines) {
    scale.tiers().lower.forEach((seg) => {
      const x = scale.toX(seg.startMs)
      parts.push(
        `<line x1="${x}" y1="0" x2="${x}" y2="${totalHeight}" stroke="${theme.split}" stroke-width="1"/>`,
      )
    })
  }

  // 3. Row stripes + separators
  rows.forEach((row, i) => {
    if (i % 2 === 1) {
      parts.push(
        `<rect x="0" y="${row.top}" width="${chartWidth}" height="${row.height}" fill="${theme.fillQuaternary}"/>`,
      )
    }
    parts.push(
      `<line x1="0" y1="${row.top + row.height}" x2="${chartWidth}" y2="${row.top + row.height}" ` +
        `stroke="${theme.split}" stroke-width="1"/>`,
    )
  })

  // 4. Backgrounds (timeboxes) — behind bars, in front of stripes
  const rowsByGroupId = new Map(
    rows.filter((r) => r.groupId).map((r) => [r.groupId as string, r]),
  )
  chartBackgrounds?.forEach((bg) => {
    const row = bg.groupId ? rowsByGroupId.get(bg.groupId) : undefined
    const box = backgroundBox(bg, row ?? null, scale, totalHeight)
    if (box.width <= 0) return
    parts.push(
      `<rect x="${box.left}" y="${box.top}" width="${box.width}" height="${box.height}" ` +
        `rx="4" fill="${bg.color ?? theme.fillSecondary}" stroke="${theme.border}" stroke-width="1"/>`,
    )
    if (!bg.label) return
    const label = fitLabel(bg.label, box.width - 8, 12, measurer)
    if (!label) return
    parts.push(
      `<text x="${box.left + 6}" y="${box.top + 12}" dominant-baseline="central" ` +
        `font-size="12" fill="${theme.textSecondary}">${escapeXml(label)}</text>`,
    )
  })

  // 5. Item bars and milestones
  rows.forEach((row) => {
    row.items.forEach(({ item, lane }) => {
      parts.push(renderItem(item, lane, row, scale, geometry, theme, measurer))
    })
  })

  // 6. Current-time line
  const now = nowMs ?? Date.now()
  if (showCurrentTime && now >= scale.domain[0] && now <= scale.domain[1]) {
    const x = scale.toX(now)
    parts.push(
      `<line x1="${x}" y1="0" x2="${x}" y2="${totalHeight}" stroke="${theme.error}" stroke-width="2"/>`,
    )
  }

  return parts.join('')
}

/** One range bar (with optional progress fill) or milestone diamond. */
function renderItem(
  item: TimelineItem,
  lane: number,
  row: ResolvedRow,
  scale: TimeScale,
  geometry: GeometryConfig,
  theme: SvgTheme,
  measurer: TextMeasurer,
): string {
  const box = itemBox(item, lane, row, scale, geometry)
  const { left, top, width, height } = box
  const fill = item.color ?? theme.primary

  if (item.kind === 'milestone') {
    // A square rotated 45° about its own centre, matching .milestone.
    const half = height / 2
    const cx = left
    const cy = top + half
    return (
      `<rect x="${cx - half}" y="${top}" width="${height}" height="${height}" ` +
      `fill="${item.color ?? theme.primary}" transform="rotate(45 ${cx} ${cy})"/>`
    )
  }

  if (width <= 0) return ''

  const isOneDay = item.end - item.start <= ONE_DAY_MS
  const visualLeft = isOneDay ? left + Math.max(0, (width - height) / 2) : left
  const visualWidth = isOneDay ? height : width
  const fontSize = labelFontSize(height)
  const parts: string[] = []

  parts.push(
    `<rect x="${visualLeft}" y="${top}" width="${visualWidth}" height="${height}" rx="2" fill="${fill}"/>`,
  )

  // Progress underlay, drawn over the bar fill but under the label.
  if (typeof item.progress === 'number') {
    const progressWidth = (visualWidth * item.progress) / 100
    if (progressWidth > 0) {
      parts.push(
        `<rect x="${visualLeft}" y="${top}" width="${progressWidth}" height="${height}" rx="2" ` +
          `fill="#000000" fill-opacity="0.18"/>`,
      )
    }
  }

  const text = item.label ?? item.id
  if (isOneDay) {
    // Too narrow to hold text: label sits outside, in the chart's text color.
    const label = truncateOneDayLabel(text)
    parts.push(
      `<text x="${visualLeft + visualWidth + ONE_DAY_LABEL_GAP}" y="${top + height / 2}" ` +
        `dominant-baseline="central" font-size="${fontSize}" fill="${theme.text}">${escapeXml(label)}</text>`,
    )
  } else {
    const label = fitLabel(
      text,
      visualWidth - BAR_LABEL_PAD * 2,
      fontSize,
      measurer,
    )
    if (label) {
      parts.push(
        `<text x="${visualLeft + BAR_LABEL_PAD}" y="${top + height / 2}" dominant-baseline="central" ` +
          `font-size="${fontSize}" fill="${contrastText(item.color)}">${escapeXml(label)}</text>`,
      )
    }
  }

  return parts.join('')
}
