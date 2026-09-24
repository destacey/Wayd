import {
  createTextMeasurer,
  wrapToWidth,
} from '@/src/components/common/timeline/render/svg/measure-text'
import { escapeXml } from '@/src/components/common/timeline/render/svg/render-svg'
import { Position, getBezierPath } from '@xyflow/react'

/**
 * A node as it is laid out on screen: absolute position and the size React Flow measured, so a label
 * that wrapped to two lines exports at the height it actually has.
 */
export interface DependencySvgNode {
  id: string
  label: string
  x: number
  y: number
  width: number
  height: number
  isGroup: boolean
  isSubject: boolean
  /** A count of records left off, which reads as a note rather than a record. */
  isOverflow?: boolean
  /** A record the reader expanded, outlined as it is on screen. */
  isExpanded?: boolean
}

/** How one kind of edge is drawn, with its colour resolved. */
export interface DependencyEdgeStyle {
  stroke: string
  width: number
  dashed?: boolean
}

export interface DependencySvgEdge {
  source: string
  target: string
  /** Leaves the source's left side rather than its right, as a link running back toward the subject does. */
  leavesLeft?: boolean
  /** Enters the target's right side rather than its left. */
  entersRight?: boolean
  style: DependencyEdgeStyle
}

/** Resolved colours, because a CSS variable means nothing in a file opened outside the app. */
export interface DependencySvgTheme {
  background: string
  nodeFill: string
  nodeStroke: string
  nodeText: string
  subjectFill: string
  subjectStroke: string
  groupFill: string
  groupStroke: string
  groupText: string
  fontFamily: string
  fontSize: number
  borderRadius: number
}

export interface RenderDependencyMapSvgOptions {
  nodes: DependencySvgNode[]
  edges: DependencySvgEdge[]
  theme: DependencySvgTheme
}

const PADDING = 32
const NODE_TEXT_PADDING = 12
const GROUP_LABEL_OFFSET = { x: 12, y: 20 }
const MAX_LABEL_LINES = 2

/**
 * Draws the map as an SVG document, from the same geometry the screen shows.
 *
 * Built rather than screenshotted: the edges are SVG paths inside a transformed group, which DOM
 * rasterisers render unreliably — html2canvas drops most of them. Going back to the graph means the
 * curves come from the same `getBezierPath` React Flow draws with, so the file matches the canvas, and
 * the output is vector rather than a picture of a screen.
 */
export const renderDependencyMapSvg = ({
  nodes,
  edges,
  theme,
}: RenderDependencyMapSvgOptions): {
  svg: string
  width: number
  height: number
} => {
  const measurer = createTextMeasurer(theme.fontFamily)
  const byId = new Map(nodes.map((node) => [node.id, node]))

  const minX = Math.min(...nodes.map((n) => n.x))
  const minY = Math.min(...nodes.map((n) => n.y))
  const maxX = Math.max(...nodes.map((n) => n.x + n.width))
  const maxY = Math.max(...nodes.map((n) => n.y + n.height))

  const width = Math.ceil(maxX - minX + PADDING * 2)
  const height = Math.ceil(maxY - minY + PADDING * 2)
  const shiftX = PADDING - minX
  const shiftY = PADDING - minY

  // One arrowhead per colour in use: a marker's fill is fixed, so every stroke needs its own.
  const colours = [...new Set(edges.map((edge) => edge.style.stroke))]
  const markerId = (stroke: string) => `arrow-${colours.indexOf(stroke)}`

  const markers = colours
    .map(
      (stroke) =>
        `<marker id="${markerId(stroke)}" markerWidth="12" markerHeight="12" refX="9" refY="5" orient="auto">` +
        `<path d="M0,1 L9,5 L0,9 z" fill="${escapeXml(stroke)}" />` +
        `</marker>`,
    )
    .join('')

  // Boxes first, then edges, then nodes — the same stacking the canvas uses, so a curve passing a box
  // reads the same way in the file.
  const groups = nodes
    .filter((node) => node.isGroup)
    .map(
      (node) =>
        `<g>` +
        `<rect x="${node.x + shiftX}" y="${node.y + shiftY}" width="${node.width}" height="${node.height}" rx="${theme.borderRadius}" ` +
        `fill="${theme.groupFill}" stroke="${theme.groupStroke}" stroke-dasharray="4 4" />` +
        `<text x="${node.x + shiftX + GROUP_LABEL_OFFSET.x}" y="${node.y + shiftY + GROUP_LABEL_OFFSET.y}" ` +
        `font-family="${escapeXml(theme.fontFamily)}" font-size="${theme.fontSize}" font-weight="600" fill="${theme.groupText}">` +
        `${escapeXml(node.label)}</text>` +
        `</g>`,
    )
    .join('')

  const paths = edges
    .map((edge) => {
      const source = byId.get(edge.source)
      const target = byId.get(edge.target)
      if (!source || !target) return ''

      // The same sides the canvas joins: right to left unless the edge says otherwise.
      const [path] = getBezierPath({
        sourceX: source.x + shiftX + (edge.leavesLeft ? 0 : source.width),
        sourceY: source.y + shiftY + source.height / 2,
        sourcePosition: edge.leavesLeft ? Position.Left : Position.Right,
        targetX: target.x + shiftX + (edge.entersRight ? target.width : 0),
        targetY: target.y + shiftY + target.height / 2,
        targetPosition: edge.entersRight ? Position.Right : Position.Left,
      })

      const { stroke, width, dashed } = edge.style

      return (
        `<path d="${path}" fill="none" stroke="${escapeXml(stroke)}" ` +
        `stroke-width="${width}"${dashed ? ' stroke-dasharray="6 4"' : ''} ` +
        `marker-end="url(#${markerId(stroke)})" />`
      )
    })
    .join('')

  const products = nodes
    .filter((node) => !node.isGroup)
    .map((node) => {
      const lines = wrapToWidth(
        node.label,
        node.width - NODE_TEXT_PADDING * 2,
        theme.fontSize,
        MAX_LABEL_LINES,
        measurer,
      )

      const lineHeight = theme.fontSize * 1.4
      // Centred as a block: the first baseline sits half the block above the middle, plus the offset
      // from a line's top to its baseline.
      const firstBaseline =
        node.y +
        shiftY +
        node.height / 2 -
        ((lines.length - 1) * lineHeight) / 2 +
        theme.fontSize * 0.35

      const text = lines
        .map(
          (line, index) =>
            `<tspan x="${node.x + shiftX + node.width / 2}" y="${firstBaseline + index * lineHeight}">${escapeXml(line)}</tspan>`,
        )
        .join('')

      const fill = node.isSubject
        ? theme.subjectFill
        : node.isOverflow
          ? 'none'
          : theme.nodeFill
      const stroke =
        node.isSubject || node.isExpanded
          ? theme.subjectStroke
          : theme.nodeStroke

      return (
        `<g>` +
        `<rect x="${node.x + shiftX}" y="${node.y + shiftY}" width="${node.width}" height="${node.height}" rx="${theme.borderRadius}" ` +
        `fill="${fill}" stroke="${stroke}"${node.isOverflow ? ' stroke-dasharray="4 4"' : ''} />` +
        `<text text-anchor="middle" font-family="${escapeXml(theme.fontFamily)}" font-size="${theme.fontSize}" ` +
        `font-weight="${node.isSubject ? 600 : 400}" fill="${node.isOverflow ? theme.groupText : theme.nodeText}">${text}</text>` +
        `</g>`
      )
    })
    .join('')

  const svg =
    `<?xml version="1.0" encoding="UTF-8"?>` +
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">` +
    `<defs>${markers}</defs>` +
    `<rect width="${width}" height="${height}" fill="${theme.background}" />` +
    groups +
    paths +
    products +
    `</svg>`

  return { svg, width, height }
}
