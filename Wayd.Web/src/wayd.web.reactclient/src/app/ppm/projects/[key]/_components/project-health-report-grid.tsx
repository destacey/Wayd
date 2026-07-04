'use client'

import { useMemo } from 'react'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import Link from 'next/link'
import ProjectHealthCheckTag from '@/src/app/ppm/projects/_components/project-health-check-tag'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { ProjectHealthCheckDetailsDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import styles from './project-health-report-grid.module.css'

interface ProjectHealthReportGridProps {
  data?: ProjectHealthCheckDetailsDto[]
  isLoading: boolean
  refetch: () => void
}

const ProjectHealthReportGrid = ({
  data,
  isLoading,
  refetch,
}: ProjectHealthReportGridProps) => {
  const columns = useMemo<ColumnDef<ProjectHealthCheckDetailsDto, any>[]>(
    () => [
      {
        id: 'status',
        accessorKey: 'status.name',
        header: 'Health',
        size: 115,
        meta: { filterType: 'set' },
        cell: ({ row }) => <ProjectHealthCheckTag healthCheck={row.original} />,
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

  return (
    <WaydGrid
      columns={columns}
      data={data ?? []}
      onRefresh={() => {
        refetch()
      }}
      isLoading={isLoading}
      csvFileName="project-health-report"
      emptyMessage="No health checks found."
    />
  )
}

export default ProjectHealthReportGrid
