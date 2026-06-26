'use client'

import {
  PlanningIntervalDetailsDto,
  PlanningIntervalTeamResponse,
} from '@/src/services/wayd-api'
import { Col, Flex, Row, Segmented, Space, Spin, Tag, Typography } from 'antd'
import { useState } from 'react'
import TeamObjectivesListCard from './team-objectives-list-card'
import TeamRisksListCard from './team-risks-list-card'
import Link from 'next/link'
import { BarsOutlined, BuildOutlined } from '@ant-design/icons'
import { SegmentedLabeledOption } from 'antd/es/segmented'
import { PlanningIntervalObjectiveDetailsDrawer } from '../../_components'
import {
  useGetPlanningIntervalCalendarQuery,
  useGetPlanningIntervalObjectivesQuery,
  useGetPlanningIntervalTeamPredictabilityQuery,
  useGetPlanningIntervalRisksQuery,
} from '@/src/store/features/planning/planning-interval-api'
import useAuth from '@/src/components/contexts/auth'
import dynamic from 'next/dynamic'

const PlanningIntervalObjectivesTimeline = dynamic(
  () => import('../../_components/planning-interval-objectives-view-manager'),
  {
    ssr: false,
    loading: () => <Spin />,
  },
)

const { Title } = Typography

export interface TeamPlanReviewProps {
  planningInterval: PlanningIntervalDetailsDto
  team: PlanningIntervalTeamResponse
  refreshPlanningInterval: () => void
}

const viewSelectorOptions: SegmentedLabeledOption[] = [
  {
    value: 'List',
    icon: <BarsOutlined alt="List" title="List" />,
  },
  {
    value: 'Timeline',
    icon: <BuildOutlined alt="Timeline" title="Timeline" />,
  },
]

const TeamPlanReview = ({
  planningInterval,
  team,
  refreshPlanningInterval,
}: TeamPlanReviewProps) => {
  const [currentView, setCurrentView] = useState<string | number>('List')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [selectedObjectiveKey, setSelectedObjectiveKey] = useState<
    number | null
  >(null)

  const { data: calendarData } = useGetPlanningIntervalCalendarQuery(
    planningInterval?.key,
    {
      skip: !planningInterval?.key,
    },
  )

  const { data: objectivesData, refetch: refetchObjectives } =
    useGetPlanningIntervalObjectivesQuery(
      {
        planningIntervalKey: planningInterval?.key,
        teamId: team?.id,
      },
      { skip: !planningInterval?.key || !team?.id },
    )

  const {
    data: risksData,
    isLoading: risksIsLoading,
    error: risksError,
    refetch: refetchRisks,
  } = useGetPlanningIntervalRisksQuery(
    {
      planningIntervalKey: planningInterval?.key,
      teamId: team?.id,
    },
    { skip: !planningInterval?.key || !team?.id },
  )

  const { data: teamPredictabilityData } =
    useGetPlanningIntervalTeamPredictabilityQuery(
      { planningIntervalKey: planningInterval?.key, teamId: team?.id },
      { skip: !planningInterval?.key || !team?.id },
    )

  const { hasPermissionClaim } = useAuth()
  const canManageObjectives = hasPermissionClaim(
    'Permissions.PlanningIntervalObjectives.Manage',
  )
  const canCreatePIObjectiveHealthChecks = !!canManageObjectives
  const canCreateRisks = hasPermissionClaim('Permissions.Risks.Create')
  const canUpdateRisks = hasPermissionClaim('Permissions.Risks.Update')

  const viewSelector = (
    <Segmented
      options={viewSelectorOptions}
      value={currentView}
      onChange={setCurrentView}
    />
  )

  const showDrawer = () => {
    setDrawerOpen(true)
  }

  const onDrawerClose = () => {
    setDrawerOpen(false)
    setSelectedObjectiveKey(null)

    // TODO: why isn't this refetching automatically on the invalidation?
    refetchObjectives()
  }

  const onObjectiveClick = (objectiveKey: number) => {
    setSelectedObjectiveKey(objectiveKey)
    showDrawer()
  }

  return (
    <>
      <Flex
        justify="space-between"
        align="center"
        style={{ paddingBottom: '16px' }}
      >
        <Space>
          <Title level={3} style={{ margin: '0' }}>
            <Link href={`/organizations/teams/${team?.key}`}>{team?.name}</Link>
          </Title>
          {(objectivesData?.length ?? 0) > 0 && teamPredictabilityData != null && (
            <Tag title="PI Predictability">{`${teamPredictabilityData}%`}</Tag>
          )}
        </Space>
        {viewSelector}
      </Flex>
      {currentView === 'List' ? (
        <Row gutter={[16, 16]}>
          <Col xs={24} sm={24} md={24} lg={12}>
            <TeamObjectivesListCard
              objectivesData={objectivesData ?? []}
              refreshObjectives={refetchObjectives}
              teamId={team?.id}
              planningIntervalId={planningInterval?.id}
              planningIntervalKey={planningInterval?.key}
              newObjectivesAllowed={
                planningInterval && !planningInterval.objectivesLocked
              }
              refreshPlanningInterval={refreshPlanningInterval}
              onObjectiveClick={onObjectiveClick}
              canManageObjectives={canManageObjectives}
              canCreateHealthChecks={canCreatePIObjectiveHealthChecks}
            />
          </Col>
          <Col xs={24} sm={24} md={24} lg={12}>
            <TeamRisksListCard
              risks={risksData ?? []}
              teamId={team?.id}
              canCreateRisks={canCreateRisks}
              canUpdateRisks={canUpdateRisks}
              refreshRisks={refetchRisks}
            />
          </Col>
        </Row>
      ) : (
        <PlanningIntervalObjectivesTimeline
          objectivesData={objectivesData ?? []}
          planningIntervalCalendar={calendarData!}
          onObjectiveClick={onObjectiveClick}
          onRefresh={refetchObjectives}
        />
      )}
      {planningInterval?.key && selectedObjectiveKey && (
        <PlanningIntervalObjectiveDetailsDrawer
          planningIntervalKey={planningInterval?.key}
          objectiveKey={selectedObjectiveKey}
          drawerOpen={drawerOpen}
          onDrawerClose={onDrawerClose}
          canManageObjectives={canManageObjectives}
        />
      )}
    </>
  )
}

export default TeamPlanReview
