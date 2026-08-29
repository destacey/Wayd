'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { WorkProcessSchemeDto } from '@/src/services/wayd-api'
import { useGetWorkProcessSchemesQuery } from '@/src/store/features/work-management/work-process-api'

export interface WorkProcessSchemesProps {
  workProcessId: string
}

/**
 * The work types a process covers, and the workflow each one runs.
 *
 * This mapping is why a work process is a record page rather than a config
 * list panel: it is a child collection with its own query, not another field
 * on the record.
 */
const WorkProcessSchemes = ({ workProcessId }: WorkProcessSchemesProps) => {
  const { data: schemes, isLoading } =
    useGetWorkProcessSchemesQuery(workProcessId)

  const columns: ColumnDef<WorkProcessSchemeDto, any>[] = [
    {
      id: 'workType',
      accessorKey: 'workType.name',
      header: 'Work Type',
      size: 200,
    },
    {
      id: 'workTypeDescription',
      accessorKey: 'workType.description',
      header: 'Description',
    },
    {
      id: 'workflow',
      accessorKey: 'workflow.name',
      header: 'Workflow',
      size: 200,
    },
    {
      id: 'isActive',
      accessorKey: 'isActive',
      header: 'Active',
      size: 100,
      meta: { columnType: 'yesNo' },
    },
  ]

  return (
    <WaydGrid
      variant="simple"
      columns={columns}
      data={schemes ?? []}
      isLoading={isLoading}
      emptyMessage="This work process has no work types."
    />
  )
}

export default WorkProcessSchemes
