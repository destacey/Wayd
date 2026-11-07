'use client'

import {
  WaydGrid,
  WaydGridPager,
  createActionsColumn,
  useGridPaging,
} from '@/src/components/common/wayd-grid'
import { useState } from 'react'
import { Badge, Button, Checkbox, Flex, Segmented, Typography } from 'antd'
import type { RowSelectionState } from '@tanstack/react-table'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import type { ItemType } from 'antd/es/menu/interface'
import useAuth from '@/src/components/contexts/auth'
import {
  CurrentJobCountsResponse,
  JobStateFilter,
  JobSummaryResponse,
} from '@/src/services/wayd-api'
import {
  JOB_LIST_POLLING_MS,
  useGetJobsQuery,
} from '@/src/store/features/admin/background-jobs-api'
import JobDetailsDrawer from './job-details-drawer'
import ElapsedTime from './elapsed-time'
import { findLongestRunningJobId, isRequeueableState } from './job-health'
import useBackgroundJobActions from './use-background-job-actions'
import { formatMilliseconds } from '@/src/utils/duration'
import styles from './job-health-summary.module.css'

/**
 * The states in the order the summary strip reads them: live work first, then
 * outcomes. `timestampLabel` names each state's timestamp column, matching the
 * label the API sends with each row, for a list with no rows to read it from.
 */
const states: {
  label: string
  value: JobStateFilter
  timestampLabel: string
  count: (c: CurrentJobCountsResponse) => number
}[] = [
  {
    label: 'Processing',
    value: JobStateFilter.Processing,
    timestampLabel: 'Started',
    count: (c) => c.processing,
  },
  {
    label: 'Enqueued',
    value: JobStateFilter.Enqueued,
    timestampLabel: 'Enqueued',
    count: (c) => c.enqueued,
  },
  {
    label: 'Scheduled',
    value: JobStateFilter.Scheduled,
    timestampLabel: 'Runs At',
    count: (c) => c.scheduled,
  },
  {
    label: 'Failed',
    value: JobStateFilter.Failed,
    timestampLabel: 'Failed',
    count: (c) => c.failed,
  },
  {
    label: 'Succeeded',
    value: JobStateFilter.Succeeded,
    timestampLabel: 'Succeeded',
    count: (c) => c.succeeded,
  },
  {
    label: 'Deleted',
    value: JobStateFilter.Deleted,
    timestampLabel: 'Deleted',
    count: (c) => c.deleted,
  },
]

const sameIds = (a: string[], b: string[]) =>
  a.length === b.length && a.every((id, i) => id === b[i])

export interface JobsTabProps {
  state: JobStateFilter
  onStateChange: (state: JobStateFilter) => void
  /** The live counts shown on the state filter; omitted while they load. */
  counts?: CurrentJobCountsResponse
}

const JobsTab = ({ state, onStateChange, counts }: JobsTabProps) => {
  const [viewingJobId, setViewingJobId] = useState<string | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({})
  const [displayedJobIds, setDisplayedJobIds] = useState<string[]>([])
  const [isBulkRunning, setIsBulkRunning] = useState(false)
  const paging = useGridPaging()

  // The state can change from outside (the summary strip's counts), so a new
  // list starts on its first page with nothing selected. Adjusted during
  // render rather than in an effect, so the old page never paints.
  const [shownState, setShownState] = useState(state)
  if (shownState !== state) {
    setShownState(state)
    setRowSelection({})
    paging.resetPage()
  }

  const { hasPermissionClaim } = useAuth()
  const canRun = hasPermissionClaim('Permissions.BackgroundJobs.Run')
  const canDelete = hasPermissionClaim('Permissions.BackgroundJobs.Delete')

  const isProcessing = state === JobStateFilter.Processing
  const isFailed = state === JobStateFilter.Failed
  const isSucceeded = state === JobStateFilter.Succeeded
  // Requeueing a running job starts a second copy while the first keeps going.
  const canRequeueInState = canRun && !isProcessing
  const showRowActions = canRequeueInState || canDelete

  // `currentData` for the rows, so a new page or state never shows the last one's rows under its own
  // label; `data`, the last answer for any page, for the pager, so it holds still while a page loads.
  // Loading only while there is no current page: a poll refetches the same page and must not flash.
  const {
    data: lastJobs,
    currentData: jobs,
    isFetching,
    refetch,
  } = useGetJobsQuery(
    { state, page: paging.page, pageSize: paging.pageSize },
    { pollingInterval: JOB_LIST_POLLING_MS },
  )

  paging.keepInRange(lastJobs?.totalPages)

  const { handleRequeue, handleDelete, handleBulkRequeue, handleBulkDelete } =
    useBackgroundJobActions()

  // A bulk action covers only rows on screen: a selected job that finished,
  // moved state, or is hidden by a grid filter is dropped from the selection,
  // so it is neither acted on nor left holding the header checkbox half-ticked.
  const handleDisplayedRowsChange = (rows: JobSummaryResponse[]) => {
    const ids = rows.map((row) => row.id)
    setDisplayedJobIds((prev) => (sameIds(prev, ids) ? prev : ids))

    const shown = new Set(ids)
    setRowSelection((prev) => {
      const kept = Object.keys(prev).filter((id) => prev[id] && shown.has(id))
      return kept.length === Object.keys(prev).length
        ? prev
        : Object.fromEntries(kept.map((id) => [id, true]))
    })
  }

  const displayed = new Set(displayedJobIds)
  const selectedJobs = (jobs?.items ?? []).filter(
    (job) => rowSelection[job.id] && displayed.has(job.id),
  )
  const clearSelection = () => setRowSelection({})

  // The buttons stay disabled until the batch settles, so a second click can't send it again.
  const runBulk = async (action: () => Promise<boolean | void>) => {
    setIsBulkRunning(true)
    try {
      const completed = await action()
      if (completed !== false) clearSelection()
    } finally {
      setIsBulkRunning(false)
    }
  }

  const openDetailsDrawer = (id: string) => {
    setViewingJobId(id)
    setDrawerOpen(true)
  }

  const closeDetailsDrawer = () => {
    setDrawerOpen(false)
    setViewingJobId(null)
  }

  const longestRunningId = isProcessing
    ? findLongestRunningJobId(jobs?.items)
    : undefined
  // The meaningful timestamp differs per state; the API labels it.
  const timestampLabel =
    jobs?.items?.[0]?.timestampLabel ??
    states.find((s) => s.value === state)?.timestampLabel ??
    'Timestamp'

  const selectColumn: ColumnDef<JobSummaryResponse, any> = {
    id: 'select',
    size: 40,
    enableSorting: false,
    enableColumnFilter: false,
    enableResizing: false,
    enableGlobalFilter: false,
    // A control column: holds its position and has no column menu.
    meta: { enableReordering: false },
    header: ({ table }) => (
      <Checkbox
        aria-label="Select all jobs shown"
        tabIndex={-1}
        checked={table.getIsAllRowsSelected()}
        // getIsSomeRowsSelected() is "at least one", so exclude all-selected for the dash state.
        indeterminate={
          !table.getIsAllRowsSelected() && table.getIsSomeRowsSelected()
        }
        onChange={() => table.toggleAllRowsSelected()}
      />
    ),
    cell: ({ row }) => (
      <Checkbox
        aria-label={`Select job ${row.original.id}`}
        tabIndex={-1}
        checked={row.getIsSelected()}
        onChange={() => row.toggleSelected()}
      />
    ),
  }

  const exceptionColumns: ColumnDef<JobSummaryResponse, any>[] = [
    {
      id: 'exceptionMessage',
      accessorKey: 'exceptionMessage',
      header: 'Exception Message',
      size: 360,
      cell: ({ row }) =>
        row.original.exceptionMessage ? (
          <Typography.Text
            type="danger"
            ellipsis={{ tooltip: row.original.exceptionMessage }}
          >
            {row.original.exceptionMessage}
          </Typography.Text>
        ) : null,
    },
    {
      id: 'exceptionType',
      accessorKey: 'exceptionType',
      header: 'Exception Type',
      size: 200,
      meta: { filterType: 'set' },
    },
  ]

  const processingColumns: ColumnDef<JobSummaryResponse, any>[] = [
    {
      id: 'runningFor',
      accessorKey: 'timestamp',
      header: 'Running For',
      size: 180,
      enableColumnFilter: false,
      cell: ({ row }) => {
        if (!row.original.timestamp) return null
        return row.original.id === longestRunningId ? (
          <Typography.Text type="warning" strong>
            <ElapsedTime start={row.original.timestamp} /> · longest
          </Typography.Text>
        ) : (
          <ElapsedTime start={row.original.timestamp} />
        )
      },
    },
    {
      id: 'serverName',
      accessorKey: 'serverName',
      header: 'Server',
      size: 260,
      meta: { filterType: 'set' },
    },
  ]

  const columns: ColumnDef<JobSummaryResponse, any>[] = [
    ...(showRowActions ? [selectColumn] : []),
    createActionsColumn<JobSummaryResponse>({
      unavailable: !showRowActions,
      ariaLabel: 'Job actions',
      getItems: (job) => {
        const items: ItemType[] = []

        if (canRun && isRequeueableState(job.state)) {
          items.push({
            key: 'requeue',
            label: 'Requeue',
            onClick: () => handleRequeue(job),
          })
        }

        if (canDelete) {
          if (items.length > 0) {
            items.push({ key: 'divider', type: 'divider' })
          }
          items.push({
            key: 'delete',
            label: 'Delete',
            danger: true,
            onClick: () => handleDelete(job),
          })
        }

        return items
      },
    }),
    {
      id: 'action',
      accessorKey: 'action',
      header: 'Job',
      size: 240,
      meta: { filterType: 'set' },
      cell: ({ row }) => (
        <Button
          type="link"
          style={{ padding: 0, height: 'auto', fontSize: 'inherit' }}
          onClick={() => openDetailsDrawer(row.original.id)}
        >
          {row.original.action}
        </Button>
      ),
    },
    // The error is what a failed list is read for, so it sits beside the job name.
    ...(isFailed ? exceptionColumns : []),
    {
      id: 'type',
      accessorKey: 'type',
      header: 'Type',
      size: 220,
      meta: { filterType: 'set' },
    },
    // A run reads start, end, length; Started and Duration sit either side of Succeeded.
    ...(isSucceeded
      ? ([
          {
            id: 'startedAt',
            accessorKey: 'startedAt',
            header: 'Started',
            meta: { columnType: 'dateTime' },
          },
        ] as ColumnDef<JobSummaryResponse, any>[])
      : []),
    {
      id: 'timestamp',
      accessorKey: 'timestamp',
      header: timestampLabel,
      meta: { columnType: 'dateTime' },
    },
    ...(isSucceeded
      ? ([
          {
            id: 'durationMilliseconds',
            accessorKey: 'durationMilliseconds',
            header: 'Duration',
            size: 120,
            enableColumnFilter: false,
            cell: ({ row }) =>
              row.original.durationMilliseconds != null ? (
                <span className={styles.elapsed}>
                  {formatMilliseconds(row.original.durationMilliseconds)}
                </span>
              ) : null,
          },
        ] as ColumnDef<JobSummaryResponse, any>[])
      : []),
    ...(isProcessing ? processingColumns : []),
    {
      id: 'namespace',
      accessorKey: 'namespace',
      header: 'Namespace',
      size: 260,
      meta: { filterType: 'set' },
    },
    { id: 'id', accessorKey: 'id', header: 'Job Id', size: 120 },
  ]

  const stateOptions = states.map(({ label, value, count }) => {
    const n = counts ? count(counts) : undefined
    return {
      value,
      label:
        value === JobStateFilter.Failed && n ? (
          <Flex gap="small" align="center">
            {label}
            <Badge count={n} overflowCount={9999} size="small" />
          </Flex>
        ) : (
          <span>
            {label}
            {n !== undefined && (
              <Typography.Text type="secondary">
                {' '}
                {n.toLocaleString()}
              </Typography.Text>
            )}
          </span>
        ),
    }
  })

  const bulkActions =
    selectedJobs.length > 0 ? (
      <Flex gap="small" align="center" wrap>
        <Typography.Text strong>{selectedJobs.length} selected</Typography.Text>
        {canRequeueInState && (
          <Button
            size="small"
            loading={isBulkRunning}
            disabled={isBulkRunning}
            onClick={() => runBulk(() => handleBulkRequeue(selectedJobs))}
          >
            Requeue
          </Button>
        )}
        {canDelete && (
          <Button
            size="small"
            danger
            disabled={isBulkRunning}
            onClick={() => runBulk(() => handleBulkDelete(selectedJobs))}
          >
            Delete
          </Button>
        )}
        <Button
          size="small"
          type="link"
          disabled={isBulkRunning}
          onClick={clearSelection}
        >
          Clear
        </Button>
      </Flex>
    ) : undefined

  return (
    <>
      <Segmented
        options={stateOptions}
        value={state}
        onChange={(value) => onStateChange(value as JobStateFilter)}
        style={{ marginBottom: 12 }}
      />
      <WaydGrid
        columns={columns}
        data={jobs?.items}
        getRowId={(job) => job.id}
        rowSelection={showRowActions ? rowSelection : undefined}
        onRowSelectionChange={showRowActions ? setRowSelection : undefined}
        onDisplayedRowsChange={handleDisplayedRowsChange}
        onRefresh={refetch}
        isLoading={!jobs && isFetching}
        persistStateKey="settings-background-jobs-list"
        csvFileName="background-jobs"
        emptyMessage="There are no jobs in this state."
        leftSlot={bulkActions}
        rightSlot={
          <WaydGridPager
            paging={paging}
            totalCount={lastJobs?.totalCount}
            noun="jobs"
          />
        }
      />
      {viewingJobId !== null && (
        <JobDetailsDrawer
          jobId={viewingJobId}
          drawerOpen={drawerOpen}
          onDrawerClose={closeDetailsDrawer}
        />
      )}
    </>
  )
}

export default JobsTab
