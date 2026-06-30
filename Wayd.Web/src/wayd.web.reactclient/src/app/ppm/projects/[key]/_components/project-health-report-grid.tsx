'use client'

import { useMemo } from 'react'
import WaydGrid from '@/src/components/common/wayd-grid'
import Link from 'next/link'
import {
  ProjectHealthCheckStatusCellRenderer,
  MarkdownCellRenderer,
} from '@/src/components/common/wayd-grid-cell-renderers'
import { ProjectHealthCheckDetailsDto } from '@/src/services/wayd-api'
import { ColDef, ICellRendererParams } from 'ag-grid-community'

interface ProjectHealthReportGridProps {
  data?: ProjectHealthCheckDetailsDto[]
  isLoading: boolean
  refetch: () => void
}

const ReportedByLinkCellRenderer = ({
  value,
  data,
}: ICellRendererParams<ProjectHealthCheckDetailsDto>) => (
  <Link href={`/organizations/employees/${data?.reportedBy?.key}`}>{value}</Link>
)

const ProjectHealthReportGrid = ({
  data,
  isLoading,
  refetch,
}: ProjectHealthReportGridProps) => {
  const columnDefs = useMemo<ColDef<ProjectHealthCheckDetailsDto>[]>(
    () => [
      { field: 'id', hide: true },
      {
        field: 'status.name',
        headerName: 'Health',
        width: 115,
        cellRenderer: ProjectHealthCheckStatusCellRenderer,
      },
      {
        field: 'note',
        width: 400,
        autoHeight: true,
        wrapText: true,
        cellRenderer: MarkdownCellRenderer,
      },
      {
        field: 'reportedBy.name',
        headerName: 'Reported By',
        cellRenderer: ReportedByLinkCellRenderer,
      },
      {
        field: 'reportedOn',
        type: 'dateTime',
      },
      {
        field: 'expiration',
        type: 'dateTime',
      },
    ],
    [],
  )

  return (
    <WaydGrid
      height={550}
      columnDefs={columnDefs}
      rowData={data}
      loadData={() => {
        refetch()
      }}
      loading={isLoading}
      emptyMessage="No health checks found."
    />
  )
}

export default ProjectHealthReportGrid

