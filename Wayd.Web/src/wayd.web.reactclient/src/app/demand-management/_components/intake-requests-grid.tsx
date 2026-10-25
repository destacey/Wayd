'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import EntityLink from '@/src/components/common/entity-link'
import {
  IntakeRequestSummaryDto,
  NeedByBasisDto,
} from '@/src/services/wayd-api'
import { formatCalendarDate } from '@/src/utils'
import Link from 'next/link'
import { FC, ReactNode } from 'react'

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
      id: 'needBy',
      accessorFn: (request) => request.needBy?.date ?? null,
      header: 'Need By',
      size: 130,
      cell: ({ row }) =>
        row.original.needBy
          ? formatCalendarDate(row.original.needBy.date)
          : null,
      meta: {
        filterType: 'date',
        exportFormatter: (value: unknown) =>
          typeof value === 'string' ? formatCalendarDate(value) : '',
      },
    },
    {
      id: 'needByBasis',
      accessorFn: (request) => basisName(request.needBy?.basis),
      header: 'Basis',
      size: 170,
      meta: { filterType: 'set' },
    },
    {
      id: 'assignedReviewer',
      accessorFn: (request) => request.assignedReviewer?.name ?? '',
      header: 'Assigned Reviewer',
      size: 180,
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
