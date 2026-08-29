'use client'

import ManagePlanningIntervalObjectiveWorkItemsForm from '@/src/app/planning/planning-intervals/_components/manage-planning-interval-objective-work-items-form'
import { WorkItemsDashboardModal, WorkItemsGrid } from '@/src/components/common/work'
import { IterationState } from '@/src/components/types'
import {
  useGetObjectiveWorkItemsQuery,
  useGetPlanningIntervalQuery,
} from '@/src/store/features/planning/planning-interval-api'
import { DashboardOutlined, FormOutlined } from '@ant-design/icons'
import { Button, Flex } from 'antd'
import { useState } from 'react'

export interface PlanningIntervalObjectiveWorkItemsSectionProps {
  planningIntervalKey: number
  objectiveKey: number
  canLinkWorkItems: boolean
}

/**
 * Every work item linked to the objective, as a full grid.
 *
 * The overview's card is the summary read; this is the one that filters, sorts
 * and exports, so it uses the shared work-items grid rather than a list.
 */
const PlanningIntervalObjectiveWorkItemsSection = ({
  planningIntervalKey,
  objectiveKey,
  canLinkWorkItems,
}: PlanningIntervalObjectiveWorkItemsSectionProps) => {
  const [openDashboard, setOpenDashboard] = useState<boolean>(false)
  const [openManageForm, setOpenManageForm] = useState<boolean>(false)

  const {
    data: workItemsData,
    isLoading,
    refetch,
  } = useGetObjectiveWorkItemsQuery({
    planningIntervalKey: planningIntervalKey.toString(),
    objectiveKey: objectiveKey.toString(),
  })

  const { data: planningInterval } =
    useGetPlanningIntervalQuery(planningIntervalKey)

  const workItems = workItemsData?.workItems ?? []

  // Nothing has been worked yet in a PI that has not started, so the dashboard
  // would chart an empty history.
  const state = planningInterval?.state.id as IterationState | undefined
  const enableDashboard =
    state !== undefined && state !== IterationState.Future && workItems.length > 0

  const onManageFormClosed = (wasSaved: boolean) => {
    setOpenManageForm(false)
    if (wasSaved) refetch()
  }

  const actions = (
    <Flex gap="small">
      {enableDashboard && (
        <Button
          type="text"
          icon={<DashboardOutlined />}
          title="Work items dashboard"
          onClick={() => setOpenDashboard(true)}
        />
      )}
      {canLinkWorkItems && (
        <Button
          type="text"
          icon={<FormOutlined />}
          title="Manage work items"
          onClick={() => setOpenManageForm(true)}
        />
      )}
    </Flex>
  )

  return (
    <>
      <WorkItemsGrid
        workItems={workItems}
        isLoading={isLoading}
        refetch={refetch}
        viewSelector={actions}
        persistStateKey="pi-objective-work-items"
      />
      {openDashboard && (
        <WorkItemsDashboardModal
          showDashboard={openDashboard}
          planningIntervalKey={planningIntervalKey}
          objectiveKey={objectiveKey}
          onModalClose={() => setOpenDashboard(false)}
        />
      )}
      {openManageForm && (
        <ManagePlanningIntervalObjectiveWorkItemsForm
          planningIntervalKey={planningIntervalKey}
          objectiveKey={objectiveKey}
          onFormComplete={() => onManageFormClosed(true)}
          onFormCancel={() => onManageFormClosed(false)}
        />
      )}
    </>
  )
}

export default PlanningIntervalObjectiveWorkItemsSection
