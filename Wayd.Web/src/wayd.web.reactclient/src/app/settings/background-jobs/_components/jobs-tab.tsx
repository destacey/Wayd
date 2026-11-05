'use client'

import {
  WaydGrid,
  WaydGridPager,
  createActionsColumn,
  useGridPaging,
} from '@/src/components/common/wayd-grid'
import { useMemo, useState } from 'react'
import { Button, Segmented } from 'antd'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import type { ItemType } from 'antd/es/menu/interface'
import useAuth from '@/src/components/contexts/auth'
import { JobStateFilter, JobSummaryResponse } from '@/src/services/wayd-api'
import {
  JOB_LIST_POLLING_MS,
  useGetJobsQuery,
} from '@/src/store/features/admin/background-jobs-api'
import JobDetailsDrawer from './job-details-drawer'
import useBackgroundJobActions from './use-background-job-actions'

const stateOptions = [
  { label: 'Processing', value: JobStateFilter.Processing },
  { label: 'Scheduled', value: JobStateFilter.Scheduled },
  { label: 'Enqueued', value: JobStateFilter.Enqueued },
  { label: 'Failed', value: JobStateFilter.Failed },
  { label: 'Succeeded', value: JobStateFilter.Succeeded },
  { label: 'Deleted', value: JobStateFilter.Deleted },
]

const JobsTab = () => {
  const [state, setState] = useState<JobStateFilter>(JobStateFilter.Processing)
  const [viewingJobId, setViewingJobId] = useState<string | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const paging = useGridPaging()

  const { hasPermissionClaim } = useAuth()
  const canRun = hasPermissionClaim('Permissions.BackgroundJobs.Run')
  const canDelete = hasPermissionClaim('Permissions.BackgroundJobs.Delete')
  const showRowActions = canRun || canDelete

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

  const { handleRequeue, handleDelete } = useBackgroundJobActions()

  const closeDetailsDrawer = () => {
    setDrawerOpen(false)
    setViewingJobId(null)
  }

  const columns = useMemo<ColumnDef<JobSummaryResponse, any>[]>(() => {
    const openDetailsDrawer = (id: string) => {
      setViewingJobId(id)
      setDrawerOpen(true)
    }

    const isFailed = state === JobStateFilter.Failed

    return [
      createActionsColumn<JobSummaryResponse>({
        unavailable: !showRowActions,
        ariaLabel: 'Job actions',
        getItems: (job) => {
          const items: ItemType[] = []

          if (canRun) {
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
      {
        id: 'type',
        accessorKey: 'type',
        header: 'Type',
        size: 220,
        meta: { filterType: 'set' },
      },
      {
        id: 'timestamp',
        accessorKey: 'timestamp',
        // The meaningful timestamp differs per state; the API labels it.
        header: jobs?.items?.[0]?.timestampLabel ?? 'Timestamp',
        meta: { columnType: 'dateTime' },
      },
      ...(isFailed
        ? ([
            {
              id: 'exceptionType',
              accessorKey: 'exceptionType',
              header: 'Exception Type',
              size: 200,
              meta: { filterType: 'set' },
            },
            {
              id: 'exceptionMessage',
              accessorKey: 'exceptionMessage',
              header: 'Exception Message',
              size: 320,
            },
          ] as ColumnDef<JobSummaryResponse, any>[])
        : []),
      {
        id: 'namespace',
        accessorKey: 'namespace',
        header: 'Namespace',
        size: 260,
        meta: { filterType: 'set' },
      },
      { id: 'id', accessorKey: 'id', header: 'Job Id', size: 120 },
    ]
  }, [
    state,
    showRowActions,
    canRun,
    canDelete,
    handleRequeue,
    handleDelete,
    jobs,
  ])

  return (
    <>
      <Segmented
        options={stateOptions}
        value={state}
        onChange={(value) => {
          setState(value as JobStateFilter)
          paging.resetPage()
        }}
        style={{ marginBottom: 12 }}
      />
      <WaydGrid
        columns={columns}
        data={jobs?.items}
        onRefresh={refetch}
        isLoading={!jobs && isFetching}
        persistStateKey="settings-background-jobs-list"
        csvFileName="background-jobs"
        emptyMessage="There are no jobs in this state."
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
