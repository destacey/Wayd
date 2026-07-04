'use client'

import { useMemo } from 'react'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import Link from 'next/link'
import PiObjectiveHealthCheckTag from './pi-objective-health-check-tag'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { PlanningIntervalObjectiveHealthCheckDetailsDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { useGetObjectiveHealthChecksQuery } from '@/src/store/features/planning/pi-objective-health-checks-api'
import styles from './pi-objective-health-report-grid.module.css'

interface PiObjectiveHealthReportGridProps {
  planningIntervalId: string
  objectiveId: string
}

const PiObjectiveHealthReportGrid = (
  props: PiObjectiveHealthReportGridProps,
) => {
  const {
    data: healthReportData,
    isLoading,
    refetch,
  } = useGetObjectiveHealthChecksQuery(
    {
      planningIntervalId: props.planningIntervalId,
      objectiveId: props.objectiveId,
    },
    { skip: !props.planningIntervalId || !props.objectiveId },
  )

  const columns = useMemo<
    ColumnDef<PlanningIntervalObjectiveHealthCheckDetailsDto, any>[]
  >(
    () => [
      {
        id: 'status',
        accessorKey: 'status.name',
        header: 'Health',
        size: 115,
        meta: { filterType: 'set' },
        cell: ({ row }) => <PiObjectiveHealthCheckTag healthCheck={row.original} />,
      },
      {
        id: 'note',
        accessorKey: 'note',
        header: 'Note',
        size: 400,
        cell: ({ getValue }) => {
          const note = getValue() as string | null | undefined
          if (!note) return null
          return (
            <div className={styles.markdown}>
              <MarkdownRenderer markdown={note} />
            </div>
          )
        },
      },
      {
        id: 'reportedBy',
        accessorKey: 'reportedBy.name',
        header: 'Reported By',
        cell: ({ row }) =>
          row.original.reportedBy ? (
            <Link
              href={`/organizations/employees/${row.original.reportedBy.key}`}
            >
              {row.original.reportedBy.name}
            </Link>
          ) : null,
      },
      {
        id: 'reportedOn',
        accessorKey: 'reportedOn',
        header: 'Reported On',
        meta: { columnType: 'dateTime' },
      },
      {
        id: 'expiration',
        accessorKey: 'expiration',
        header: 'Expiration',
        meta: { columnType: 'dateTime' },
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      height={550}
      columns={columns}
      data={healthReportData ?? []}
      onRefresh={refresh}
      isLoading={isLoading}
      csvFileName="pi-objective-health-report"
    />
  )
}

export default PiObjectiveHealthReportGrid
