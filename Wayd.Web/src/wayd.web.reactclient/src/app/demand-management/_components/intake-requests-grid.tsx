'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import EntityLink from '@/src/components/common/entity-link'
import {
  statusCategoryDescription,
  WorkflowStatusTag,
} from '@/src/components/common/status-workflows'
import {
  IntakeRequestSummaryDto,
  NeedByBasisDto,
  NeedByBasisFamily,
} from '@/src/services/wayd-api'
import { formatCalendarDate } from '@/src/utils'
import { Tooltip } from 'antd'
import Link from 'next/link'
import { FC, ReactNode } from 'react'
import { NeedByTag } from './need-by'
import { AgeTag, daysSince } from './request-age'

export interface IntakeRequestsGridProps {
  requests: IntakeRequestSummaryDto[] | undefined
  isLoading: boolean
  refetch: () => void
  /** The need-by bases, from which the basis column takes its labels. */
  needByBases: NeedByBasisDto[] | undefined
  /** Separate per page, since each page shows the list for a different question. */
  persistStateKey: string
  csvFileName: string
  emptyMessage?: string
  leftSlot?: ReactNode
}

export const requestHref = (key: number) => `/demand-management/requests/${key}`

/**
 * Orders by need-by date, and on the same date puts a date fixed for us before one we chose, since that is
 * the one that cannot move. Requests with no date come last.
 */
export const compareNeedBy = (
  a: IntakeRequestSummaryDto,
  b: IntakeRequestSummaryDto,
): number => {
  if (!a.needBy || !b.needBy) return a.needBy ? -1 : b.needBy ? 1 : 0
  if (a.needBy.date !== b.needBy.date)
    return a.needBy.date < b.needBy.date ? -1 : 1
  const fixed = (r: IntakeRequestSummaryDto) =>
    r.needBy?.family === NeedByBasisFamily.FixedForUs ? 0 : 1
  return fixed(a) - fixed(b)
}

/**
 * Intake requests as their summaries: the columns are the summary tier and nothing more, because that is
 * all a list returns. People are plain names, not links, since most requesters cannot open employee pages.
 */
const IntakeRequestsGrid: FC<IntakeRequestsGridProps> = ({
  requests,
  isLoading,
  refetch,
  needByBases,
  persistStateKey,
  csvFileName,
  emptyMessage,
  leftSlot,
}) => {
  const basisName = (code: string | undefined) =>
    code ? (needByBases?.find((b) => b.code === code)?.name ?? code) : ''

  const columns: ColumnDef<IntakeRequestSummaryDto, any>[] = [
    {
      id: 'key',
      accessorKey: 'key',
      header: 'Number',
      size: 100,
      cell: ({ row }) => (
        <Link href={requestHref(row.original.key)}>{row.original.key}</Link>
      ),
    },
    {
      id: 'title',
      accessorKey: 'title',
      header: 'Title',
      size: 380,
      cell: ({ row }) => (
        <EntityLink href={requestHref(row.original.key)}>
          {row.original.title}
        </EntityLink>
      ),
    },
    {
      id: 'status',
      accessorFn: (request) => request.status.name,
      header: 'Status',
      size: 150,
      meta: { filterType: 'set' },
      // A status name is the desk workflow's own word. The tooltip carries the category, which is the
      // fixed meaning that decides whether the request is still open.
      cell: ({ row }) => (
        <Tooltip
          title={statusCategoryDescription(row.original.status.category)}
        >
          <span>
            <WorkflowStatusTag
              name={row.original.status.name}
              category={row.original.status.category}
            />
          </span>
        </Tooltip>
      ),
    },
    {
      id: 'timeInStatus',
      accessorFn: (request) => daysSince(request.statusChangedOn),
      header: 'In Status',
      size: 100,
      cell: ({ row }) => (
        <AgeTag
          since={row.original.statusChangedOn}
          label="In this status since"
        />
      ),
      meta: { exportFormatter: (value: unknown) => `${value}` },
    },
    {
      id: 'needBy',
      accessorFn: (request) => request.needBy?.date ?? null,
      header: 'Need By',
      size: 260,
      sortFn: (a, b) => compareNeedBy(a.original, b.original),
      cell: ({ row }) =>
        row.original.needBy ? <NeedByTag needBy={row.original.needBy} /> : null,
      meta: {
        filterType: 'date',
        exportFormatter: (value: unknown) =>
          typeof value === 'string' ? formatCalendarDate(value) : '',
      },
    },
    {
      id: 'age',
      accessorFn: (request) => daysSince(request.submittedOn),
      header: 'Age',
      size: 90,
      cell: ({ row }) => (
        <AgeTag since={row.original.submittedOn} label="Submitted" />
      ),
      meta: { exportFormatter: (value: unknown) => `${value}` },
    },
    {
      id: 'assignee',
      accessorFn: (request) => request.assignee?.name ?? '',
      header: 'Assignee',
      size: 180,
      meta: { filterType: 'set' },
    },
    {
      id: 'desk',
      accessorKey: 'desk.name',
      header: 'Desk',
      size: 180,
      meta: { filterType: 'set' },
    },
    {
      id: 'requester',
      accessorKey: 'requester.name',
      header: 'Requester',
      size: 180,
      meta: { filterType: 'set' },
    },
    {
      id: 'channel',
      accessorKey: 'channel.name',
      header: 'Channel',
      size: 180,
      meta: { filterType: 'set' },
    },
    {
      id: 'needByBasis',
      accessorFn: (request) => basisName(request.needBy?.basis),
      header: 'Basis',
      size: 170,
      meta: { filterType: 'set', hiddenByDefault: true },
    },
  ]

  return (
    <WaydGrid
      columns={columns}
      data={requests}
      isLoading={isLoading}
      onRefresh={refetch}
      persistStateKey={persistStateKey}
      csvFileName={csvFileName}
      emptyMessage={emptyMessage}
      leftSlot={leftSlot}
      initialSorting={[{ id: 'key', desc: true }]}
    />
  )
}

export default IntakeRequestsGrid
