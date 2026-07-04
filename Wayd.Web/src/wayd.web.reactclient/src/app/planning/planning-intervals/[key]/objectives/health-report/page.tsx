'use client'

import PageTitle from '@/src/components/common/page-title'
import { use, useMemo } from 'react'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { authorizePage } from '@/src/components/hoc'
import { notFound } from 'next/navigation'
import Link from 'next/link'
import {
  WaydGrid,
  renderTeamLink,
} from '@/src/components/common/wayd-grid'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import PiObjectiveHealthCheckTag from '@/src/app/planning/planning-intervals/_components/pi-objective-health-check-tag'
import { Progress } from 'antd'
import { useGetPlanningIntervalObjectivesHealthReportQuery } from '@/src/store/features/planning/planning-interval-api'
import { PlanningIntervalObjectiveHealthCheckDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import styles from './health-report-page.module.css'

const ObjectiveHealthReportPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const piKey = Number(key)

  useDocumentTitle('PI Objectives Health Report')

  const {
    data: healthReport,
    isLoading,
    refetch,
  } = useGetPlanningIntervalObjectivesHealthReportQuery({
    planningIntervalKey: piKey,
  })

  const columns = useMemo<
    ColumnDef<PlanningIntervalObjectiveHealthCheckDto, any>[]
  >(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 400,
        meta: { filterEnableSet: true },
        cell: ({ row }) => (
          <Link
            href={`/planning/planning-intervals/${row.original.planningInterval?.key}/objectives/${row.original.key}`}
          >
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'isStretch',
        accessorKey: 'isStretch',
        header: 'Stretch',
        meta: { columnType: 'yesNo' },
      },
      {
        id: 'status',
        accessorKey: 'status.name',
        header: 'Status',
        size: 125,
        meta: { filterType: 'set' },
      },
      {
        id: 'team',
        accessorKey: 'team.name',
        header: 'Team',
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.team),
      },
      {
        id: 'progress',
        accessorKey: 'progress',
        header: 'Progress',
        size: 250,
        enableColumnFilter: false,
        cell: ({ row }) => {
          const status = ['Canceled', 'Missed'].includes(
            row.original.status?.name,
          )
            ? 'exception'
            : undefined
          return (
            <Progress
              percent={row.original.progress}
              size="small"
              status={status}
            />
          )
        },
      },
      {
        id: 'health',
        accessorFn: (row) => row.healthStatus?.name ?? '',
        header: 'Health',
        size: 115,
        cell: ({ row }) => {
          const item = row.original
          if (!item.healthCheckId) return null
          return (
            <PiObjectiveHealthCheckTag
              healthCheck={{
                id: item.healthCheckId,
                status: item.healthStatus!,
                reportedOn: item.reportedOn,
                expiration: item.expiration,
                note: item.note,
              }}
              planningIntervalId={item.planningInterval?.id}
              objectiveId={item.id}
            />
          )
        },
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

  if (!isLoading && !healthReport) {
    return notFound()
  }

  return (
    <>
      <PageTitle title="PI Objectives Health Report" />
      <WaydGrid
        columns={columns}
        data={healthReport ?? []}
        isLoading={isLoading}
        onRefresh={refresh}
        csvFileName="pi-objectives-health-report"
      />
    </>
  )
}

const ObjectiveHealthReportPageWithAuthorization = authorizePage(
  ObjectiveHealthReportPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default ObjectiveHealthReportPageWithAuthorization
