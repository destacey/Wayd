'use client'

import { WaydEmpty } from '@/src/components/common'
import { CycleTimeMetric, MetricCard } from '@/src/components/common/metrics'
import { getCycleTimeWorkItems } from '@/src/components/common/work/cycle-time-report.filtering'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { EmployeeDetailsDto, WorkStatusCategory } from '@/src/services/wayd-api'
import {
  useGetDirectReportsQuery,
  useGetEmployeeWorkItemsQuery,
} from '@/src/store/features/organizations/employee-api'
import { useGetEmployeeTeamMembershipsQuery } from '@/src/store/features/organization/team-members-api'
import { Card, Col, Flex, Row, Skeleton, Tag, Typography } from 'antd'
import Link from 'next/link'
import { useMemo } from 'react'

const { Text, Title } = Typography

/** Matches the cycle time report's own default window. */
const CYCLE_TIME_DAYS = 90

export interface EmployeeOverviewProps {
  employee: EmployeeDetailsDto
  /**
   * Navigates to a section by id. Supplied by the page so section ids stay
   * defined in one place rather than being restated here.
   */
  onNavigateToSection: (sectionId: string) => void
}

const SectionCard = ({
  title,
  isLoading,
  isEmpty,
  emptyMessage,
  children,
}: {
  title: string
  isLoading: boolean
  /** Omit for blocks the caller only renders when they have content. */
  isEmpty?: boolean
  emptyMessage?: string
  children: React.ReactNode
}) => (
  <Flex vertical gap="small">
    <Title level={4} style={{ margin: 0 }}>
      {title}
    </Title>
    <Card size="small">
      {isLoading ? (
        <Skeleton active paragraph={{ rows: 2 }} title={false} />
      ) : isEmpty ? (
        <WaydEmpty message={emptyMessage} />
      ) : (
        children
      )}
    </Card>
  </Flex>
)

/**
 * What this person is working on, at a glance.
 *
 * Counts come from the same queries the sections use, so the numbers cannot
 * disagree with the lists they summarise. Cheaper aggregate endpoints would
 * be worth having if this page grows, but correctness first.
 */
const EmployeeOverview = ({
  employee,
  onNavigateToSection,
}: EmployeeOverviewProps) => {
  const { data: memberships, isLoading: teamsLoading } =
    useGetEmployeeTeamMembershipsQuery(
      { employeeId: employee.id },
      { skip: !employee.id },
    )

  const { data: directReports, isLoading: reportsLoading } =
    useGetDirectReportsQuery(employee.id, { skip: !employee.id })

  const { data: openWorkItems, isLoading: workLoading } =
    useGetEmployeeWorkItemsQuery(
      {
        employeeId: employee.id,
        statusCategories: [
          WorkStatusCategory.Proposed,
          WorkStatusCategory.Active,
        ],
      },
      { skip: !employee.id },
    )

  // Ninety days, matching the cycle time report's own default window, so the
  // headline figure here agrees with the report the rail links to.
  const doneFrom = useMemo(() => {
    const from = new Date()
    from.setUTCDate(from.getUTCDate() - CYCLE_TIME_DAYS)
    from.setUTCHours(0, 0, 0, 0)
    return from.toISOString()
  }, [])

  const { data: completedWorkItems, isLoading: cycleTimeLoading } =
    useGetEmployeeWorkItemsQuery(
      {
        employeeId: employee.id,
        statusCategories: [WorkStatusCategory.Done],
        doneFrom,
      },
      { skip: !employee.id },
    )

  const averageCycleTime = useMemo(() => {
    const withCycleTime = getCycleTimeWorkItems(completedWorkItems)
    if (withCycleTime.length === 0) return null

    const total = withCycleTime.reduce(
      (sum, item) => sum + (item.cycleTime ?? 0),
      0,
    )
    return total / withCycleTime.length
  }, [completedWorkItems])

  const openWorkItemCount = openWorkItems?.length ?? 0

  const sortedTeams = useMemo(
    () =>
      [...(memberships ?? [])].sort((a, b) =>
        caseInsensitiveCompare(a.team.name, b.team.name),
      ),
    [memberships],
  )

  const sortedReports = useMemo(
    () =>
      [...(directReports ?? [])].sort((a, b) =>
        caseInsensitiveCompare(a.displayName, b.displayName),
      ),
    [directReports],
  )

  return (
    <Flex vertical gap="large">
      {/* Tiles that would only ever read zero are omitted rather than shown
          empty — an overview should carry facts, not absences.

          MetricCard sets height: 100%, so tiles in a row match heights even
          when only one carries a secondary value. */}
      <Row gutter={[16, 16]} align="stretch">
        <Col xs={12} md={8}>
          <MetricCard
            title="Teams"
            value={teamsLoading ? '—' : sortedTeams.length}
            onClick={() => onNavigateToSection('teams')}
          />
        </Col>
        {(workLoading || openWorkItemCount > 0) && (
          <Col xs={12} md={8}>
            <MetricCard
              title="Assigned Work Items"
              value={workLoading ? '—' : openWorkItemCount}
              onClick={() => onNavigateToSection('work-items')}
            />
          </Col>
        )}
        {(cycleTimeLoading || averageCycleTime !== null) && (
          <Col xs={12} md={8}>
            <CycleTimeMetric
              value={averageCycleTime ?? 0}
              secondaryValue={
                <Text type="secondary">Last {CYCLE_TIME_DAYS} days</Text>
              }
              onClick={() => onNavigateToSection('cycle-time-report')}
            />
          </Col>
        )}
        {(reportsLoading || sortedReports.length > 0) && (
          <Col xs={12} md={8}>
            <MetricCard
              title="Direct Reports"
              value={reportsLoading ? '—' : sortedReports.length}
            />
          </Col>
        )}
      </Row>

      <SectionCard
        title="Teams"
        isLoading={teamsLoading}
        isEmpty={sortedTeams.length === 0}
        emptyMessage="Not a member of any team."
      >
        <Flex vertical gap={10}>
          {sortedTeams.map((m) => (
            <Flex
              key={m.team.id}
              align="center"
              gap="small"
              wrap
              justify="space-between"
            >
              <Link href={`/organizations/teams/${m.team.key}`}>
                {m.team.name}
              </Link>
              <Flex gap={4} wrap>
                {m.roles.length > 0 ? (
                  [...m.roles]
                    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
                    .map((r) => <Tag key={r.id}>{r.name}</Tag>)
                ) : (
                  <Text type="secondary">Member</Text>
                )}
              </Flex>
            </Flex>
          ))}
        </Flex>
      </SectionCard>

      {/* Most people manage nobody, so an empty-state card here would be noise
          on the majority of records rather than useful information. */}
      {(reportsLoading || sortedReports.length > 0) && (
        <SectionCard title="Direct Reports" isLoading={reportsLoading}>
          <Flex vertical gap={10}>
            {sortedReports.map((r) => (
              <Flex
                key={r.id}
                align="center"
                gap="small"
                wrap
                justify="space-between"
              >
                <Link href={`/organizations/employees/${r.key}`}>
                  {r.displayName}
                </Link>
                {r.jobTitle && <Text type="secondary">{r.jobTitle}</Text>}
              </Flex>
            ))}
          </Flex>
        </SectionCard>
      )}
    </Flex>
  )
}

export default EmployeeOverview
