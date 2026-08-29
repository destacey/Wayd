'use client'

import dayjs from 'dayjs'
import { CSSProperties, FC } from 'react'
import { Card, Flex, Grid, Progress, Typography } from 'antd'

const { Text } = Typography
const { useBreakpoint } = Grid

const DATE_FORMAT = 'MMM D'

export interface TimelineProgressProps {
  start: Date | null
  end: Date | null
  variant?: 'outlined' | 'borderless'
  size?: 'default' | 'small'
  style?: CSSProperties
  dateFormat?: string
}

const TimelineProgress: FC<TimelineProgressProps> = ({
  start,
  end,
  variant = 'outlined',
  size = 'default',
  style,
  dateFormat = DATE_FORMAT,
}: TimelineProgressProps) => {
  const screens = useBreakpoint()
  const isMobile = !screens.md // Mobile/tablet when viewport is below the md breakpoint (< 768px)

  if (!start || !end) return null

  const now = dayjs()
  const startDate = dayjs(start)
  const endDate = dayjs(end)

  // For calendar day counting, ignore time and count inclusive days
  const startDay = startDate.startOf('day')
  const endDay = endDate.startOf('day')
  const today = now.startOf('day')

  const totalDays = endDay.diff(startDay, 'day') + 1
  const isFuture = now.isBefore(startDay)
  const daysUntilStart = isFuture ? startDay.diff(today, 'day') : 0

  const currentDay = isFuture
    ? 0
    : Math.min(Math.max(now.diff(startDay, 'day') + 1, 0), totalDays)
  const progressPercent = isFuture
    ? 0
    : Math.round((currentDay / totalDays) * 100)

  const fontSize = size === 'small' ? 11 : 12

  const content = (
    <Flex vertical gap={4} style={variant === 'borderless' ? style : undefined}>
      <Text type="secondary" style={{ fontSize }}>Timeline</Text>
      <Progress
        percent={progressPercent}
        showInfo={false}
        style={{ margin: 0 }}
      />
      <Flex justify="space-between">
        <Text type="secondary" style={{ fontSize }}>
          {startDate.format(dateFormat)}
        </Text>
        <Text type="secondary" style={{ fontSize }}>
          {endDate.format(dateFormat)}
        </Text>
      </Flex>
      <Flex justify="center">
        <Text type="secondary" style={{ fontSize }}>
          {isFuture
            ? `Starts in ${daysUntilStart} day${daysUntilStart !== 1 ? 's' : ''} · ${totalDays} day project`
            : `Day ${currentDay} of ${totalDays} (${progressPercent}%)`}
        </Text>
      </Flex>
    </Flex>
  )

  if (variant === 'borderless') {
    return content
  }

  const cardStyle = isMobile
    ? { width: '100%', ...style }
    : { minWidth: 275, width: 'fit-content', ...style }

  return (
    <Card size="small" style={cardStyle} variant={variant}>
      {content}
    </Card>
  )
}

export default TimelineProgress
