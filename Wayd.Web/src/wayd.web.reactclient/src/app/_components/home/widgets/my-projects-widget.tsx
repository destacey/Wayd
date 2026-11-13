'use client'

import { DEFAULT_STATUSES } from '@/src/app/ppm/dashboards/projects/_components/dashboard-model'
import { WaydTooltip } from '@/src/components/common'
import { MyProjectsSummaryDto, ProjectStatus } from '@/src/services/wayd-api'
import { useGetMyProjectsSummaryQuery } from '@/src/store/features/ppm/projects-api'
import { Badge, Tag, Typography } from 'antd'
import { FC } from 'react'
import { useHomeContext } from '../use-home-context'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeWidgetProps } from '../widget-registry'
import {
  pickedOf,
  WidgetSetting,
  WidgetSettingValues,
} from '../widget-settings'
import styles from '../home.module.css'

const { Text } = Typography

/** The setting for which project statuses the widget counts. */
export const STATUSES_SETTING = 'statuses'

/** Every status, in lifecycle order, as the setting offers and the widget words them. */
const STATUS_LABELS: [ProjectStatus, string][] = [
  [ProjectStatus.Proposed, 'Proposed'],
  [ProjectStatus.Approved, 'Approved'],
  [ProjectStatus.Active, 'Active'],
  [ProjectStatus.Completed, 'Completed'],
  [ProjectStatus.Canceled, 'Canceled'],
]

/**
 * What the user can change about the widget, beyond its empty display. The
 * default is the statuses the dashboard opens on, so the count matches the
 * list it links to until the user counts others.
 */
export const myProjectsSettings: WidgetSetting[] = [
  {
    kind: 'multiSelect',
    key: STATUSES_SETTING,
    label: 'Statuses counted',
    options: STATUS_LABELS.map(([value, label]) => ({ value, label })),
    default: DEFAULT_STATUSES,
  },
]

/** The statuses the widget counts: the setting's pick, or the dashboard's default without settings. */
export const countedStatuses = (
  settings: WidgetSettingValues | undefined,
): ProjectStatus[] => {
  const picked = new Set(pickedOf(settings, STATUSES_SETTING, DEFAULT_STATUSES))
  return STATUS_LABELS.map(([status]) => status).filter((s) => picked.has(s))
}

/**
 * The statuses as the widget words them: "approved and active", or with
 * `or` for saying there are none of them.
 */
export const statusWording = (
  statuses: ProjectStatus[],
  conjunction: 'and' | 'or' = 'and',
): string => {
  const words = STATUS_LABELS.filter(([s]) => statuses.includes(s)).map(
    ([, label]) => label.toLowerCase(),
  )
  return words.length < 2
    ? words.join('')
    : `${words.slice(0, -1).join(', ')} ${conjunction} ${words[words.length - 1]}`
}

/** Whether the statuses are exactly the ones the home context counts. */
const isDefaultStatuses = (statuses: ProjectStatus[]) =>
  statuses.length === DEFAULT_STATUSES.length &&
  DEFAULT_STATUSES.every((s) => statuses.includes(s))

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

/**
 * Whether the home context says the caller has no approved or active project,
 * so the summary is known to be empty without asking. The context counts only
 * those statuses, so it says nothing about any other pick.
 */
const useHasNoProjects = (statuses: ProjectStatus[]) => {
  const { context } = useHomeContext()
  return isDefaultStatuses(statuses) && context?.projects.totalCount === 0
}

/** How many projects in the counted statuses the caller holds a role on, by role. */
const MyProjectsWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const statuses = countedStatuses(settings)
  const hasNoProjects = useHasNoProjects(statuses)
  const {
    data: summary,
    isLoading,
    isError,
  } = useGetMyProjectsSummaryQuery(
    { status: statuses },
    { skip: hasNoProjects },
  )

  const body = () => {
    if (isError) return <WidgetError message="Could not load your projects." />
    if (!summary || summary.totalCount === 0) {
      return (
        <WidgetEmpty
          message={`You have no ${statusWording(statuses, 'or')} projects. They appear here once you hold a role on one or are assigned a task.`}
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
            {statusWording(statuses)} {plural(summary.totalCount)}
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
    <WidgetCard
      title={title}
      viewAllHref={DASHBOARD_HREF}
      loading={isLoading}
      failed={isError}
      empty={!summary || summary.totalCount === 0}
      emptySummary={`No ${statusWording(statuses, 'or')} projects`}
    >
      {body()}
    </WidgetCard>
  )
}

export default MyProjectsWidget
