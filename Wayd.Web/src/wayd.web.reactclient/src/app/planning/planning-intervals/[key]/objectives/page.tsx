'use client'

import PlanningIntervalObjectivesGrid from '@/src/app/planning/planning-intervals/_components/planning-interval-objectives-grid'
import { useDocumentTitle } from '@/src/hooks'
import { BuildOutlined, MenuOutlined } from '@ant-design/icons'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import { use, useState } from 'react'
import {
  CreatePlanningIntervalObjectiveForm,
  PlanningIntervalObjectiveDetailsDrawer,
} from '../../_components'
import { PageTitle } from '@/src/components/common'
import { notFound } from 'next/navigation'
import { Button, Spin } from 'antd'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import dynamic from 'next/dynamic'

const PlanningIntervalObjectivesTimeline = dynamic(
  () => import('../../_components/planning-interval-objectives-view-manager'),
  {
    ssr: false,
    loading: () => <Spin />,
  },
)
import {
  useGetPlanningIntervalCalendarQuery,
  useGetPlanningIntervalObjectivesQuery,
  useGetPlanningIntervalQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'

const viewSelectorOptions: SegmentedLabeledOption[] = [
  {
    value: 'List',
    icon: <MenuOutlined alt="List" title="List" />,
  },
  {
    value: 'Timeline',
    icon: <BuildOutlined alt="Timeline" title="Timeline" />,
  },
]

const PlanningIntervalObjectivesPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const piKey = Number(key)

  useDocumentTitle('PI Objectives')
  const [currentView, setCurrentView] = useState<string | number>('List')
  const [openCreateObjectiveForm, setOpenCreateObjectiveForm] =
    useState<boolean>(false)
  const [drawerObjectiveKey, setDrawerObjectiveKey] = useState<number | null>(null)

  const { data: planningIntervalData, isLoading } =
    useGetPlanningIntervalQuery(piKey)

  const {
    data: objectivesData,
    isLoading: isLoadingObjectives,
    refetch: refectObjectives,
  } = useGetPlanningIntervalObjectivesQuery({
    planningIntervalKey: piKey,
    teamId: undefined,
  })

  const { data: calendarData } = useGetPlanningIntervalCalendarQuery(piKey)

  const { data: teamData } = useGetPlanningIntervalTeamsQuery(piKey)

  const { hasPermissionClaim } = useAuth()
  const canManageObjectives = hasPermissionClaim(
    'Permissions.PlanningIntervalObjectives.Manage',
  )
  const canCreateObjectives =
    canManageObjectives &&
    planningIntervalData &&
    !planningIntervalData.objectivesLocked &&
    (teamData?.filter((t) => t.type == 'Team').length ?? 0) > 0
  const showActions = canCreateObjectives

  const viewSelector = (
    <Segmented
      options={viewSelectorOptions}
      value={currentView}
      onChange={setCurrentView}
    />
  )

  const createObjectiveButtonClicked = () => {
    setOpenCreateObjectiveForm(true)
  }

  const onCreateObjectiveFormClosed = (wasCreated: boolean) => {
    setOpenCreateObjectiveForm(false)
  }

  const actions = () => {
    return (
      <>
        {canCreateObjectives && (
          <Button onClick={createObjectiveButtonClicked}>
            Create Objective
          </Button>
        )}
      </>
    )
  }

  if (!isLoading && !planningIntervalData) {
    return notFound()
  }

  return (
    <>
      <PageTitle title="PI Objectives" actions={showActions && actions()} />
      {currentView === 'List' && (
        <PlanningIntervalObjectivesGrid
          objectivesData={objectivesData ?? []}
          isLoading={isLoadingObjectives}
          refreshObjectives={refectObjectives}
          planningIntervalKey={piKey}
          hidePlanningIntervalColumn={true}
          hideTeamColumn={false}
          viewSelector={viewSelector}
          persistStateKey="planning-interval-objectives"
        />
      )}
      {currentView === 'Timeline' && (
        <PlanningIntervalObjectivesTimeline
          objectivesData={objectivesData ?? []}
          planningIntervalCalendar={calendarData!}
          enableGroups={true}
          teamNames={teamData
            ?.filter((t) => t.type == 'Team')
            .map((t) => t.name)}
          viewSelector={viewSelector}
          onObjectiveClick={setDrawerObjectiveKey}
          onRefresh={refectObjectives}
        />
      )}
      {drawerObjectiveKey !== null && (
        <PlanningIntervalObjectiveDetailsDrawer
          planningIntervalKey={piKey}
          objectiveKey={drawerObjectiveKey}
          drawerOpen={drawerObjectiveKey !== null}
          onDrawerClose={() => setDrawerObjectiveKey(null)}
          canManageObjectives={!!canManageObjectives}
        />
      )}
      {openCreateObjectiveForm && (
        <CreatePlanningIntervalObjectiveForm
          planningIntervalKey={piKey}
          onFormCreate={() => onCreateObjectiveFormClosed(true)}
          onFormCancel={() => onCreateObjectiveFormClosed(false)}
        />
      )}
    </>
  )
}

const PlanningIntervalObjectivesPageWithAuthorization = authorizePage(
  PlanningIntervalObjectivesPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalObjectivesPageWithAuthorization
