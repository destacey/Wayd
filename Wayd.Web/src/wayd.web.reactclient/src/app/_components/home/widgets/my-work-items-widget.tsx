'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import ExternalIconLink from '@/src/components/common/external-icon-link'
import WorkStatusTag from '@/src/components/common/work/work-status-tag'
import { StatusCategoryId } from '@/src/components/types'
import { StatusCategory, WorkItemListDto } from '@/src/services/wayd-api'
import { useGetMyWorkItemsQuery } from '@/src/store/features/user-management/me-api'
import { Typography } from 'antd'
import { FC } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { rowsToShow } from '../widget-settings'
import { HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** The most work items the widget lists; the rest are counted below them. */
export const MY_WORK_ITEMS_LIMIT = 8

/** How many days, today included, the done count looks back over. */
export const RECENTLY_DONE_DAYS = 7

const OPEN_CATEGORIES = [StatusCategory.Active, StatusCategory.Proposed]
const DONE_CATEGORIES = [StatusCategory.Done]

/**
 * The start of the first of the last {@link RECENTLY_DONE_DAYS} days, today
 * included, as an ISO instant. Fixed to the day, so every render in it asks
 * the same question and shares one cached answer.
 */
export const recentlyDoneSince = (now: Date = new Date()): string => {
  const since = new Date(now)
  since.setHours(0, 0, 0, 0)
  since.setDate(since.getDate() - (RECENTLY_DONE_DAYS - 1))
  return since.toISOString()
}

/**
 * Active work items first, then Proposed, each keeping the server's order
 * (stack rank). Any other category, which the request leaves out, is dropped.
 */
export const groupMyWorkItems = (
  workItems: WorkItemListDto[],
): WorkItemListDto[] => {
  const inCategory = (category: StatusCategoryId) =>
    workItems.filter((w) => w.statusCategory.id === category)
  return [
    ...inCategory(StatusCategoryId.Active),
    ...inCategory(StatusCategoryId.Proposed),
  ]
}

const plural = (n: number, one: string, many: string) =>
  `${n} ${n === 1 ? one : many}`

const WorkItemRow: FC<{ workItem: WorkItemListDto }> = ({ workItem }) => (
  <WaydList.Item className={styles.listItem}>
    <div className={styles.listItemBody}>
      <span>
        <ExternalIconLink
          content={
            <EntityLink
              href={`/work/workspaces/${workItem.workspace.key}/work-items/${workItem.key}`}
            >
              {workItem.key} · {workItem.title}
            </EntityLink>
          }
          url={workItem.externalViewWorkItemUrl}
          tooltip="Open in external system"
        />
      </span>
      <span className={styles.listItemMeta}>
        <WorkStatusTag
          status={workItem.status}
          category={workItem.statusCategory.id}
        />
        <Text type="secondary">{workItem.type.name}</Text>
        {workItem.sprint ? (
          <Text type="secondary">{workItem.sprint.name}</Text>
        ) : workItem.team ? (
          <Text type="secondary">{workItem.team.name}</Text>
        ) : null}
      </span>
    </div>
  </WaydList.Item>
)

/**
 * The backlog-level work items assigned to the caller that are in progress or
 * proposed, Active first, with how many they finished in the last week.
 */
const MyWorkItemsWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, MY_WORK_ITEMS_LIMIT)
  const open = useGetMyWorkItemsQuery({ statusCategories: OPEN_CATEGORIES })
  // The done count is extra: without it the open work still shows. Like the
  // other widgets' counts, it shows only above zero.
  const { data: done } = useGetMyWorkItemsQuery({
    statusCategories: DONE_CATEGORIES,
    doneFrom: recentlyDoneSince(),
  })

  const doneLine =
    done && done.length > 0 ? (
      <Text type="secondary" className={styles.widgetEmpty}>
        {plural(done.length, 'work item', 'work items')} done in the last{' '}
        {RECENTLY_DONE_DAYS} days.
      </Text>
    ) : null

  const body = () => {
    if (open.isError)
      return <WidgetError message="Could not load your work items." />
    const workItems = groupMyWorkItems(open.data ?? [])
    if (workItems.length === 0) {
      return (
        <>
          <WidgetEmpty message="Nothing is assigned to you in progress or proposed. Work items assigned to you in a synced workspace appear here." />
          {doneLine}
        </>
      )
    }
    const more = workItems.length - rows
    return (
      <>
        <WaydList size="small">
          {workItems.slice(0, rows).map((workItem) => (
            <WorkItemRow key={workItem.id} workItem={workItem} />
          ))}
        </WaydList>
        {more > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            And {plural(more, 'more work item', 'more work items')}.
          </Text>
        ) : null}
        {doneLine}
      </>
    )
  }

  return (
    <WidgetCard
      title={title}
      loading={open.isLoading}
      failed={open.isError}
      // The done count is extra, so with no open work the widget is empty.
      empty={groupMyWorkItems(open.data ?? []).length === 0}
      emptySummary="Nothing in progress or proposed"
    >
      {body()}
    </WidgetCard>
  )
}

export default MyWorkItemsWidget
