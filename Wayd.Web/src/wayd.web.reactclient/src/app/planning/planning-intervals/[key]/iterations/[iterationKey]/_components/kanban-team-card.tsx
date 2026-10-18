'use client'

import { CycleTimeMetric, MetricCard } from '@/src/components/common/metrics'
import { WaydTooltip } from '@/src/components/common'
import useTheme from '@/src/components/contexts/theme'
import {
  DateMeasuredTeamMetricsSummary,
  SizingMethod,
} from '@/src/services/wayd-api'
import { sizingMethodLabel, sizingMethodMeasure } from '@/src/utils'
import { Card, Col, Flex, Row, Tag } from 'antd'
import Link from 'next/link'
import { FC } from 'react'

export interface KanbanTeamCardProps {
  team: DateMeasuredTeamMetricsSummary
  /** Counts work items rather than summing the team's own sizing method. */
  byCount: boolean
}

const metricCardStyle: React.CSSProperties = { height: '100%' }

/**
 * A Kanban team's row in a PI iteration. A Kanban team has no sprint, so it is
 * measured by the iteration's dates: what it completed within them and what it
 * had in progress at their end. Nothing is scoped to the iteration, so there is
 * no commitment, total or not-started figure.
 */
const KanbanTeamCard: FC<KanbanTeamCardProps> = ({ team, byCount }) => {
  // The window is counted in the team's own time zone, so whether it has begun
  // or ended can differ from the iteration's state near its boundaries.
  const isFuture = !team.inProgressAsOf
  // Instants reach the store as ISO strings, though typed as Date.
  const isPast =
    !!team.inProgressAsOf &&
    new Date(team.inProgressAsOf).getTime() === new Date(team.until).getTime()
  const { token } = useTheme()
  const sizingMethod = byCount ? SizingMethod.Count : team.sizingMethod
  const measure = sizingMethodMeasure(sizingMethod)
  const completed = byCount ? team.completedWorkItems : team.completedEstimate
  const inProgress = byCount
    ? team.inProgressWorkItems
    : team.inProgressEstimate

  return (
    <Card size="small" hoverable styles={{ body: { padding: 16 } }}>
      <Flex vertical gap="middle">
        <Flex justify="space-between" align="flex-start" gap={8} wrap>
          <Link
            href={`/organizations/teams/${team.team.key}`}
            style={{ fontSize: 16, fontWeight: 600, width: 'fit-content' }}
          >
            {team.team.name}
          </Link>
          <Flex gap={8} wrap>
            {team.isHistoryIncomplete && isPast && (
              <WaydTooltip title="This team's work item history has not been read through yet, so what was in progress at the iteration's end may be short.">
                <Tag color="warning">History incomplete</Tag>
              </WaydTooltip>
            )}
            <WaydTooltip
              title={`A Kanban team has no sprint, so it is measured by the iteration's dates in its time zone (${team.timeZone}).`}
            >
              <Tag>Measured by dates</Tag>
            </WaydTooltip>
            <WaydTooltip
              title={`This team is measured in ${measure}: its sizing method on the iteration's first day, or its last if the team's operating model began during the iteration.`}
            >
              <Tag>{sizingMethodLabel(sizingMethod)}</Tag>
            </WaydTooltip>
          </Flex>
        </Flex>

        <Row gutter={[8, 8]}>
          <Col xs={12} sm={8} md={6}>
            <MetricCard
              title="Scope"
              value="—"
              tooltip="Not applicable: nothing is scoped to an iteration for a Kanban team, so it has no committed, added or not-started work."
              cardStyle={metricCardStyle}
            />
          </Col>
          {!isFuture && (
            <>
              <Col xs={12} sm={8} md={6}>
                <MetricCard
                  title="Velocity"
                  value={completed}
                  valueStyle={{ color: token.colorSuccess }}
                  tooltip={`The ${measure} that reached a Done status within the iteration's dates. Work moved to a Removed status is not counted.`}
                  cardStyle={metricCardStyle}
                />
              </Col>
              <Col xs={12} sm={8} md={6}>
                <MetricCard
                  title="In Progress"
                  value={inProgress}
                  tooltip={
                    isPast
                      ? `The ${measure} that were in progress at the end of the iteration.`
                      : `The ${measure} in progress now.`
                  }
                  cardStyle={metricCardStyle}
                />
              </Col>
              <Col xs={12} sm={8} md={6}>
                <CycleTimeMetric
                  value={team.cycleTime?.averageCycleTimeDays ?? 0}
                  cardStyle={metricCardStyle}
                />
              </Col>
            </>
          )}
        </Row>
      </Flex>
    </Card>
  )
}

export default KanbanTeamCard
