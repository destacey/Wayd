'use client'

import { WaydEmpty } from '@/src/components/common'
import { MetricCard } from '@/src/components/common/metrics'
import ActiveTeamSprint from '@/src/components/common/planning/active-team-sprint'
import TeamMemberCard from '@/src/app/(legacy)/organizations/teams/_components/team-member-card'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { Methodology, TeamDetailsDto } from '@/src/services/wayd-api'
import {
  useGetTeamBacklogQuery,
  useGetTeamDependenciesQuery,
  useGetTeamRisksQuery,
} from '@/src/store/features/organizations/team-api'
import { useGetTeamMembersQuery } from '@/src/store/features/organization/team-members-api'
import { Card, Col, Flex, Row, Skeleton, Typography } from 'antd'
import { useMemo } from 'react'

const { Text } = Typography

export interface TeamOverviewProps {
  team: TeamDetailsDto
  /** Navigates to a section by id, so section ids stay defined on the page. */
  onNavigateToSection: (sectionId: string) => void
}

/**
 * What the team is working on, at a glance.
 *
 * Counts come from the same queries the sections use, so a tile cannot
 * disagree with the list it summarises.
 */
const TeamOverview = ({ team, onNavigateToSection }: TeamOverviewProps) => {
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

  const isScrum = team.operatingModel?.methodology === Methodology.Scrum

  return (
    <Flex vertical gap="large">
      {/* Tiles that would only ever read zero are omitted — an overview should
          carry facts, not absences. */}
      <Row gutter={[16, 16]} align="stretch">
        <Col xs={12} md={6}>
          <MetricCard
            title="Members"
            value={membersLoading ? '—' : sortedMembers.length}
            onClick={() => onNavigateToSection('members')}
          />
        </Col>
        {(backlogLoading || backlogCount > 0) && (
          <Col xs={12} md={6}>
            <MetricCard
              title="Backlog"
              value={backlogLoading ? '—' : backlogCount}
              onClick={() => onNavigateToSection('backlog')}
            />
          </Col>
        )}
        {(dependenciesLoading || dependencyCount > 0) && (
          <Col xs={12} md={6}>
            <MetricCard
              title="Dependencies"
              value={dependenciesLoading ? '—' : dependencyCount}
              onClick={() => onNavigateToSection('dependency-management')}
            />
          </Col>
        )}
        {(risksLoading || riskCount > 0) && (
          <Col xs={12} md={6}>
            <MetricCard
              title="Open Risks"
              value={risksLoading ? '—' : riskCount}
              onClick={() => onNavigateToSection('risk-management')}
            />
          </Col>
        )}
      </Row>

      {/* Only Scrum teams run sprints, so a Kanban team gets no empty card. */}
      {isScrum && team.operatingModel && (
        <Flex vertical gap="small">
          <Text strong style={{ fontSize: 14 }}>
            Current Sprint
          </Text>
          <ActiveTeamSprint
            teamId={team.id}
            sizingMethod={team.operatingModel.sizingMethod}
          />
        </Flex>
      )}

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
