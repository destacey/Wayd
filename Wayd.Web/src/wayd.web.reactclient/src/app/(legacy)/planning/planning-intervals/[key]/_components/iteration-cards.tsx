'use client'

import { useGetPlanningIntervalIterationsQuery } from '@/src/store/features/planning/planning-interval-api'
import { Card, Flex, Skeleton, Typography } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import IterationHealthFlag from './iteration-health-flag'

const { Text } = Typography

const formatRange = (start: Date, end: Date) => {
  const startD = dayjs(start)
  const endD = dayjs(end)
  const sameYear = startD.isSame(endD, 'year')
  const format = sameYear ? 'MMM D' : 'MMM D, YYYY'
  return `${startD.format(format)} – ${endD.format('MMM D, YYYY')}`
}

// Inclusive day counts so an iteration that starts and ends on the same day
// reads as "Day 1 / 1" rather than "Day 0 / 0".
const activeDayInfo = (start: Date, end: Date) => {
  const startD = dayjs(start).startOf('day')
  const endD = dayjs(end).startOf('day')
  const today = dayjs(new Date()).startOf('day')
  const totalDays = endD.diff(startD, 'day') + 1
  const currentDay = Math.min(
    Math.max(today.diff(startD, 'day') + 1, 1),
    totalDays,
  )
  return { currentDay, totalDays }
}

const IterationCards = ({ piKey }: { piKey: number }) => {
  const { data: iterations, isLoading } =
    useGetPlanningIntervalIterationsQuery(piKey)

  if (isLoading) {
    return <Skeleton active paragraph={{ rows: 2 }} />
  }

  if (!iterations || iterations.length === 0) {
    return null
  }

  const ordered = [...iterations].sort(
    (a, b) => new Date(a.start).getTime() - new Date(b.start).getTime(),
  )

  return (
    <Card size="small" title="Iterations">
      <Flex gap={12} wrap>
        {ordered.map((iteration) => {
          const isActive = iteration.state === 'Active'
          const isIP =
            iteration.category.name === 'IP' ||
            /innovation/i.test(iteration.category.name)
          const dayInfo = isActive
            ? activeDayInfo(iteration.start, iteration.end)
            : null

          const accentBg = isActive
            ? 'var(--ant-color-primary-bg)'
            : isIP
              ? 'var(--ant-color-warning-bg)'
              : undefined
          const accentText = isActive
            ? 'var(--ant-color-primary-text)'
            : isIP
              ? 'var(--ant-color-warning-text)'
              : undefined

          return (
            <Link
              key={iteration.key}
              href={`/planning/planning-intervals/${piKey}/iterations/${iteration.key}`}
              style={{ flex: '0 0 200px' }}
            >
              <Card
                size="small"
                style={{ height: '100%' }}
                styles={{
                  body: {
                    padding: 12,
                    backgroundColor: accentBg,
                  },
                }}
                hoverable
              >
                <Flex vertical gap={2}>
                  <Flex justify="space-between" align="center" gap={8}>
                    <Text strong style={{ color: accentText }}>
                      {iteration.name}
                    </Text>
                    {isActive && (
                      <IterationHealthFlag
                        piKey={piKey}
                        iterationKey={iteration.key}
                        start={iteration.start}
                        end={iteration.end}
                      />
                    )}
                  </Flex>
                  <Text
                    type="secondary"
                    style={{ fontSize: 12, textTransform: 'uppercase' }}
                  >
                    {iteration.category.name}
                    {dayInfo &&
                      ` · Day ${dayInfo.currentDay}/${dayInfo.totalDays}`}
                  </Text>
                  <Text style={{ fontSize: 12 }}>
                    {formatRange(iteration.start, iteration.end)}
                  </Text>
                </Flex>
              </Card>
            </Link>
          )
        })}
      </Flex>
    </Card>
  )
}

export default IterationCards

