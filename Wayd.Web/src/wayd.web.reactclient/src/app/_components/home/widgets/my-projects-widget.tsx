'use client'

import { DEFAULT_STATUSES } from '@/src/app/ppm/dashboards/projects/_components/dashboard-model'
import { WaydTooltip } from '@/src/components/common'
import { MyProjectsSummaryDto } from '@/src/services/wayd-api'
import {
  useGetMyProjectsSummaryQuery,
  useGetMyProjectsTaskMetricsQuery,
} from '@/src/store/features/ppm/projects-api'
import { Badge, Tag, Typography } from 'antd'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** The projects the dashboard opens on, so a count here matches the list it links to. */
const STATUS_FILTER = { status: DEFAULT_STATUSES }

const DASHBOARD_HREF = '/ppm/dashboards/projects'

export interface RoleCount {
  label: string
  count: number
}

/** The roles the caller holds across their projects, in role order, leaving out any they hold on none. */
export const roleCounts = (summary: MyProjectsSummaryDto): RoleCount[] =>
  [
    { label: 'Sponsor', count: summary.sponsorCount },
    { label: 'Owner', count: summary.ownerCount },
    { label: 'PM', count: summary.managerCount },
    { label: 'Member', count: summary.memberCount },
    { label: 'Task Assignee', count: summary.assigneeCount },
  ].filter((r) => r.count > 0)

const plural = (n: number) => (n === 1 ? 'project' : 'projects')

/** How many approved and active projects the caller holds a role on, by role. */
const MyProjectsWidget: FC<HomeWidgetProps> = ({ title }) => {
  const {
    data: summary,
    isLoading,
    isError,
  } = useGetMyProjectsSummaryQuery(STATUS_FILTER)

  const body = () => {
    if (isError) return <WidgetError message="Could not load your projects." />
    if (!summary || summary.totalCount === 0) {
      return (
        <WidgetEmpty
          message="You have no approved or active projects. They appear here once you hold a role on one or are assigned a task."
          link={{ href: '/ppm/projects', label: 'Browse projects' }}
        />
      )
    }
    return (
      <div className={styles.projectSummary}>
        <span className={styles.projectCount}>
          <Text strong className={styles.projectCountNumber}>
            {summary.totalCount}
          </Text>
          <Text type="secondary">
            approved and active {plural(summary.totalCount)}
          </Text>
        </span>
        <span className={styles.projectRoles}>
          {roleCounts(summary).map(({ label, count }) => (
            <WaydTooltip
              key={label}
              title={`You are a ${label} on ${count} ${plural(count)}`}
            >
              <Tag>
                {label}{' '}
                <Badge
                  count={count}
                  color="var(--ant-color-primary)"
                  size="small"
                />
              </Tag>
            </WaydTooltip>
          ))}
        </span>
      </div>
    )
  }

  return (
    <WidgetCard title={title} viewAllHref={DASHBOARD_HREF} loading={isLoading}>
      {body()}
    </WidgetCard>
  )
}

/** Open tasks past their planned end, across the caller's projects. */
export const OverdueTasksTile: FC = () => {
  const { data, isLoading, isError } =
    useGetMyProjectsTaskMetricsQuery(STATUS_FILTER)
  return (
    <AttentionTile
      title="Overdue tasks"
      context="on your projects"
      // A failed refetch keeps the last good data; showing it would pass off an old count as current.
      value={isError ? undefined : data?.overdue}
      tooltip="Open tasks on your projects that are past their planned end date."
      href={DASHBOARD_HREF}
      alert="error"
      loading={isLoading}
    />
  )
}

/** Open tasks due by the end of this week, across the caller's projects. */
export const DueThisWeekTile: FC = () => {
  const { data, isLoading, isError } =
    useGetMyProjectsTaskMetricsQuery(STATUS_FILTER)
  return (
    <AttentionTile
      title="Due this week"
      context="on your projects"
      value={isError ? undefined : data?.dueThisWeek}
      tooltip="Open tasks on your projects due by the end of this week (Saturday)."
      href={DASHBOARD_HREF}
      alert="warning"
      loading={isLoading}
    />
  )
}

export default MyProjectsWidget
