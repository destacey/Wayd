'use client'

import { ProductActivityDto } from '@/src/services/wayd-api'
import { Empty, Flex, Tooltip, Typography, theme } from 'antd'
import type { GlobalToken } from 'antd'
import dayjs from 'dayjs'

const { Text } = Typography

/** Every day in the window, so a day with nothing released still gets a cell. */
const daysBetween = (from: string, to: string): string[] => {
  const days: string[] = []
  for (
    let day = dayjs(from);
    !day.isAfter(dayjs(to), 'day');
    day = day.add(1, 'day')
  ) {
    days.push(day.format('YYYY-MM-DD'))
  }
  return days
}

/**
 * A colour at a given opacity, composited over whatever is behind it.
 *
 * Falls back to the colour untouched if it is not the hex antd hands out, so an unexpected token
 * format degrades to a flat shade rather than an invalid style.
 */
export const withAlpha = (color: string, alpha: number): string => {
  const hex = color.trim()
  const short = /^#([\da-f])([\da-f])([\da-f])$/i.exec(hex)
  const long = /^#([\da-f]{2})([\da-f]{2})([\da-f]{2})/i.exec(hex)

  const parts = short
    ? short.slice(1).map((c) => parseInt(c + c, 16))
    : long
      ? long.slice(1).map((c) => parseInt(c, 16))
      : null

  return parts ? `rgba(${parts.join(', ')}, ${alpha})` : color
}

/**
 * How strongly each step is drawn, quietest first.
 *
 * One colour at rising opacity rather than a walk along antd's semantic tokens. Those are not
 * ordered by lightness — under the dark and slate algorithms `colorPrimaryHover` is *lighter* than
 * `colorPrimary` while `colorPrimaryActive` is darker — so a ramp built from them scrambled in
 * every theme but the default. Opacity is monotonic by construction, in any theme, and takes its
 * hue from whatever primary the active theme sets.
 */
const stepOpacity = [0.25, 0.45, 0.62, 0.8, 1]

/**
 * The steps of the scale, quietest first: none, one, two, three, four, five or more.
 *
 * Shared by the cells and the legend from one place: a legend naming a different set of shades from
 * the ones on screen is worse than no legend, and nothing would catch the drift.
 */
const shades = (token: GlobalToken): string[] => [
  token.colorFillQuaternary,
  ...stepOpacity.map((alpha) => withAlpha(token.colorPrimary, alpha)),
]

/** The step everything at or above it shares. */
const topLevel = 5

/**
 * Which step a count falls in.
 *
 * An absolute scale, not one relative to the busiest day on screen. A relative scale silently
 * redefines every shade when the window changes — the same cell means "a quiet day" in one window
 * and "the busiest day" in another, and two screenshots cannot be compared.
 *
 * Five steps and an "or more" because a day with twenty versions and a day with six are both simply
 * busy; grading beyond that spends shades on a distinction nobody acts on.
 */
const intensity = (count: number): number => Math.min(count, topLevel)

/**
 * What a shade stands for.
 *
 * Derived from the same constant the cells use rather than written out, so a change to the number
 * of steps cannot leave the legend describing the old scale.
 */
export const levelDescription = (level: number): string => {
  if (level === 0) return 'No versions'
  if (level >= topLevel) return `${topLevel} or more versions`
  return `${level} version${level === 1 ? '' : 's'}`
}

const cellHeight = 18
const cellGap = 3

/**
 * The product name column.
 *
 * A share of the width rather than a fixed size, so the grid uses whatever it is given — floored,
 * because a percentage alone collapses the names on a narrow screen.
 */
const nameColumn = 'minmax(140px, 26%)'

/** One step of indentation per level of the catalog. */
const indentPerLevel = 16

/**
 * The scale the grid is read against, for the card header.
 *
 * Separate from the grid so it can sit in the header rather than above the rows, where it would push
 * the first product down and read as part of the data.
 */
export const VersionActivityLegend = () => {
  const { token } = theme.useToken()
  const scale = shades(token)

  const swatch = {
    width: 16,
    height: cellHeight,
    borderRadius: 3,
    cursor: 'default',
  } as const

  return (
    <Flex align="center" gap={12} wrap>
      <Flex align="center" gap={6}>
        <Text type="secondary" style={{ fontSize: 12 }}>
          Fewer
        </Text>
        <Flex gap={cellGap}>
          {scale.map((background, level) => (
            <Tooltip key={level} title={levelDescription(level)}>
              <div style={{ ...swatch, background }} />
            </Tooltip>
          ))}
        </Flex>
        <Text type="secondary" style={{ fontSize: 12 }}>
          More
        </Text>
      </Flex>

      <Flex align="center" gap={6}>
        <Tooltip title="At least one version released that day has since been withdrawn.">
          <div
            style={{
              ...swatch,
              background: scale[3],
              outline: `2px solid ${token.colorError}`,
              outlineOffset: -2,
            }}
          />
        </Tooltip>
        <Text type="secondary" style={{ fontSize: 12 }}>
          Withdrawn
        </Text>
      </Flex>
    </Flex>
  )
}

export interface VersionActivityProps {
  activity: ProductActivityDto[]
  from: string
  to: string
}

/**
 * A day-by-day grid of versions released per product, as an indented catalog tree.
 *
 * Indented rather than grouped under each parent's name: grouping listed any node that is both
 * releasable and a parent twice, once as a row and again as a heading over its own children. The
 * rows arrive depth-first, so indenting is all it takes to show the hierarchy.
 *
 * Laid out as a grid rather than fixed-width cells so it uses the width it is given: the days share
 * whatever is left after the names, and a cell is a rectangle sized by the window rather than a
 * square that runs off the edge.
 *
 * Products that shipped nothing are kept: an absent row would read as "not in scope" rather than
 * "nothing shipped", and a quiet product is usually the thing worth noticing.
 */
const VersionActivity = ({ activity, from, to }: VersionActivityProps) => {
  const { token } = theme.useToken()
  const days = daysBetween(from, to)

  const counts = new Map<
    string,
    Map<string, { released: number; withdrawn: number }>
  >()
  for (const row of activity) {
    counts.set(
      row.product.id,
      new Map(
        row.days.map((day) => [
          dayjs(day.date).format('YYYY-MM-DD'),
          { released: day.released, withdrawn: day.withdrawn },
        ]),
      ),
    )
  }

  if (activity.length === 0) {
    return <Empty description="No releasable products in scope." />
  }

  const scale = shades(token)

  const rowGrid = {
    display: 'grid',
    gridTemplateColumns: `${nameColumn} 1fr`,
    gap: 8,
    alignItems: 'center',
  } as const

  const dayGrid = {
    display: 'grid',
    gridTemplateColumns: `repeat(${days.length}, minmax(0, 1fr))`,
    gap: cellGap,
  } as const

  return (
    <Flex vertical gap={4}>
      <div style={rowGrid}>
        <div />
        <div style={dayGrid}>
          {days.map((day) => (
            // The initial alone cannot say which week, and two Mondays look identical.
            <Tooltip key={day} title={dayjs(day).format('ddd D MMM YYYY')}>
              <Text
                type="secondary"
                style={{
                  fontSize: 11,
                  textAlign: 'center',
                  cursor: 'default',
                  overflow: 'hidden',
                }}
              >
                {dayjs(day).format('dd').charAt(0)}
              </Text>
            </Tooltip>
          ))}
        </div>
      </div>

      {activity.map((row) => (
        <div style={rowGrid} key={row.product.id}>
          <div
            style={{
              paddingLeft: row.depth * indentPerLevel,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
            title={row.product.name}
          >
            <Text type={row.isReleasable ? undefined : 'secondary'}>
              {row.product.name}
            </Text>
          </div>

          {row.isReleasable ? (
            <div style={dayGrid}>
              {days.map((day) => {
                const cell = counts.get(row.product.id)?.get(day)
                const released = cell?.released ?? 0
                const withdrawn = cell?.withdrawn ?? 0

                return (
                  <Tooltip
                    key={day}
                    title={`${row.product.name} · ${dayjs(day).format('D MMM')} · ${released} version${released === 1 ? '' : 's'}${withdrawn > 0 ? `, ${withdrawn} withdrawn` : ''}`}
                  >
                    <div
                      aria-label={`${row.product.name} ${day}: ${released} released`}
                      style={{
                        height: cellHeight,
                        borderRadius: 3,
                        background: scale[intensity(released)],
                        outline:
                          withdrawn > 0
                            ? `2px solid ${token.colorError}`
                            : undefined,
                        outlineOffset: -2,
                      }}
                    />
                  </Tooltip>
                )
              })}
            </div>
          ) : (
            // A grouping can never have versions cut against it, so an empty row of days would
            // claim it had a quiet fortnight rather than that the question does not apply.
            <div />
          )}
        </div>
      ))}
    </Flex>
  )
}

export default VersionActivity
