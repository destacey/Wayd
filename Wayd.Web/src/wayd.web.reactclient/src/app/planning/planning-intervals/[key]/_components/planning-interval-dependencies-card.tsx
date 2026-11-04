'use client'

import { MetricCard } from '@/src/components/common/metrics'
import { DependencyHealth } from '@/src/components/types'
import { useGetPlanningIntervalDependenciesQuery } from '@/src/store/features/planning/planning-interval-api'
import { Card, Col, Row } from 'antd'
import { matchesStateFilter } from './planning-interval-dependencies-section'

export interface PlanningIntervalDependenciesCardProps {
  piKey: number
  /** Opens the Dependencies section, which lists what these figures count. */
  onOpen: () => void
}

/** The PI's open cross-team dependencies at a glance, linking through to the Dependencies section. */
const PlanningIntervalDependenciesCard = ({
  piKey,
  onOpen,
}: PlanningIntervalDependenciesCardProps) => {
  const { data, isLoading } = useGetPlanningIntervalDependenciesQuery(piKey)

  const open = (data ?? []).filter((d) => matchesStateFilter(d, 'open'))
  const withHealth = (health: DependencyHealth) =>
    open.filter((d) => d.health.id === health).length

  return (
    <Card size="small" title="Cross-Team Dependencies">
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <MetricCard
            title="Open"
            value={open.length}
            loading={isLoading}
            tooltip="Dependencies between different teams, or with an item that has no team, where either item is planned in one of this PI's sprints and the predecessor is To Do or In Progress."
            onClick={onOpen}
            ariaLabel="Open cross-team dependencies: view the Dependencies section"
          />
        </Col>
        <Col xs={12} sm={8}>
          <MetricCard
            title="Unhealthy"
            value={withHealth(DependencyHealth.Unhealthy)}
            loading={isLoading}
            tooltip="Open dependencies whose successor has already started, or is planned in an earlier sprint than its predecessor, or is planned while its predecessor is not."
            onClick={onOpen}
            ariaLabel="Unhealthy cross-team dependencies: view the Dependencies section"
          />
        </Col>
        <Col xs={12} sm={8}>
          <MetricCard
            title="At Risk"
            value={withHealth(DependencyHealth.AtRisk)}
            loading={isLoading}
            tooltip="Open dependencies where neither item is planned in a current or future sprint."
            onClick={onOpen}
            ariaLabel="At-risk cross-team dependencies: view the Dependencies section"
          />
        </Col>
      </Row>
    </Card>
  )
}

export default PlanningIntervalDependenciesCard
