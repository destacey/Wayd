'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { useMemo, useState } from 'react'
import { Button, Segmented, Typography } from 'antd'
import type { ColumnDef } from '../../../../components/common/wayd-grid-core'
import type { ItemType } from 'antd/es/menu/interface'
import useAuth from '@/src/components/contexts/auth'
import { JobStateFilter, JobSummaryResponse } from '@/src/services/wayd-api'
import {
  JOB_LIST_POLLING_MS,
  useGetJobsQuery,
} from '@/src/store/features/admin/background-jobs-api'
import JobDetailsDrawer from './job-details-drawer'
import useBackgroundJobActions from './use-background-job-actions'

// The API caps a page at 500. Fetch one max-size page and let the grid
// filter/sort client-side; the overflow note carries the true total.
const JOB_PAGE_SIZE = 500

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

  const { hasPermissionClaim } = useAuth()
  const canRun = hasPermissionClaim('Permissions.BackgroundJobs.Run')
  const canDelete = hasPermissionClaim('Permissions.BackgroundJobs.Delete')
  const showRowActions = canRun || canDelete

  // isLoading, not isFetching: isFetching is true on every poll, which would flash
  // the grid's loading state every few seconds.
  const {
    data: jobs,
    isLoading,
    refetch,
  } = useGetJobsQuery(
    { state, pageSize: JOB_PAGE_SIZE },
    { pollingInterval: JOB_LIST_POLLING_MS },
  )

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
        hide: !showRowActions,
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

  const overflow = jobs && jobs.totalCount > jobs.items.length

  return (
    <>
      <Segmented
        options={stateOptions}
        value={state}
        onChange={(value) => setState(value as JobStateFilter)}
        style={{ marginBottom: 12 }}
      />
      <WaydGrid
        columns={columns}
        data={jobs?.items}
        onRefresh={refetch}
        isLoading={isLoading}
        persistStateKey="settings-background-jobs-list"
        csvFileName="background-jobs"
        initialSorting={[{ id: 'timestamp', desc: true }]}
        emptyMessage="There are no jobs in this state."
        leftSlot={
          overflow ? (
            <Typography.Text type="warning">
              {`Showing the first ${jobs.items.length} of ${jobs.totalCount} jobs.`}
            </Typography.Text>
          ) : undefined
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
