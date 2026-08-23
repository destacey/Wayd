import { Card, Flex, Statistic, StatisticProps } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { FC, ReactNode } from 'react'

const { Meta } = Card

export interface MetricCardProps extends Omit<StatisticProps, 'valueStyle'> {
  cardStyle?: React.CSSProperties
  statisticStyle?: React.CSSProperties
  tooltip?: string
  /**
   * Where the `tooltip` is anchored. Defaults to `'title'` so metric
   * interactions are not blocked by a card-level tooltip.
   */
  tooltipTarget?: 'card' | 'title'
  secondaryValue?: ReactNode
  // Support both old valueStyle (for backwards compatibility) and new styles.content
  valueStyle?: React.CSSProperties
  /**
   * When true, renders without the card's border or hover affordance — for
   * cases where the metric sits inside another card and the nested chrome
   * would feel doubled-up.
   */
  embedded?: boolean
  hoverable?: boolean
  /**
   * Makes the card a link to where the metric comes from — typically the
   * section that lists what it counts.
   *
   * Sets the hover affordance automatically, and gives the card a button role
   * with Enter/Space handling, since a click target that only responds to a
   * mouse is unreachable by keyboard.
   */
  onClick?: () => void
  /** Accessible name for the link. Falls back to the title when it is a string. */
  ariaLabel?: string
}

const MetricCard: FC<MetricCardProps> = ({
  cardStyle,
  statisticStyle,
  tooltip,
  tooltipTarget = 'title',
  secondaryValue,
  valueStyle,
  styles,
  embedded = false,
  hoverable = false,
  onClick,
  ariaLabel,
  title,
  ...statisticProps
}) => {
  const interactiveProps = onClick
    ? {
        onClick,
        onKeyDown: (e: React.KeyboardEvent) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault()
            onClick()
          }
        },
        role: 'button' as const,
        tabIndex: 0,
        'aria-label':
          ariaLabel ?? (typeof title === 'string' ? title : undefined),
      }
    : {}

  const defaultCardStyle = cardStyle ?? { height: '100%' }
  const defaultStatisticStyle = statisticStyle ?? { whiteSpace: 'nowrap' }

  // Migrate deprecated valueStyle to new styles.content format
  const statisticStyles = valueStyle
    ? { ...styles, content: valueStyle }
    : styles

  const titleNode =
    tooltip && tooltipTarget === 'title' ? (
      <WaydTooltip title={tooltip} helpCursor>
        <span>{title}</span>
      </WaydTooltip>
    ) : (
      title
    )

  // Embedded mode: skip the Card wrapper entirely — the metric is nested in
  // another card already, so the body padding / background of an inner card
  // reads as visual noise even with `variant="borderless"`.
  const inner = embedded ? (
    <Flex vertical>
      <Statistic
        {...statisticProps}
        title={titleNode}
        style={defaultStatisticStyle}
        styles={statisticStyles}
      />
      {secondaryValue !== undefined && (
        <Flex justify="flex-end">{secondaryValue}</Flex>
      )}
    </Flex>
  ) : (
    <Card
      style={defaultCardStyle}
      size="small"
      hoverable={hoverable || !!onClick}
      {...interactiveProps}
    >
      <Statistic
        {...statisticProps}
        title={titleNode}
        style={defaultStatisticStyle}
        styles={statisticStyles}
      />
      {secondaryValue !== undefined && (
        <Meta description={<Flex justify="flex-end">{secondaryValue}</Flex>} />
      )}
    </Card>
  )

  return tooltip && tooltipTarget === 'card' ? (
    <WaydTooltip title={tooltip} helpCursor>
      {inner}
    </WaydTooltip>
  ) : (
    inner
  )
}

export default MetricCard
