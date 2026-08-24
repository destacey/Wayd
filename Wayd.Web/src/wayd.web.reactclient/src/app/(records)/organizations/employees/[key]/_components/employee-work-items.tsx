'use client'

import { WorkItemsGrid } from '@/src/components/common/work'
import { WorkStatusCategory } from '@/src/services/wayd-api'
import { useGetEmployeeWorkItemsQuery } from '@/src/store/features/organizations/employee-api'
import { FC } from 'react'

export interface EmployeeWorkItemsProps {
  employeeId: string
}

/**
 * Work currently assigned to the employee.
 *
 * Done and Removed are excluded: the question this section answers is "what is
 * this person working on", and completed work is already served by the Cycle
 * Time report. A fetch wrapper only — the grid is the shared `WorkItemsGrid`.
 */
const EmployeeWorkItems: FC<EmployeeWorkItemsProps> = ({ employeeId }) => {
  const workItemsQuery = useGetEmployeeWorkItemsQuery(
    {
      employeeId,
      statusCategories: [WorkStatusCategory.Proposed, WorkStatusCategory.Active],
    },
    { skip: !employeeId },
  )

  return (
    <WorkItemsGrid
      workItems={workItemsQuery.data ?? []}
      isLoading={workItemsQuery.isLoading}
      refetch={workItemsQuery.refetch}
      persistStateKey="employee-work-items"
    />
  )
}

export default EmployeeWorkItems
