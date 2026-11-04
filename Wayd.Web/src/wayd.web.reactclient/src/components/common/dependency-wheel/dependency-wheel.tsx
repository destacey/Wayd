'use client'

import { DependencyHealth } from '@/src/components/types'
import { softenChartColor } from '@/src/utils'
import { DownloadOutlined } from '@ant-design/icons'
import { Button, Dropdown, Flex, theme, type GlobalToken } from 'antd'
import {
  chordDirected,
  ribbonArrow,
  type Chord,
  type ChordSubgroup,
} from 'd3-chord'
import { arc, type DefaultArcObject } from 'd3-shape'
import {
  FocusEvent,
  KeyboardEvent,
  MouseEvent,
  useEffect,
  useRef,
  useState,
} from 'react'
import {
  downloadSvg,
  downloadSvgAsPng,
} from '../timeline/render/svg/export-svg'
import { DEPENDENCY_HEALTH_VALUES } from '../work/dependency-health-colors'
import {
  DependencyWheelData,
  DependencyWheelSelection,
  NO_TEAM_KEY,
} from './dependency-wheel-data'
import styles from './dependency-wheel.module.css'
import DependencyWheelTooltip, {
  DependencyWheelTooltipSubject,
} from './dependency-wheel-tooltip'

// How far the summary card sits from the pointer.
const TOOLTIP_OFFSET = 14

// Drawn in a square scaled to fit, with a margin past the arcs just wide enough for the longest label.
const OUTER_RADIUS = 200
const INNER_RADIUS = 186
const LABEL_RADIUS = OUTER_RADIUS + 8

// Label size in drawing units. It grows as the wheel is scaled down, so labels stay readable on a phone.
const LABEL_SCREEN_PX = 12
const LABEL_MIN = 13
const LABEL_MAX = 20
// The average glyph width of the UI font, as a share of its size, for sizing the label margin.
const GLYPH_WIDTH = 0.62

/** The side of the square the wheel is drawn in, leaving room for its longest label at the largest size. */
const viewSize = (longestLabel: number) =>
  2 * Math.ceil(LABEL_RADIUS + longestLabel * GLYPH_WIDTH * LABEL_MAX + 6)

const LIT_OPACITY = 0.65
const DIM_OPACITY = 0.08
const ARC_DIM_OPACITY = 0.3

// Red and green are left out, so a team's colour is never read as a health.
const teamPalette = (token: GlobalToken) => [
  token.blue,
  token.purple,
  token.cyan,
  token.magenta,
  token.gold,
  token.geekblue,
  token.lime,
  token.volcano,
  token.orange,
]

export interface DependencyWheelProps {
  data: DependencyWheelData
  selection: DependencyWheelSelection | null
  onSelectionChange: (selection: DependencyWheelSelection | null) => void
  /** Names the downloaded image. */
  exportFileName: string
}

const sameSelection = (
  a: DependencyWheelSelection | null,
  b: DependencyWheelSelection,
) =>
  !!a &&
  (a.kind === 'group' && b.kind === 'group'
    ? a.key === b.key
    : a.kind === 'flow' &&
      b.kind === 'flow' &&
      a.sourceKey === b.sourceKey &&
      a.targetKey === b.targetKey)

const healthName = (health: DependencyHealth) =>
  DEPENDENCY_HEALTH_VALUES.find((v) => v.health === health)?.name ?? 'Unknown'

const plural = (count: number) =>
  `${count} ${count === 1 ? 'dependency' : 'dependencies'}`

const activateOnKey =
  (action: () => void) => (event: KeyboardEvent<SVGElement>) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      action()
    }
  }

const arcPath = arc<DefaultArcObject>()
const ribbonGenerator = ribbonArrow<Chord, ChordSubgroup>()
  .radius(INNER_RADIUS - 1)
  .padAngle(1 / INNER_RADIUS)
  .headRadius(10)

// Called without a canvas context, the generator returns the path string. Its typings list the
// canvas-context overload (returning void) first, so TypeScript picks that one.
const ribbonPath = (chord: Chord) =>
  ribbonGenerator(chord) as unknown as string | null

/** The label size, in drawing units, for the width the wheel is drawn at. */
const useLabelSize = (
  svgRef: React.RefObject<SVGSVGElement | null>,
  view: number,
) => {
  const [size, setSize] = useState(LABEL_MIN)

  useEffect(() => {
    const svg = svgRef.current
    if (!svg || typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(([entry]) => {
      const width = entry.contentRect.width
      if (width <= 0) return
      const scaled = (LABEL_SCREEN_PX * view) / width
      setSize(Math.min(LABEL_MAX, Math.max(LABEL_MIN, Math.round(scaled))))
    })
    observer.observe(svg)
    return () => observer.disconnect()
  }, [svgRef, view])

  return size
}

/**
 * A directed chord diagram of dependencies between teams. Each arc is a team, sized by the dependencies it
 * takes part in; each ribbon runs from the team that owns the predecessors to the team waiting on them, in
 * the predecessors' team's colour. Teams take the theme's preset colours in name order, repeating past nine;
 * items with no team are grey. Picking an arc or ribbon reports it, so the caller can narrow a
 * list to match; picking it again clears it.
 *
 * The arcs are in the tab order. A ribbon joins it once one of its teams is picked, so a keyboard user
 * picks a team and then one of its directions, without tabbing past every ribbon on the wheel.
 */
const DependencyWheel = ({
  data,
  selection,
  onSelectionChange,
  exportFileName,
}: DependencyWheelProps) => {
  const { token } = theme.useToken()
  const svgRef = useRef<SVGSVGElement>(null)
  const canvasRef = useRef<HTMLDivElement>(null)
  const [pointer, setPointer] = useState<{
    x: number
    y: number
    flip: boolean
  } | null>(null)
  const { groups, matrix, flows } = data
  const view = viewSize(Math.max(1, ...groups.map((g) => g.label.length)))
  const labelSize = useLabelSize(svgRef, view)
  const [hovered, setHovered] = useState<DependencyWheelSelection | null>(null)

  const layout = chordDirected()
    .padAngle(groups.length > 1 ? 0.04 : 0)
    .sortSubgroups((a, b) => b - a)(matrix)

  const flowHealth = new Map(
    flows.map((f) => [`${f.sourceKey}>${f.targetKey}`, f.health]),
  )

  // Softened toward the card, as the app's other charts are, so the wheel does not outshout the page.
  const palette = teamPalette(token).map((color) =>
    softenChartColor(color, token.colorBgContainer),
  )
  const teamColor = (index: number) =>
    groups[index].key === NO_TEAM_KEY
      ? token.colorTextTertiary
      : palette[index % palette.length]

  const focus = hovered ?? selection

  const groupLit = (index: number) =>
    !focus ||
    (focus.kind === 'group'
      ? groups[index].key === focus.key ||
        flows.some(
          (f) =>
            (f.sourceKey === focus.key && f.targetKey === groups[index].key) ||
            (f.targetKey === focus.key && f.sourceKey === groups[index].key),
        )
      : groups[index].key === focus.sourceKey ||
        groups[index].key === focus.targetKey)

  const touches = (
    pick: DependencyWheelSelection | null,
    sourceKey: string,
    targetKey: string,
  ) =>
    !!pick &&
    (pick.kind === 'group'
      ? sourceKey === pick.key || targetKey === pick.key
      : sourceKey === pick.sourceKey && targetKey === pick.targetKey)

  const pointAt = (clientX: number, clientY: number) => {
    const canvas = canvasRef.current?.getBoundingClientRect()
    if (!canvas) return
    const x = clientX - canvas.left
    // Past the middle, the card opens to the left of the pointer so it stays over the wheel.
    setPointer({ x, y: clientY - canvas.top, flip: x > canvas.width / 2 })
  }
  const pointAtElement = (event: FocusEvent<SVGElement>) => {
    const box = event.currentTarget.getBoundingClientRect()
    pointAt(box.left + box.width / 2, box.top + box.height / 2)
  }
  const hover = (pick: DependencyWheelSelection) => (event: MouseEvent) => {
    setHovered(pick)
    pointAt(event.clientX, event.clientY)
  }
  const focusOn =
    (pick: DependencyWheelSelection) => (event: FocusEvent<SVGElement>) => {
      setHovered(pick)
      pointAtElement(event)
    }
  const leave = () => {
    setHovered(null)
    setPointer(null)
  }

  const groupIndex = (key: string) => groups.findIndex((g) => g.key === key)
  const tooltipSubject = ((): DependencyWheelTooltipSubject | null => {
    if (!hovered) return null
    if (hovered.kind === 'group') {
      const index = groupIndex(hovered.key)
      return index < 0
        ? null
        : { kind: 'group', group: groups[index], color: teamColor(index) }
    }
    const flow = flows.find(
      (f) =>
        f.sourceKey === hovered.sourceKey && f.targetKey === hovered.targetKey,
    )
    const sourceIndex = groupIndex(hovered.sourceKey)
    const targetIndex = groupIndex(hovered.targetKey)
    return !flow || sourceIndex < 0 || targetIndex < 0
      ? null
      : {
          kind: 'flow',
          flow,
          source: groups[sourceIndex],
          target: groups[targetIndex],
          sourceColor: teamColor(sourceIndex),
          targetColor: teamColor(targetIndex),
        }
  })()

  const select = (next: DependencyWheelSelection) =>
    onSelectionChange(sameSelection(selection, next) ? null : next)

  const serialize = () => {
    const svg = svgRef.current
    if (!svg) return null
    const clone = svg.cloneNode(true) as SVGSVGElement
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg')
    clone.setAttribute('width', String(view))
    clone.setAttribute('height', String(view))
    // The page background is not part of the drawing, so give the image its own.
    const background = document.createElementNS(
      'http://www.w3.org/2000/svg',
      'rect',
    )
    background.setAttribute('x', String(-view / 2))
    background.setAttribute('y', String(-view / 2))
    background.setAttribute('width', String(view))
    background.setAttribute('height', String(view))
    background.setAttribute('fill', token.colorBgContainer)
    clone.insertBefore(background, clone.firstChild)
    return new XMLSerializer().serializeToString(clone)
  }

  const exportImage = async (format: 'svg' | 'png') => {
    const svg = serialize()
    if (!svg) return
    if (format === 'svg') downloadSvg(svg, `${exportFileName}.svg`)
    else
      await downloadSvgAsPng(svg, `${exportFileName}.png`, {
        width: view,
        height: view,
        scale: 2,
      })
  }

  return (
    <Flex vertical gap="small">
      <div ref={canvasRef} className={styles.canvas}>
        <svg
          ref={svgRef}
          className={styles.wheel}
          viewBox={`${-view / 2} ${-view / 2} ${view} ${view}`}
          role="group"
          aria-label="Dependencies between teams"
          onMouseLeave={leave}
          onMouseMove={(event) =>
            hovered && pointAt(event.clientX, event.clientY)
          }
        >
          {/* Arcs first, so they come first in the tab order; ribbons sit inside the arcs, so nothing overlaps. */}
          <g>
            {layout.groups.map((g) => {
              const group = groups[g.index]
              const pick: DependencyWheelSelection = {
                kind: 'group',
                key: group.key,
              }
              const picked = sameSelection(selection, pick)
              const label = `${group.name}: predecessor in ${plural(group.outgoing)}, successor in ${plural(group.incoming)}`
              const angle = (g.startAngle + g.endAngle) / 2
              const flipped = angle > Math.PI
              return (
                <g key={group.key}>
                  <path
                    className={styles.arc}
                    d={
                      arcPath({
                        innerRadius: INNER_RADIUS,
                        outerRadius: OUTER_RADIUS,
                        startAngle: g.startAngle,
                        endAngle: g.endAngle,
                      }) ?? undefined
                    }
                    fill={teamColor(g.index)}
                    fillOpacity={groupLit(g.index) ? 1 : ARC_DIM_OPACITY}
                    stroke={picked ? token.colorText : undefined}
                    strokeWidth={picked ? 2 : undefined}
                    role="button"
                    tabIndex={0}
                    aria-label={label}
                    aria-pressed={picked}
                    onMouseEnter={hover(pick)}
                    onFocus={focusOn(pick)}
                    onBlur={leave}
                    onClick={() => select(pick)}
                    onKeyDown={activateOnKey(() => select(pick))}
                  />
                  <text
                    className={styles.label}
                    transform={`rotate(${(angle * 180) / Math.PI - 90}) translate(${LABEL_RADIUS}) ${flipped ? 'rotate(180)' : ''}`}
                    textAnchor={flipped ? 'end' : 'start'}
                    dominantBaseline="middle"
                    fill={
                      groupLit(g.index)
                        ? token.colorText
                        : token.colorTextQuaternary
                    }
                    fontFamily={token.fontFamily}
                    fontSize={labelSize}
                    fontWeight={picked ? 600 : undefined}
                    aria-hidden
                  >
                    {group.label}
                  </text>
                </g>
              )
            })}
          </g>
          <g>
            {layout.map((chord) => {
              const sourceGroup = groups[chord.source.index]
              const targetGroup = groups[chord.target.index]
              const health =
                flowHealth.get(`${sourceGroup.key}>${targetGroup.key}`) ??
                DependencyHealth.Unknown
              const flow: DependencyWheelSelection = {
                kind: 'flow',
                sourceKey: sourceGroup.key,
                targetKey: targetGroup.key,
              }
              const label = `${sourceGroup.name} → ${targetGroup.name}: ${plural(chord.source.value)}, worst health ${healthName(health)}`
              return (
                <path
                  key={`${sourceGroup.key}>${targetGroup.key}`}
                  className={styles.ribbon}
                  d={ribbonPath(chord) ?? undefined}
                  fill={teamColor(chord.source.index)}
                  fillOpacity={
                    !focus || touches(focus, sourceGroup.key, targetGroup.key)
                      ? LIT_OPACITY
                      : DIM_OPACITY
                  }
                  stroke={token.colorBgContainer}
                  strokeWidth={0.5}
                  role="button"
                  tabIndex={
                    touches(selection, sourceGroup.key, targetGroup.key)
                      ? 0
                      : -1
                  }
                  aria-label={label}
                  aria-pressed={sameSelection(selection, flow)}
                  onMouseEnter={hover(flow)}
                  onFocus={focusOn(flow)}
                  onBlur={leave}
                  onClick={() => select(flow)}
                  onKeyDown={activateOnKey(() => select(flow))}
                />
              )
            })}
          </g>
        </svg>
        {/* The essentials are in each shape's accessible name, so the card is for sighted users only. */}
        {tooltipSubject && pointer && (
          <div
            className={styles.tooltip}
            style={{
              left: pointer.x,
              top: pointer.y,
              transform: pointer.flip
                ? `translate(calc(-100% - ${TOOLTIP_OFFSET}px), ${TOOLTIP_OFFSET}px)`
                : `translate(${TOOLTIP_OFFSET}px, ${TOOLTIP_OFFSET}px)`,
            }}
            aria-hidden
          >
            <DependencyWheelTooltip subject={tooltipSubject} />
          </div>
        )}
      </div>
      <Flex justify="flex-end">
        <Dropdown
          menu={{
            items: [
              { key: 'png', label: 'Download PNG' },
              { key: 'svg', label: 'Download SVG' },
            ],
            onClick: ({ key }) => exportImage(key as 'svg' | 'png'),
          }}
        >
          <Button
            type="text"
            size="small"
            icon={<DownloadOutlined />}
            aria-label="Download the dependency wheel"
          />
        </Dropdown>
      </Flex>
    </Flex>
  )
}

export default DependencyWheel
