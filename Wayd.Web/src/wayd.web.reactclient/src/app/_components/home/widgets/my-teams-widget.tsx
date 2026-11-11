'use client'

import { MetricCard } from '@/src/components/common/metrics'
import ActiveTeamSprint from '@/src/components/common/planning/active-team-sprint'
import {
  Methodology,
  MyTeamFlowSummaryResponse,
  MyTeamSummaryResponse,
} from '@/src/services/wayd-api'
import { formatCalendarDate, teamUrl } from '@/src/utils'
import { Flex, Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeWidgetProps } from '../widget-registry'
import { useMyTeamsSummary } from './my-teams-tile'
import { daysSince, plural } from './team-figures'
import TeamOfTeamsRollup from './team-of-teams-rollup'
import styles from '../home.module.css'

const { Text } = Typography

export { daysSince } from './team-figures'

const TeamHeader: FC<{ summary: MyTeamSummaryResponse }> = ({ summary }) => (
  <Flex justify="space-between" align="baseline" gap="small" wrap>
    <Link href={teamUrl(summary.team)}>
      <Text strong>{summary.team.name}</Text>
    </Link>
    {summary.methodology ? (
      <Text type="secondary">{summary.methodology}</Text>
    ) : null}
  </Flex>
)

/** The team of teams a team belongs to today, under its card. */
const PartOf: FC<{ summary: MyTeamSummaryResponse }> = ({ summary }) =>
  summary.partOf ? (
    <span className={styles.listItemMeta}>
      <Text type="secondary">
        Part of{' '}
        <Link href={teamUrl(summary.partOf)}>{summary.partOf.name}</Link>
      </Text>
    </span>
  ) : null

const Flow: FC<{ flow: MyTeamFlowSummaryResponse }> = ({ flow }) => {
  const oldest = flow.oldestInProgress
  return (
    <Flex vertical gap="small">
      <div className={styles.teamFlowMetrics}>
        <MetricCard
          title="In Progress"
          value={flow.inProgressCount}
          tooltip="Backlog items in an In Progress status now."
        />
        <MetricCard
          title="Done"
          value={flow.completedCount}
          tooltip={`Backlog items finished since ${formatCalendarDate(flow.completedSince)}, today included.`}
        />
        <MetricCard
          title="Cycle Time"
          value={flow.medianCycleTimeDays ?? '—'}
          precision={flow.medianCycleTimeDays == null ? undefined : 1}
          suffix={flow.medianCycleTimeDays == null ? undefined : 'days'}
          tooltip="The median days from starting to finishing the items in Done."
        />
      </div>
      {flow.currentPeriod ? (
        <Text type="secondary">
          Reporting period{' '}
          {formatCalendarDate(flow.currentPeriod.start, 'MMM D')} –{' '}
          {formatCalendarDate(flow.currentPeriod.end, 'MMM D')}
        </Text>
      ) : null}
      {oldest ? (
        <span className={styles.listItemMeta}>
          <Text type="secondary">Oldest in progress</Text>
          <Link
            href={`/work/workspaces/${oldest.workspaceKey}/work-items/${oldest.key}`}
          >
            {oldest.key}
          </Link>
          <Text ellipsis className={styles.teamOldestTitle}>
            {oldest.title}
          </Text>
          <Text type="secondary">
            {plural(daysSince(oldest.activatedOn), 'day')}
          </Text>
        </span>
      ) : null}
    </Flex>
  )
}

const TeamFooter: FC<{ summary: MyTeamSummaryResponse }> = ({ summary }) => {
  const { dependencies, openRiskCount } = summary
  if (!dependencies && openRiskCount == null) return null
  const href = teamUrl(summary.team)
  const needsAttention = dependencies
    ? dependencies.atRiskCount + dependencies.unhealthyCount
    : 0
  return (
    <span className={styles.listItemMeta}>
      {dependencies ? (
        <Link href={`${href}?section=dependency-management`}>
          {plural(dependencies.openCount, 'dependency', 'dependencies')}
          {needsAttention > 0 ? (
            <Text type="warning"> · {needsAttention} at risk</Text>
          ) : null}
        </Link>
      ) : null}
      {openRiskCount != null ? (
        <Link href={`${href}?section=risk-management`}>
          {plural(openRiskCount, 'open risk')}
        </Link>
      ) : null}
    </span>
  )
}

/** One team, drawn for how it works today, with its dependencies and risks underneath. */
const TeamSection: FC<{ summary: MyTeamSummaryResponse }> = ({ summary }) => {
  const isScrum = summary.methodology === Methodology.Scrum
  const activeSprint = summary.sprint?.activeSprint

  return (
    <div className={styles.teamSection}>
      {isScrum && activeSprint ? (
        // The sprint card's own title links to the team. It reads the sprint
        // again, so the header stands in should the sprint end in between.
        <ActiveTeamSprint
          teamId={summary.team.id}
          showTeamLink
          embedded
          fallback={<TeamHeader summary={summary} />}
        />
      ) : (
        <>
          <TeamHeader summary={summary} />
          {isScrum && summary.sprint ? (
            <Text type="secondary">
              No active sprint. The team&apos;s sprint appears here while it
              runs.
            </Text>
          ) : null}
          {summary.flow ? <Flow flow={summary.flow} /> : null}
        </>
      )}
      <TeamFooter summary={summary} />
      <PartOf summary={summary} />
    </div>
  )
}

/**
 * A card for each team of teams and team the caller is a member of: teams of
 * teams first, as the rollup over teams the caller may also be on.
 */
const MyTeamsWidget: FC<HomeWidgetProps> = ({ title }) => {
  const { data, isLoading, isError, isOnNoTeam } = useMyTeamsSummary()

  const body = () => {
    if (isError) return <WidgetError message="Could not load your teams." />
    if (
      isOnNoTeam ||
      !data ||
      (data.teams.length === 0 && data.teamsOfTeams.length === 0)
    ) {
      return (
        <WidgetEmpty message="You are not a member of a team. Your teams appear here once you join one." />
      )
    }
    return (
      <Flex vertical gap="middle">
        {data.teamsOfTeams.map((rollup) => (
          <TeamOfTeamsRollup key={rollup.team.id} rollup={rollup} />
        ))}
        {data.teams.map((summary) => (
          <TeamSection key={summary.team.id} summary={summary} />
        ))}
      </Flex>
    )
  }

  return (
    <WidgetCard title={title} loading={isLoading}>
      {body()}
    </WidgetCard>
  )
}

export default MyTeamsWidget
