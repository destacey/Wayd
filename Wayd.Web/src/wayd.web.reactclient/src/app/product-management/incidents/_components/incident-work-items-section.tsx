'use client'

import ManageIncidentWorkItemsForm from '@/src/app/product-management/incidents/_components/manage-incident-work-items-form'
import ReplaceIncidentWorkItemForm from '@/src/app/product-management/incidents/_components/replace-incident-work-item-form'
import { WorkItemsGrid } from '@/src/components/common/work'
import { WorkItemListDto } from '@/src/services/wayd-api'
import { useGetIncidentWorkItemsQuery } from '@/src/store/features/product-management/incidents-api'
import { FormOutlined } from '@ant-design/icons'
import type { ItemType } from 'antd/es/menu/interface'
import { Button } from 'antd'
import { useState } from 'react'

export interface IncidentWorkItemsSectionProps {
  incidentId: string
  idOrKey: string
  canManage: boolean
}

/**
 * Every work item linked to the incident, in the shared work-items grid.
 *
 * Linking and replacing stay on the forms the incident already has. The grid
 * is the list; it does not chart completion.
 */
const IncidentWorkItemsSection = ({
  incidentId,
  idOrKey,
  canManage,
}: IncidentWorkItemsSectionProps) => {
  const [managing, setManaging] = useState(false)
  const [replacing, setReplacing] = useState<WorkItemListDto | null>(null)

  const { data, isLoading, refetch } = useGetIncidentWorkItemsQuery({
    idOrKey,
    incidentId,
  })

  const getRowActions = canManage
    ? (workItem: WorkItemListDto): ItemType[] => [
        {
          key: 'change',
          label: 'Change linked work item',
          onClick: () => setReplacing(workItem),
        },
      ]
    : undefined

  return (
    <>
      <WorkItemsGrid
        workItems={data?.workItems ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="incident-work-items"
        getRowActions={getRowActions}
        viewSelector={
          canManage ? (
            <Button
              type="text"
              icon={<FormOutlined />}
              title="Manage work items"
              onClick={() => setManaging(true)}
            />
          ) : undefined
        }
      />
      {managing ? (
        <ManageIncidentWorkItemsForm
          incidentId={incidentId}
          idOrKey={idOrKey}
          onFormComplete={() => setManaging(false)}
          onFormCancel={() => setManaging(false)}
        />
      ) : null}
      {replacing ? (
        <ReplaceIncidentWorkItemForm
          incidentId={incidentId}
          idOrKey={idOrKey}
          workItem={replacing}
          onFormComplete={() => setReplacing(null)}
          onFormCancel={() => setReplacing(null)}
        />
      ) : null}
    </>
  )
}

export default IncidentWorkItemsSection
