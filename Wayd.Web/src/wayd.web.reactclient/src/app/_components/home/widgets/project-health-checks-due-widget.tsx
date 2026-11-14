'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import HealthCheckTag from '@/src/components/common/health-check/health-check-tag'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  ProjectListDto,
  ProjectMemberRole,
  ProjectStatus,
} from '@/src/services/wayd-api'
import { useGetProjectsQuery } from '@/src/store/features/ppm/projects-api'
import { calendarDaysBetween, toCalendarDate } from '@/src/utils'
import { Typography } from 'antd'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import { useHomeContext } from '../use-home-context'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import {
  daysOf,
  daysSetting,
  rowsToShow,
  WidgetSetting,
} from '../widget-settings'
import {
  HomeTileProps,
  HomeWidgetProps,
  widgetAnchorId,
} from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** Active projects the caller owns or manages: the people who report their health. */
const LED_ACTIVE_PROJECTS = {
  status: [ProjectStatus.Active],
  role: [ProjectMemberRole.Owner, ProjectMemberRole.Manager],
}

/** The setting for how soon a current health check's expiry puts its project on the list. */
export const EXPIRING_WINDOW_SETTING = 'expiring-window'

/** How soon a current health check's expiry puts its project on the list until the user picks another window. */
export const EXPIRING_SOON_DAYS = 7

/** What the user can change about the widget, beyond its rows and empty display. */
export const healthChecksDueSettings: WidgetSetting[] = [
  daysSetting(
    EXPIRING_WINDOW_SETTING,
    'List checks expiring within',
    [7, 14],
    EXPIRING_SOON_DAYS,
  ),
]

/** The most projects the widget lists; the rest are counted below them. */
export const HEALTH_CHECKS_DUE_LIMIT = 5

/** A project on the list: one with no current health check, or one whose check expires soon. */
export interface HealthCheckDue {
  project: ProjectListDto
  /** Whole days until the current check expires; undefined when there is none. */
  expiresInDays?: number
}

/**
 * Whether a project has no current health check at `now`. A check the list
 * returned can lapse while the page stays open, so its expiry is checked too.
 */
export const lacksCurrentHealthCheck = (
  project: ProjectListDto,
  now: Date,
): boolean =>
  !project.healthCheck || new Date(project.healthCheck.expiration) <= now

/**
 * The projects whose health check is due: those with none current first, by
 * name, then those expiring within `expiringDays` days, soonest first. A
 * check that is current and not expiring soon leaves its project off.
 */
export const healthChecksDue = (
  projects: ProjectListDto[],
  now: Date = new Date(),
  expiringDays: number = EXPIRING_SOON_DAYS,
): HealthCheckDue[] => {
  const today = toCalendarDate(now)
  const missing = projects
    .filter((p) => lacksCurrentHealthCheck(p, now))
    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
    .map((project) => ({ project }))
  const expiring = projects
    .filter((p) => !lacksCurrentHealthCheck(p, now))
    .map((project) => ({
      project,
      expiresInDays: calendarDaysBetween(
        today,
        project.healthCheck!.expiration,
      ),
    }))
    .filter((d) => d.expiresInDays <= expiringDays)
    .sort(
      (a, b) =>
        a.expiresInDays - b.expiresInDays ||
        caseInsensitiveCompare(a.project.name, b.project.name),
    )
  return [...missing, ...expiring]
}

const expiresLabel = (days: number) =>
  days <= 0
    ? 'Expires today'
    : `Expires in ${days} ${days === 1 ? 'day' : 'days'}`

/**
 * The query the widget and its tile read; RTK Query shares the request.
 * Skipped when the home context says the caller owns or manages no approved
 * or active project, which covers every active one.
 */
const useLedProjects = () => {
  const { context } = useHomeContext()
  const leadsNone =
    !!context &&
    context.projects.ownerCount + context.projects.managerCount === 0
  const result = useGetProjectsQuery(LED_ACTIVE_PROJECTS, { skip: leadsNone })
  return { ...result, leadsNone }
}

/** Active projects the caller owns or manages whose health check is missing, expired or expiring soon. */
const ProjectHealthChecksDueWidget: FC<HomeWidgetProps> = ({
  title,
  settings,
}) => {
  const rows = rowsToShow(settings, HEALTH_CHECKS_DUE_LIMIT)
  const expiringDays = daysOf(
    settings,
    EXPIRING_WINDOW_SETTING,
    EXPIRING_SOON_DAYS,
  )
  const { data: projects, isLoading, isError } = useLedProjects()
  const now = new Date()

  const body = () => {
    if (isError)
      return <WidgetError message="Could not load your projects' health." />
    if (!projects || projects.length === 0) {
      return (
        <WidgetEmpty message="None of the projects you own or manage is active yet. Their health checks appear here once one is, when a check is missing or about to expire." />
      )
    }
    const due = healthChecksDue(projects, now, expiringDays)
    if (due.length === 0) {
      return (
        <WidgetEmpty
          message={`Every active project you own or manage has a current health check that lasts more than ${expiringDays} days.`}
        />
      )
    }
    const more = due.length - rows
    return (
      <>
        <WaydList size="small">
          {due.slice(0, rows).map(({ project, expiresInDays }) => (
            <WaydList.Item key={project.id} className={styles.listItem}>
              <div className={styles.listItemBody}>
                <EntityLink href={`/ppm/projects/${project.key}`}>
                  {project.key} · {project.name}
                </EntityLink>
                <span className={styles.listItemMeta}>
                  {expiresInDays === undefined ? (
                    <Text type="danger">
                      {project.healthCheck
                        ? 'Health check expired'
                        : 'No current health check'}
                    </Text>
                  ) : (
                    <>
                      <HealthCheckTag healthCheck={project.healthCheck} />
                      <Text type="warning">{expiresLabel(expiresInDays)}</Text>
                    </>
                  )}
                </span>
              </div>
            </WaydList.Item>
          ))}
        </WaydList>
        {more > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            And {more} more.
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
      empty={healthChecksDue(projects ?? [], now, expiringDays).length === 0}
      emptySummary="None due"
    >
      {body()}
    </WidgetCard>
  )
}

/** Active projects the caller owns or manages with no current health check. */
export const ProjectHealthChecksDueTile: FC<HomeTileProps> = ({ title }) => {
  const { data, isLoading, isError, leadsNone } = useLedProjects()
  // A failed refetch keeps the last good data; showing it would pass off an old count as current.
  const value = isError
    ? undefined
    : leadsNone
      ? 0
      : data?.filter((p) => lacksCurrentHealthCheck(p, new Date())).length
  return (
    <AttentionTile
      title={title}
      context="on projects you lead"
      value={value}
      tooltip="Active projects you own or manage with no current health check: none was reported, or the last one expired."
      href={`#${widgetAnchorId('project-health-checks-due')}`}
      alert="warning"
      loading={isLoading}
    />
  )
}

export default ProjectHealthChecksDueWidget
