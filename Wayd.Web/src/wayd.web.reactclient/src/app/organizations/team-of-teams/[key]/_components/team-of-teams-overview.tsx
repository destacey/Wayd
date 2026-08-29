'use client'

import { WaydEmpty } from '@/src/components/common'
import { MetricCard } from '@/src/components/common/metrics'
import {
  WaydGrid,
  caseInsensitiveCompare,
} from '@/src/components/common/wayd-grid'
import TeamMemberCard from '@/src/app/organizations/teams/_components/team-member-card'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  Methodology,
  TeamNavigationDto,
  TeamOfTeamsDetailsDto,
} from '@/src/services/wayd-api'
import { teamUrl } from '@/src/utils'
import {
  useGetTeamOfTeamsMembershipsQuery,
  useGetTeamOfTeamsRisksQuery,
  useGetTeamOperatingModelsForTeamsQuery,
} from '@/src/store/features/organizations/team-api'
import { useGetTeamOfTeamsMembersQuery } from '@/src/store/features/organization/team-members-api'
import { Card, Col, Flex, Row, Skeleton, Typography } from 'antd'
import Link from 'next/link'
import { useMemo } from 'react'

const { Text } = Typography

export interface TeamOfTeamsOverviewProps {
  team: TeamOfTeamsDetailsDto
  /** Navigates to a section by id, so section ids stay defined on the page. */
  onNavigateToSection: (sectionId: string) => void
}

/**
 * What this team of teams looks like, at a glance.
 *
 * Counts come from the same queries the sections use, so a tile cannot
 * disagree with the list it summarises.
 */
const TeamOfTeamsOverview = ({
  team,
  onNavigateToSection,
}: TeamOfTeamsOverviewProps) => {
  const { data: members, isLoading: membersLoading } =
    useGetTeamOfTeamsMembersQuery({ teamId: team.id }, { skip: !team.id })

  const { data: memberships, isLoading: membershipsLoading } =
    useGetTeamOfTeamsMembershipsQuery({ teamId: team.id }, { skip: !team.id })

  // Memberships run both ways — rows where this record is the child carry its
  // own parent. Keep only the ones where it is the parent, and only those
  // currently in effect, so a closed membership does not read as a live team.
  const childTeams = useMemo(
    () =>
      (memberships ?? [])
        .filter((m) => m.parent.id === team.id && !m.end)
        .map((m) => m.child)
        .sort((a, b) => caseInsensitiveCompare(a.name, b.name)),
    [memberships, team.id],
  )

  // Methodology lives on the operating model, not the team, and this endpoint
  // takes the whole set at once — so the column costs one query rather than
  // one per child team.
  const childTeamIds = useMemo(() => childTeams.map((t) => t.id), [childTeams])

  const { data: operatingModels, isLoading: methodologiesLoading } =
    useGetTeamOperatingModelsForTeamsQuery(
      { teamIds: childTeamIds },
      { skip: childTeamIds.length === 0 },
    )

  // No isCurrent filter: the query already returns only the model effective
  // today. isCurrent means "has no end date", so a model with a future end is
  // the live one and still reports false — filtering on it drops those teams.
  const methodologyByTeamId = useMemo(() => {
    const map = new Map<string, Methodology>()
    operatingModels?.forEach((m) => map.set(m.teamId, m.methodology))
    return map
  }, [operatingModels])

  const teamColumns = useMemo<ColumnDef<TeamNavigationDto, any>[]>(
    () => [
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Team',
        size: 240,
        // teamUrl branches on type, so a nested team-of-teams links to its
        // own route rather than the teams one.
        cell: ({ row }) => (
          <Link href={teamUrl(row.original)}>{row.original.name}</Link>
        ),
      },
      { id: 'code', accessorKey: 'code', header: 'Code', size: 140 },
      { id: 'type', accessorKey: 'type', header: 'Type', size: 150 },
      {
        id: 'methodology',
        header: 'Methodology',
        size: 150,
        accessorFn: (t) => methodologyByTeamId.get(t.id) ?? '',
      },
    ],
    [methodologyByTeamId],
  )

  const { data: risks, isLoading: risksLoading } = useGetTeamOfTeamsRisksQuery(
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

  const riskCount = risks?.length ?? 0

  return (
    <Flex vertical gap="large">
      {/* Tiles that would only ever read zero are omitted — an overview should
          carry facts, not absences. */}
      <Row gutter={[16, 16]} align="stretch">
        <Col xs={12} md={8}>
          <MetricCard
            title="Teams"
            value={childTeams.length}
            loading={membershipsLoading}
            onClick={() => onNavigateToSection('team-memberships')}
          />
        </Col>
        <Col xs={12} md={8}>
          <MetricCard
            title="Members"
            value={sortedMembers.length}
            loading={membersLoading}
            onClick={() => onNavigateToSection('members')}
          />
        </Col>
        {(risksLoading || riskCount > 0) && (
          <Col xs={12} md={8}>
            <MetricCard
              title="Open Risks"
              value={riskCount}
              loading={risksLoading}
              onClick={() => onNavigateToSection('risk-management')}
            />
          </Col>
        )}
      </Row>

      <Flex vertical gap="small">
        <Text strong style={{ fontSize: 14 }}>
          Teams
        </Text>
        {membershipsLoading ? (
          <Card size="small">
            <Skeleton active paragraph={{ rows: 2 }} title={false} />
          </Card>
        ) : childTeams.length === 0 ? (
          <Card size="small">
            <WaydEmpty message="No teams assigned." />
          </Card>
        ) : (
          <WaydGrid
            variant="simple"
            columns={teamColumns}
            data={childTeams}
            isLoading={methodologiesLoading}
          />
        )}
      </Flex>

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

export default TeamOfTeamsOverview
