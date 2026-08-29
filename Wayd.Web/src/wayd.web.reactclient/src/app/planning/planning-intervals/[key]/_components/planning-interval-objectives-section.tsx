'use client'

import PlanningIntervalObjectivesGrid from '@/src/app/planning/planning-intervals/_components/planning-interval-objectives-grid'
import {
  CreatePlanningIntervalObjectiveForm,
  PlanningIntervalObjectiveDetailsDrawer,
} from '@/src/app/planning/planning-intervals/_components'
import {
  useGetPlanningIntervalCalendarQuery,
  useGetPlanningIntervalObjectivesQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import useAuth from '@/src/components/contexts/auth'
import { BuildOutlined, MenuOutlined } from '@ant-design/icons'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import dynamic from 'next/dynamic'
import { Spin } from 'antd'
import { useState } from 'react'

const PlanningIntervalObjectivesTimeline = dynamic(
  () =>
    import(
      '@/src/app/planning/planning-intervals/_components/planning-interval-objectives-view-manager'
    ),
  { ssr: false, loading: () => <Spin /> },
)

const viewSelectorOptions: SegmentedLabeledOption[] = [
  { value: 'List', icon: <MenuOutlined />, title: 'List' },
  { value: 'Timeline', icon: <BuildOutlined />, title: 'Timeline' },
]

export interface PlanningIntervalObjectivesSectionProps {
  planningIntervalKey: number
  /** Opens the create form. Owned by the page, which renders the action. */
  createFormOpen: boolean
  onCreateFormClose: () => void
}

/**
 * The PI's objectives, as a list or a timeline.
 *
 * The view selector rides in the grid's own toolbar rather than the section
 * heading, so it sits with the data it reorders.
 */
const PlanningIntervalObjectivesSection = ({
  planningIntervalKey,
  createFormOpen,
  onCreateFormClose,
}: PlanningIntervalObjectivesSectionProps) => {
  const [currentView, setCurrentView] = useState<string | number>('List')
  const [drawerObjectiveKey, setDrawerObjectiveKey] = useState<number | null>(
    null,
  )

  const {
    data: objectives,
    isLoading,
    refetch,
  } = useGetPlanningIntervalObjectivesQuery({
    planningIntervalKey,
    teamId: undefined,
  })

  const { data: calendar } = useGetPlanningIntervalCalendarQuery(
    planningIntervalKey,
  )
  const { data: teams } = useGetPlanningIntervalTeamsQuery(planningIntervalKey)

  const { hasPermissionClaim } = useAuth()
  const canManageObjectives = hasPermissionClaim(
    'Permissions.PlanningIntervalObjectives.Manage',
  )

  const viewSelector = (
    <Segmented
      options={viewSelectorOptions}
      value={currentView}
      onChange={setCurrentView}
    />
  )

  return (
    <>
      {currentView === 'List' && (
        <PlanningIntervalObjectivesGrid
          objectivesData={objectives ?? []}
          isLoading={isLoading}
          refreshObjectives={refetch}
          planningIntervalKey={planningIntervalKey}
          hidePlanningIntervalColumn
          hideTeamColumn={false}
          viewSelector={viewSelector}
          persistStateKey="planning-interval-objectives"
        />
      )}
      {currentView === 'Timeline' && (
        <PlanningIntervalObjectivesTimeline
          objectivesData={objectives ?? []}
          planningIntervalCalendar={calendar!}
          enableGroups
          teamNames={teams?.filter((t) => t.type === 'Team').map((t) => t.name)}
          viewSelector={viewSelector}
          onObjectiveClick={setDrawerObjectiveKey}
          onRefresh={refetch}
        />
      )}
      {drawerObjectiveKey !== null && (
        <PlanningIntervalObjectiveDetailsDrawer
          planningIntervalKey={planningIntervalKey}
          objectiveKey={drawerObjectiveKey}
          drawerOpen
          onDrawerClose={() => setDrawerObjectiveKey(null)}
          canManageObjectives={!!canManageObjectives}
        />
      )}
      {createFormOpen && (
        <CreatePlanningIntervalObjectiveForm
          planningIntervalKey={planningIntervalKey}
          onFormCreate={onCreateFormClose}
          onFormCancel={onCreateFormClose}
        />
      )}
    </>
  )
}

export default PlanningIntervalObjectivesSection
