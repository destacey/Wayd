'use client'

import React, { useMemo } from 'react'
import WaydGrid from '../../../../components/common/wayd-grid'
import Link from 'next/link'
import dayjs from 'dayjs'
import {
  PiObjectiveHealthCheckStatusCellRenderer,
  MarkdownCellRenderer,
} from '../../../../components/common/wayd-grid-cell-renderers'
import { PlanningIntervalObjectiveHealthCheckDetailsDto } from '@/src/services/wayd-api'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import { useGetObjectiveHealthChecksQuery } from '@/src/store/features/planning/pi-objective-health-checks-api'

interface PiObjectiveHealthReportGridProps {
  planningIntervalId: string
  objectiveId: string
}

const ReportedByLinkCellRenderer = ({ value, data }: ICellRendererParams<PlanningIntervalObjectiveHealthCheckDetailsDto>) => {
  return (
    <Link href={`/organizations/employees/${data!.reportedBy?.key}`}>
      {value}
    </Link>
  )
}

const PiObjectiveHealthReportGrid = (
  props: PiObjectiveHealthReportGridProps,
) => {
  const {
    data: healthReportData,
    isLoading,
    isFetching,
    error,
    refetch,
  } = useGetObjectiveHealthChecksQuery(
    {
      planningIntervalId: props.planningIntervalId,
      objectiveId: props.objectiveId,
    },
    { skip: !props.planningIntervalId || !props.objectiveId },
  )

  const columnDefs = useMemo<
    ColDef<PlanningIntervalObjectiveHealthCheckDetailsDto>[]
  >(
    () => [
      { field: 'id', hide: true },
      {
        field: 'status.name',
        headerName: 'Health',
        width: 115,
        cellRenderer: PiObjectiveHealthCheckStatusCellRenderer,
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
        valueGetter: (params) =>
          params.data?.reportedOn ? dayjs(params.data.reportedOn).format('M/D/YYYY h:mm A') : null,
      },
      {
        field: 'expiration',
        valueGetter: (params) =>
          params.data?.expiration ? dayjs(params.data.expiration).format('M/D/YYYY h:mm A') : null,
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <>
      <WaydGrid
        height={550}
        columnDefs={columnDefs}
        rowData={healthReportData}
        loadData={refresh}
        loading={isLoading}
      />
    </>
  )
}

export default PiObjectiveHealthReportGrid
