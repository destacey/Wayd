'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { IterationState } from '@/src/components/types'
import { PlanningIntervalListDto } from '@/src/services/wayd-api'
import {
  useGetPlanningIntervalIterationsQuery,
  useGetPlanningIntervalsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import {
  calendarDaysBetween,
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
  todayCalendarDate,
} from '@/src/utils'
import { Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** How far ahead a future planning interval is listed, in days. */
export const UPCOMING_WINDOW_DAYS = 14

/** Active planning intervals, and future ones starting within the window, soonest first. */
export const currentPlanningIntervals = (
  planningIntervals: PlanningIntervalListDto[],
  today: CalendarDate,
): PlanningIntervalListDto[] =>
  planningIntervals
    .filter((pi) => {
      const state = pi.state.id as IterationState
      return (
        state === IterationState.Active ||
        (state === IterationState.Future &&
          calendarDaysBetween(today, pi.start) <= UPCOMING_WINDOW_DAYS)
      )
    })
    .sort((a, b) => compareCalendarDates(a.start, b.start))

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

/**
 * How long a planning interval has left, or until it starts. Days left count
 * today, so the last day reads "1 day left", never "0 days left".
 */
export const countdownText = (
  pi: PlanningIntervalListDto,
  today: CalendarDate,
): string =>
  (pi.state.id as IterationState) === IterationState.Active
    ? `${plural(calendarDaysBetween(today, pi.end) + 1, 'day')} left`
    : `starts in ${plural(calendarDaysBetween(today, pi.start), 'day')}`

const PlanningIntervalRow: FC<{
  pi: PlanningIntervalListDto
  today: CalendarDate
}> = ({ pi, today }) => {
  const isActive = (pi.state.id as IterationState) === IterationState.Active
  const { data: iterations } = useGetPlanningIntervalIterationsQuery(pi.key, {
    skip: !isActive,
  })
  const activeIteration = iterations?.find((i) => i.state === 'Active')
  const base = `/planning/planning-intervals/${pi.key}`

  return (
    <WaydList.Item className={styles.listItem}>
      <div className={styles.listItemBody}>
        <EntityLink href={base}>{pi.name}</EntityLink>
        <span className={styles.listItemMeta}>
          <Text type="secondary">
            {formatCalendarDate(pi.start)} – {formatCalendarDate(pi.end)}
          </Text>
          <Text type="secondary">{countdownText(pi, today)}</Text>
        </span>
        <span className={styles.listItemMeta}>
          <Link href={`${base}?section=plan-review`}>Plan Review</Link>
          {activeIteration ? (
            <Link href={`${base}/iterations/${activeIteration.key}`}>
              {activeIteration.name}
            </Link>
          ) : null}
        </span>
      </div>
    </WaydList.Item>
  )
}

/** The planning intervals running now or starting soon, with a way into each. */
const PlanningIntervalsWidget: FC<HomeWidgetProps> = ({ title }) => {
  const { data, isLoading, isError } = useGetPlanningIntervalsQuery()
  const today = todayCalendarDate()
  const planningIntervals = currentPlanningIntervals(data ?? [], today)

  const body = () => {
    if (isError) {
      return <WidgetError message="Could not load planning intervals." />
    }
    if (planningIntervals.length === 0) {
      return (
        <WidgetEmpty
          message={`No planning interval is active or starts in the next ${UPCOMING_WINDOW_DAYS} days.`}
          link={{
            href: '/planning/planning-intervals',
            label: 'All planning intervals',
          }}
        />
      )
    }
    return (
      <WaydList size="small">
        {planningIntervals.map((pi) => (
          <PlanningIntervalRow key={pi.key} pi={pi} today={today} />
        ))}
      </WaydList>
    )
  }

  return (
    <WidgetCard
      title={title}
      viewAllHref="/planning/planning-intervals"
      loading={isLoading}
      failed={isError}
      empty={planningIntervals.length === 0}
      emptySummary="None active or starting soon"
    >
      {body()}
    </WidgetCard>
  )
}

export default PlanningIntervalsWidget
