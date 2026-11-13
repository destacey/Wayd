'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import { MyProjectDeliverableDto } from '@/src/services/wayd-api'
import { useGetMyProjectDeliverablesQuery } from '@/src/store/features/ppm/project-deliverables-api'
import {
  addCalendarDays,
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
  todayCalendarDate,
} from '@/src/utils'
import { Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { rowsToShow } from '../widget-settings'
import { HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** The most deliverables the widget lists; the rest are counted below them. */
export const MY_DELIVERABLES_LIMIT = 5

/** How far ahead a target date puts a deliverable on the list. */
export const DELIVERABLES_DUE_SOON_DAYS = 14

/**
 * The deliverables the widget lists — past target, or with a target within
 * {@link DELIVERABLES_DUE_SOON_DAYS} days — and how many fall later or have no
 * target date. The home page is a briefing, so those are counted, not listed.
 */
export const deliverablesDueSoon = (
  deliverables: MyProjectDeliverableDto[],
  today: CalendarDate,
): { soon: MyProjectDeliverableDto[]; laterCount: number } => {
  const horizon = addCalendarDays(today, DELIVERABLES_DUE_SOON_DAYS)
  const soon = deliverables.filter(
    (d) => !!d.targetDate && compareCalendarDates(d.targetDate, horizon) <= 0,
  )
  return { soon, laterCount: deliverables.length - soon.length }
}

const plural = (n: number, one: string, many: string) =>
  `${n} ${n === 1 ? one : many}`

const Target: FC<{
  deliverable: MyProjectDeliverableDto
  today: CalendarDate
}> = ({ deliverable, today }) => {
  if (!deliverable.targetDate)
    return <Text type="secondary">No target date</Text>
  const label = `Target ${formatCalendarDate(deliverable.targetDate)}`
  if (compareCalendarDates(deliverable.targetDate, today) < 0)
    return <Text type="danger">{label} (past target)</Text>
  return <Text type="secondary">{label}</Text>
}

/**
 * The open deliverables the caller owns that are past target or due within
 * two weeks, past target first, in the order the server gives; the rest are
 * counted, not listed.
 */
const MyDeliverablesWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, MY_DELIVERABLES_LIMIT)
  const {
    data: deliverables,
    isLoading,
    isError,
  } = useGetMyProjectDeliverablesQuery()
  const today = todayCalendarDate()

  const body = () => {
    if (isError)
      return <WidgetError message="Could not load your deliverables." />
    if (!deliverables || deliverables.length === 0) {
      return (
        <WidgetEmpty message="You own no open deliverables. They appear here when a project names you as a deliverable's owner." />
      )
    }
    const { soon, laterCount } = deliverablesDueSoon(deliverables, today)
    if (soon.length === 0) {
      return (
        <WidgetEmpty
          message={`None is due in the next ${DELIVERABLES_DUE_SOON_DAYS} days. You own ${plural(laterCount, 'open deliverable', 'open deliverables')} due later.`}
        />
      )
    }
    const more = soon.length - rows
    return (
      <>
        <WaydList size="small">
          {soon.slice(0, rows).map((deliverable) => (
            <WaydList.Item key={deliverable.id} className={styles.listItem}>
              <div className={styles.listItemBody}>
                <EntityLink
                  href={`/ppm/projects/${deliverable.project.key}?section=deliverables`}
                >
                  {deliverable.name}
                </EntityLink>
                <span className={styles.listItemMeta}>
                  <WorkflowStatusTag
                    name={deliverable.status.name}
                    category={deliverable.status.category}
                  />
                  <Target deliverable={deliverable} today={today} />
                  <Link href={`/ppm/projects/${deliverable.project.key}`}>
                    {deliverable.project.name}
                  </Link>
                </span>
              </div>
            </WaydList.Item>
          ))}
        </WaydList>
        {more > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            And {more} more due in the next {DELIVERABLES_DUE_SOON_DAYS} days.
          </Text>
        ) : null}
        {laterCount > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            You own{' '}
            {plural(
              laterCount,
              'more open deliverable',
              'more open deliverables',
            )}{' '}
            due later.
          </Text>
        ) : null}
      </>
    )
  }

  return (
    <WidgetCard
      title={title}
      loading={isLoading}
      failed={isError}
      // Not when only later deliverables remain: they are counted, and hiding them would hide work.
      empty={!deliverables || deliverables.length === 0}
      emptySummary="No open deliverables"
    >
      {body()}
    </WidgetCard>
  )
}

export default MyDeliverablesWidget
