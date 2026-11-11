'use client'

import {
  AttentionStatus,
  Methodology,
  MyTeamOfTeamsRowResponse,
  MyTeamOfTeamsSummaryResponse,
} from '@/src/services/wayd-api'
import useAuth from '@/src/components/contexts/auth'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { teamUrl } from '@/src/utils'
import { Badge, Button, Flex, Tag, Typography } from 'antd'
import Link from 'next/link'
import { FC, useState } from 'react'
import styles from '../home.module.css'
import { daysSince, plural } from './team-figures'

const { Text } = Typography

const percent = (share: number) => `${Math.round(share * 100)}%`

const statusBadge: Record<AttentionStatus, 'error' | 'warning' | 'success'> = {
  [AttentionStatus.NeedsAttention]: 'error',
  [AttentionStatus.Watch]: 'warning',
  [AttentionStatus.OnTrack]: 'success',
}

const statusLabel: Record<AttentionStatus, string> = {
  [AttentionStatus.NeedsAttention]: 'Needs attention',
  [AttentionStatus.Watch]: 'Watch',
  [AttentionStatus.OnTrack]: 'On track',
}

/**
 * "6 teams · 4 Scrum, 2 Kanban", with any teams of teams in it counted apart
 * ("5 teams, 1 team of teams · …"), and leaving out a methodology no team
 * follows.
 */
export const rollupHeadline = (
  rollup: Pick<
    MyTeamOfTeamsSummaryResponse,
    'rows' | 'scrumTeamCount' | 'kanbanTeamCount'
  >,
): string => {
  const nestedCount = rollup.rows.filter((r) => r.nested).length
  const members = [
    plural(rollup.rows.length - nestedCount, 'team'),
    nestedCount > 0
      ? plural(nestedCount, 'team of teams', 'teams of teams')
      : null,
  ].filter(Boolean)
  const methodologies = [
    rollup.scrumTeamCount > 0 ? `${rollup.scrumTeamCount} Scrum` : null,
    rollup.kanbanTeamCount > 0 ? `${rollup.kanbanTeamCount} Kanban` : null,
  ].filter(Boolean)
  return [members.join(', '), methodologies.join(', ')]
    .filter(Boolean)
    .join(' · ')
}

/** What a row says about its team today, beside its name. */
const RowSignal: FC<{
  row: MyTeamOfTeamsRowResponse
  now: Date
  canViewSprints: boolean
}> = ({ row, now, canViewSprints }) => {
  if (row.nested) {
    const { teamCount, needsAttentionCount, watchCount } = row.nested
    const flagged = [
      needsAttentionCount > 0
        ? `${needsAttentionCount} ${needsAttentionCount === 1 ? 'needs' : 'need'} attention`
        : null,
      watchCount > 0 ? `${watchCount} to watch` : null,
    ].filter(Boolean)
    return (
      <Text type="secondary">
        {plural(teamCount, 'team')}
        {flagged.length > 0 ? ` · ${flagged.join(', ')}` : ''}
      </Text>
    )
  }

  if (row.sprint) {
    const { sprint, endedNotCompleted, doneShare, expectedShare } = row.sprint
    return (
      <span>
        <Link href={`/work/sprints/${sprint.key}`}>{sprint.name}</Link>
        <Text type={endedNotCompleted ? 'danger' : 'secondary'}>
          {endedNotCompleted
            ? ' ended, not completed'
            : doneShare != null && expectedShare != null
              ? ` · ${percent(doneShare)} done, ${percent(expectedShare)} expected`
              : ''}
        </Text>
      </span>
    )
  }

  // Without View Iterations no sprint is sent either, so say nothing rather
  // than claim the team is between sprints.
  if (row.methodology === Methodology.Scrum && canViewSprints) {
    return <Text type="secondary">No active sprint</Text>
  }

  if (row.flow) {
    const { inProgressCount, oldestInProgressSince } = row.flow
    const oldestDays = oldestInProgressSince
      ? daysSince(oldestInProgressSince, now)
      : null
    return (
      <Text type="secondary">
        {inProgressCount} in progress
        {oldestDays != null ? ` · oldest ${plural(oldestDays, 'day')}` : ''}
      </Text>
    )
  }

  return null
}

const Row: FC<{
  row: MyTeamOfTeamsRowResponse
  now: Date
  canViewSprints: boolean
}> = ({ row, now, canViewSprints }) => (
  <li className={styles.rollupRow}>
    {/* A status badge renders no title, so the label goes on a wrapper. */}
    <WaydTooltip title={statusLabel[row.status]}>
      <span role="img" aria-label={statusLabel[row.status]}>
        <Badge status={statusBadge[row.status]} />
      </span>
    </WaydTooltip>
    <Link href={teamUrl(row.team)}>{row.team.name}</Link>
    {row.isMine ? <Tag variant="filled">Your team</Tag> : null}
    <RowSignal row={row} now={now} canViewSprints={canViewSprints} />
  </li>
)

const RollupFooter: FC<{ rollup: MyTeamOfTeamsSummaryResponse }> = ({
  rollup,
}) => {
  const { dependencies, risks } = rollup
  if (!dependencies && !risks) return null
  const href = teamUrl(rollup.team)
  const needsAttention = dependencies
    ? dependencies.atRiskCount + dependencies.unhealthyCount
    : 0
  return (
    <span className={styles.listItemMeta}>
      {dependencies ? (
        <Link href={`${href}?section=dependencies`}>
          {plural(
            dependencies.openCount,
            'cross-team dependency',
            'cross-team dependencies',
          )}
          {needsAttention > 0 ? (
            <Text type="warning"> · {needsAttention} at risk</Text>
          ) : null}
        </Link>
      ) : null}
      {risks ? (
        <Link href={`${href}?section=risk-management`}>
          {plural(risks.ownOpenCount, 'open risk')}
          {risks.teamsOpenCount > 0 ? (
            <Text type="secondary"> · {risks.teamsOpenCount} on its teams</Text>
          ) : null}
        </Link>
      ) : null}
    </span>
  )
}

export interface TeamOfTeamsRollupProps {
  rollup: MyTeamOfTeamsSummaryResponse
  /** The moment ages are counted to; now unless a test pins it. */
  now?: Date
}

/**
 * A team of teams the caller belongs to, as one card: the teams beneath it
 * needing attention, then those to watch, with those on track collapsed
 * behind a count.
 */
const TeamOfTeamsRollup: FC<TeamOfTeamsRollupProps> = ({
  rollup,
  now = new Date(),
}) => {
  const [showOnTrack, setShowOnTrack] = useState(false)
  const { hasPermissionClaim } = useAuth()
  const canViewSprints = hasPermissionClaim('Permissions.Iterations.View')
  // The server sends the rows worst first, so on-track rows are the tail.
  const flagged = rollup.rows.filter(
    (r) => r.status !== AttentionStatus.OnTrack,
  )
  const onTrack = rollup.rows.filter(
    (r) => r.status === AttentionStatus.OnTrack,
  )
  const shown = showOnTrack ? [...flagged, ...onTrack] : flagged
  const pi = rollup.planningInterval

  return (
    <div className={styles.teamSection}>
      <Flex justify="space-between" align="baseline" gap="small" wrap>
        <Link href={teamUrl(rollup.team)}>
          <Text strong>{rollup.team.name}</Text>
        </Link>
        <Text type="secondary">{rollupHeadline(rollup)}</Text>
      </Flex>
      {pi ? (
        <span className={styles.listItemMeta}>
          <Link
            href={`/planning/planning-intervals/${pi.planningInterval.key}`}
          >
            {pi.planningInterval.name}
          </Link>
          {pi.objectivesAtRiskCount != null ? (
            <Text type={pi.objectivesAtRiskCount > 0 ? 'warning' : 'secondary'}>
              {plural(pi.objectivesAtRiskCount, 'objective')} at risk
            </Text>
          ) : null}
        </span>
      ) : null}
      {rollup.rows.length === 0 ? (
        <Text type="secondary">No teams are in this team of teams today.</Text>
      ) : shown.length > 0 ? (
        <ul className={styles.rollupRows}>
          {shown.map((row) => (
            <Row
              key={row.team.id}
              row={row}
              now={now}
              canViewSprints={canViewSprints}
            />
          ))}
        </ul>
      ) : null}
      {onTrack.length > 0 ? (
        <Button
          type="link"
          size="small"
          className={styles.rollupToggle}
          aria-expanded={showOnTrack}
          onClick={() => setShowOnTrack((open) => !open)}
        >
          {showOnTrack ? 'Hide on track' : `+ ${onTrack.length} on track`}
        </Button>
      ) : null}
      <RollupFooter rollup={rollup} />
    </div>
  )
}

export default TeamOfTeamsRollup
