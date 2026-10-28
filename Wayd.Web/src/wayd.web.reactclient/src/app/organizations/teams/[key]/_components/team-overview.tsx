'use client'

import { WaydEmpty } from '@/src/components/common'
import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import ActiveTeamSprint from '@/src/components/common/planning/active-team-sprint'
// From its file, not the folder's index, which would pull the lazily loaded report in with it.
import TeamSprintMetricsSummary from '@/src/components/common/work/sprint-metrics/team-sprint-metrics-summary'
import TeamCadenceMetricsSummary from '@/src/components/common/work/cadence-metrics/team-cadence-metrics-summary'
import TeamMemberCard from '@/src/app/organizations/teams/_components/team-member-card'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { Methodology, TeamDetailsDto } from '@/src/services/wayd-api'
import {
  useGetTeamBacklogQuery,
  useGetTeamDependenciesQuery,
  useGetTeamRisksQuery,
} from '@/src/store/features/organizations/team-api'
import { useGetTeamMembersQuery } from '@/src/store/features/organization/team-members-api'
import { useGetTeamCapacityQuery } from '@/src/store/features/organization/team-capacity-api'
import { calendarDateInZone, todayCalendarDate } from '@/src/utils'
import { Card, Col, Flex, Row, Skeleton, Typography } from 'antd'
import { useMemo } from 'react'

const { Text } = Typography

export interface TeamOverviewProps {
  team: TeamDetailsDto
  /** Whether the team has ever run Scrum, so it may have sprints to measure. */
  hasEverBeenScrum?: boolean
  /** Navigates to a section by id, so section ids stay defined on the page. */
  onNavigateToSection: (sectionId: string) => void
  /** Whether capacity management is on, which the Capacity tile reports on. */
  capacityEnabled?: boolean
}

/**
 * What the team is working on, at a glance.
 *
 * Counts come from the same queries the sections use, so a tile cannot
 * disagree with the list it summarises.
 */
const TeamOverview = ({
  team,
  hasEverBeenScrum,
  onNavigateToSection,
  capacityEnabled = false,
}: TeamOverviewProps) => {
  const { data: members, isLoading: membersLoading } = useGetTeamMembersQuery(
    { teamId: team.id },
    { skip: !team.id },
  )

  const { data: backlog, isLoading: backlogLoading } = useGetTeamBacklogQuery(
    team.id,
    { skip: !team.id },
  )

  const { data: dependencies, isLoading: dependenciesLoading } =
    useGetTeamDependenciesQuery(team.id, { skip: !team.id })

  const { data: risks, isLoading: risksLoading } = useGetTeamRisksQuery(
    { id: team.id, includeClosed: false, enabled: true },
    { skip: !team.id },
  )

  // Today in the team's own time zone, as the Capacity tab reads it, so the
  // tile and the tab agree on which allocations are current.
  const timeZone = team.operatingModel?.timeZone
  const today = timeZone
    ? calendarDateInZone(new Date(), timeZone)
    : todayCalendarDate()

  // `isFetching` rather than `isLoading`, which is true only on the first
  // load: when the date rolls over, `currentData` empties while the new day
  // loads, and the tile would vanish instead of showing its skeleton.
  const { currentData: capacity, isFetching: capacityFetching } =
    useGetTeamCapacityQuery(
      { teamId: team.id, teamType: 'Team', from: today, to: today },
      { skip: !capacityEnabled || !team.id },
    )
  const capacityLoading = capacityFetching && !capacity

  const sortedMembers = useMemo(
    () =>
      [...(members ?? [])].sort((a, b) =>
        caseInsensitiveCompare(a.employee.name, b.employee.name),
      ),
    [members],
  )

  const backlogCount = backlog?.length ?? 0
  const dependencyCount = dependencies?.length ?? 0
  const riskCount = risks?.length ?? 0
  // A one-day range returns only the allocations in effect that day, so their
  // percentages add up to today's full-time equivalents. Rounded to the one
  // decimal shown, so a 1–4% allocation hides the tile rather than reading 0.0.
  const allocatedPercent = (capacity?.people ?? [])
    .flatMap((p) => p.allocations)
    .reduce((sum, a) => sum + a.percent, 0)
  const fte = Math.round(allocatedPercent / 10) / 10

  const isScrum = team.operatingModel?.methodology === Methodology.Scrum

  return (
    <Flex vertical gap="large">
      {/* Tiles that would only ever read zero are omitted — an overview should
          carry facts, not absences. */}
      {/* A wrapping row rather than a 24-column grid: up to five tiles show,
          which no column span divides evenly. */}
      <Flex wrap gap={16}>
        <MetricCard
          title="Members"
          value={sortedMembers.length}
          loading={membersLoading}
          onClick={() => onNavigateToSection('members')}
          cardStyle={METRIC_CARD_FLEX}
        />
        {capacityEnabled && (capacityLoading || fte > 0) && (
          <MetricCard
            title="Capacity"
            value={fte}
            precision={1}
            suffix="FTE"
            tooltip="The time allocated to the team today, in full-time equivalents: an allocation of 100% is one."
            loading={capacityLoading}
            onClick={() => onNavigateToSection('capacity')}
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
        {(backlogLoading || backlogCount > 0) && (
          <MetricCard
            title="Backlog"
            value={backlogCount}
            loading={backlogLoading}
            onClick={() => onNavigateToSection('backlog')}
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
        {(dependenciesLoading || dependencyCount > 0) && (
          <MetricCard
            title="Dependencies"
            value={dependencyCount}
            loading={dependenciesLoading}
            onClick={() => onNavigateToSection('dependency-management')}
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
        {(risksLoading || riskCount > 0) && (
          <MetricCard
            title="Open Risks"
            value={riskCount}
            loading={risksLoading}
            onClick={() => onNavigateToSection('risk-management')}
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
      </Flex>

      {/* Only Scrum teams run sprints, so a Kanban team gets no empty card. */}
      {isScrum && team.operatingModel && (
        <Flex vertical gap="small">
          <Text strong style={{ fontSize: 14 }}>
            Current Sprint
          </Text>
          <ActiveTeamSprint teamId={team.id} />
        </Flex>
      )}

      {/* A team that has moved off Scrum still has past sprints worth reading. */}
      {hasEverBeenScrum && (
        <TeamSprintMetricsSummary
          teamId={team.id}
          onOpenReport={() => onNavigateToSection('sprint-metrics')}
        />
      )}

      {team.operatingModel?.cadenceWeeks ? (
        <TeamCadenceMetricsSummary
          teamId={team.id}
          onOpenReport={() => onNavigateToSection('velocity')}
        />
      ) : null}

      <Flex vertical gap="small">
        <Text strong style={{ fontSize: 14 }}>
          Members
        </Text>
        {membersLoading ? (
          <Card size="small">
            <Skeleton active paragraph={{ rows: 2 }} title={false} />
          </Card>
        ) : sortedMembers.length === 0 ? (
          <Card size="small">
            <WaydEmpty message="No members assigned." />
          </Card>
        ) : (
          // Cards rather than rows: a person is scanned by face and name, and
          // the card carries the avatar, job title and roles together.
          <Row gutter={[12, 12]}>
            {sortedMembers.map((m) => (
              <Col key={m.employee.id} xs={24} sm={12} lg={8} xxl={6}>
                <TeamMemberCard member={m} />
              </Col>
            ))}
          </Row>
        )}
      </Flex>
    </Flex>
  )
}

export default TeamOverview
