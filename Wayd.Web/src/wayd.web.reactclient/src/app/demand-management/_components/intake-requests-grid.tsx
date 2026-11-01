'use client'

import { NOT_RECORDED_CHANNEL_LABEL } from './channel'
import { WaydGrid, createCsvColumn } from '@/src/components/common/wayd-grid'
import type {
  ColumnDef,
  SortingState,
} from '@/src/components/common/wayd-grid-core'
import EntityLink from '@/src/components/common/entity-link'
import { tagText } from '@/src/components/common/tags'
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
import { deskHref } from './desk-status'
import EmployeeName from './employee-name'
import { WaitTime, daysSince } from './request-age'
import { ScoreValue } from './score-value'
import type { RequestScores } from './request-scores'

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
  /** Shown at the right of the grid's toolbar, such as a layout switcher. */
  rightSlot?: ReactNode
  /**
   * The requests' current scores, fetched beside the summaries since a score is substance. Adds a Score
   * column when given.
   */
  scores?: RequestScores
  /** The sort the grid opens on; newest first unless given. Empty keeps the order the requests arrive in. */
  initialSorting?: SortingState
}

export const requestHref = (key: number) => `/demand-management/requests/${key}`

/**
 * Orders by need-by date, ascending: on the same date a date fixed for us comes before one we chose, since
 * that is the one that cannot move, and requests with no date come last. The grid reverses the whole order
 * when sorting descending, as it does for every column's empty values.
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
 * all a list returns. The desk links to its page, which everyone can open; people link to theirs only for a
 * reader who can open employee pages, and are plain names for everyone else, as most requesters cannot.
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
  rightSlot,
  scores,
  initialSorting = [{ id: 'key', desc: true }],
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
    ...(scores
      ? [
          {
            id: 'score',
            accessorFn: (request: IntakeRequestSummaryDto) =>
              scores.get(request.id)?.value ?? null,
            header: 'Score',
            size: 100,
            cell: ({ row }: { row: { original: IntakeRequestSummaryDto } }) => (
              <ScoreValue score={scores.get(row.original.id)} />
            ),
          } satisfies ColumnDef<IntakeRequestSummaryDto, any>,
        ]
      : []),
    {
      id: 'status',
      accessorFn: (request) => request.status.name,
      header: 'Status',
      size: 150,
      meta: { filterType: 'set' },
      // A status name is the desk workflow's own word. The tooltip carries the category, which is the
      // fixed meaning that decides whether the request is still open.
      cell: ({ row }) => (
        <Tooltip title={statusCategoryDescription(row.original.status)}>
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
        <WaitTime
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
        <WaitTime since={row.original.submittedOn} label="Submitted" />
      ),
      meta: { exportFormatter: (value: unknown) => `${value}` },
    },
    {
      id: 'submittedOn',
      accessorKey: 'submittedOn',
      header: 'Submitted',
      size: 180,
      meta: { columnType: 'dateTime', hiddenByDefault: true },
    },
    {
      id: 'assignee',
      accessorFn: (request) => request.assignee?.name ?? '',
      header: 'Assignee',
      size: 180,
      cell: ({ row }) => <EmployeeName employee={row.original.assignee} />,
      meta: { filterType: 'set' },
    },
    {
      id: 'desk',
      accessorKey: 'desk.name',
      header: 'Desk',
      size: 180,
      cell: ({ row }) => (
        <Link href={deskHref(row.original.desk.key)} prefetch={false}>
          {row.original.desk.name}
        </Link>
      ),
      meta: { filterType: 'set' },
    },
    {
      id: 'requester',
      accessorKey: 'requester.name',
      header: 'Requester',
      size: 180,
      cell: ({ row }) => <EmployeeName employee={row.original.requester} />,
      meta: { filterType: 'set' },
    },
    {
      id: 'channel',
      accessorFn: (r) => r.channel?.name ?? NOT_RECORDED_CHANNEL_LABEL,
      header: 'Channel',
      size: 180,
      meta: { filterType: 'set' },
    },
    createCsvColumn<IntakeRequestSummaryDto>({
      id: 'tags',
      header: 'Tags',
      size: 260,
      // Qualified by axis, as the request page's chips are, so the set filter picks one axis's tag rather
      // than every tag sharing a name; a free tag reads as its name. Kept in the server's order: by axis,
      // then name, with free tags last.
      getValues: (r) =>
        r.tags.map((t) =>
          tagText({ name: t.name, categoryName: t.categoryName }),
        ),
    }),
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
      rightSlot={rightSlot}
      initialSorting={initialSorting}
    />
  )
}

export default IntakeRequestsGrid
