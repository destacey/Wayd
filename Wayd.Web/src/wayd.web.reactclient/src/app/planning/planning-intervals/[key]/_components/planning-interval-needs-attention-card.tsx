'use client'

import { useMemo, useState } from 'react'
import { Card, Col, Row, Segmented } from 'antd'
import { useGetPlanningIntervalObjectivesQuery } from '@/src/store/features/planning/planning-interval-api'
import WaydEmpty from '@/src/components/common/wayd-empty'
import { PlanningIntervalObjectiveCard } from '.'
import useAuth from '@/src/components/contexts/auth'
import { PlanningIntervalObjectiveDetailsDrawer } from '../../_components'

type SortMode = 'health' | 'progress' | 'team'

interface PlanningIntervalNeedsAttentionCardProps {
  piKey: number
}

const attentionHealthStatuses = new Set(['At Risk', 'Unhealthy'])
const healthPriority: Record<string, number> = {
  Unhealthy: 0,
  'At Risk': 1,
}

const PlanningIntervalNeedsAttentionCard = ({
  piKey,
}: PlanningIntervalNeedsAttentionCardProps) => {
  const [sortMode, setSortMode] = useState<SortMode>('health')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [selectedObjectiveKey, setSelectedObjectiveKey] = useState<
    number | null
  >(null)
  const { hasPermissionClaim } = useAuth()

  const {
    data: objectivesData,
    isLoading,
    refetch,
  } = useGetPlanningIntervalObjectivesQuery(
    {
      planningIntervalKey: piKey,
      teamId: undefined,
    },
    {
      skip: !piKey,
    },
  )

  const canManageObjectives = hasPermissionClaim(
    'Permissions.PlanningIntervalObjectives.Manage',
  )
  const canCreatePIObjectiveHealthChecks = !!canManageObjectives

  const attentionObjectives = useMemo(() => {
    const objectives = (objectivesData ?? []).filter(
      (objective) =>
        objective.status?.name !== 'Completed' &&
        attentionHealthStatuses.has(
          objective.healthCheck?.status?.name ?? 'Unknown',
        ),
    )

    return objectives.sort((a, b) => {
      if (sortMode === 'progress') {
        const progressDiff = (b.progress ?? 0) - (a.progress ?? 0)
        if (progressDiff !== 0) return progressDiff
      }

      if (sortMode === 'team') {
        const teamCompare = (a.team?.name ?? '').localeCompare(
          b.team?.name ?? '',
        )
        if (teamCompare !== 0) return teamCompare
      }

      const healthDiff =
        (healthPriority[a.healthCheck?.status?.name ?? 'Unknown'] ?? 99) -
        (healthPriority[b.healthCheck?.status?.name ?? 'Unknown'] ?? 99)
      if (healthDiff !== 0) return healthDiff

      return a.key - b.key
    })
  }, [objectivesData, sortMode])

  if (isLoading) {
    return <Card size="small" loading title="Needs Attention" />
  }

  const onObjectiveClick = (objectiveKey: number) => {
    setSelectedObjectiveKey(objectiveKey)
    setDrawerOpen(true)
  }

  const onDrawerClose = () => {
    setDrawerOpen(false)
    setSelectedObjectiveKey(null)
    refetch()
  }

  return (
    <>
      <Card
        size="small"
        title="Needs Attention - Objectives"
        extra={
          <Segmented
            size="small"
            value={sortMode}
            onChange={(value) => setSortMode(value as SortMode)}
            options={[
              { label: 'by health', value: 'health' },
              { label: 'by progress', value: 'progress' },
              { label: 'by team', value: 'team' },
            ]}
          />
        }
        styles={{ body: { padding: 8 } }}
      >
        {attentionObjectives.length === 0 ? (
          <WaydEmpty message="No objectives need attention" />
        ) : (
          <Row gutter={[12, 12]}>
            {attentionObjectives.map((objective) => (
              <Col xs={24} md={12} key={objective.id}>
                <PlanningIntervalObjectiveCard
                  objective={objective}
                  piKey={piKey}
                  canUpdateObjectives={canManageObjectives}
                  canCreateHealthChecks={canCreatePIObjectiveHealthChecks}
                  refreshObjectives={refetch}
                  onObjectiveClick={onObjectiveClick}
                />
              </Col>
            ))}
          </Row>
        )}
      </Card>
      {selectedObjectiveKey && (
        <PlanningIntervalObjectiveDetailsDrawer
          planningIntervalKey={piKey}
          objectiveKey={selectedObjectiveKey}
          drawerOpen={drawerOpen}
          onDrawerClose={onDrawerClose}
          canManageObjectives={canManageObjectives}
        />
      )}
    </>
  )
}

export default PlanningIntervalNeedsAttentionCard

