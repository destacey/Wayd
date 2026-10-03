'use client'

import { CSSProperties, FC } from 'react'
import { Card, Flex, Grid, Progress, Typography } from 'antd'
import {
  calendarDaysBetween,
  CalendarDate,
  parseCalendarDate,
  todayCalendarDate,
} from '@/src/utils/calendar-date'

const { Text } = Typography
const { useBreakpoint } = Grid

const DATE_FORMAT = 'MMM D'

export interface TimelineProgressProps {
  start: CalendarDate | null
  end: CalendarDate | null
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

  const startDay = parseCalendarDate(start)
  const endDay = parseCalendarDate(end)

  const totalDays = calendarDaysBetween(start, end) + 1
  const daysUntilStart = Math.max(
    calendarDaysBetween(todayCalendarDate(), start),
    0,
  )
  const isFuture = daysUntilStart > 0

  const currentDay = isFuture
    ? 0
    : Math.min(calendarDaysBetween(start, todayCalendarDate()) + 1, totalDays)
  const progressPercent = isFuture
    ? 0
    : Math.round((currentDay / totalDays) * 100)

  const fontSize = size === 'small' ? 11 : 12

  const content = (
    <Flex vertical gap={4} style={variant === 'borderless' ? style : undefined}>
      <Text type="secondary" style={{ fontSize }}>
        Timeline
      </Text>
      <Progress
        percent={progressPercent}
        showInfo={false}
        style={{ margin: 0 }}
      />
      <Flex justify="space-between">
        <Text type="secondary" style={{ fontSize }}>
          {startDay.format(dateFormat)}
        </Text>
        <Text type="secondary" style={{ fontSize }}>
          {endDay.format(dateFormat)}
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
