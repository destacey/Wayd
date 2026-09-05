'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { useMemo } from 'react'
import { Typography } from 'antd'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import type { ItemType } from 'antd/es/menu/interface'
import useAuth from '@/src/components/contexts/auth'
import { RecurringJobResponse } from '@/src/services/wayd-api'
import {
  RECURRING_JOB_POLLING_MS,
  useGetRecurringJobsQuery,
} from '@/src/store/features/admin/background-jobs-api'
import useBackgroundJobActions from './use-background-job-actions'

const RecurringJobsTab = () => {
  const { hasPermissionClaim } = useAuth()
  const canDelete = hasPermissionClaim('Permissions.BackgroundJobs.Delete')

  const {
    data: recurringJobs,
    isLoading,
    refetch,
  } = useGetRecurringJobsQuery(undefined, {
    pollingInterval: RECURRING_JOB_POLLING_MS,
  })
  const { handleRemoveRecurring } = useBackgroundJobActions()

  const columns = useMemo<ColumnDef<RecurringJobResponse, any>[]>(
    () => [
      createActionsColumn<RecurringJobResponse>({
        unavailable: !canDelete,
        ariaLabel: 'Recurring job actions',
        getItems: (job) => {
          const items: ItemType[] = [
            {
              key: 'remove',
              label: 'Remove',
              danger: true,
              onClick: () => handleRemoveRecurring(job.id),
            },
          ]
          return items
        },
      }),
      { id: 'id', accessorKey: 'id', header: 'Job Id', size: 220 },
      { id: 'cron', accessorKey: 'cron', header: 'Cron', size: 140 },
      {
        id: 'action',
        accessorKey: 'action',
        header: 'Method',
        size: 200,
        meta: { filterType: 'set' },
      },
      {
        id: 'nextExecution',
        accessorKey: 'nextExecution',
        header: 'Next Run',
        meta: { columnType: 'dateTime' },
      },
      {
        id: 'lastExecution',
        accessorKey: 'lastExecution',
        header: 'Last Run',
        meta: { columnType: 'dateTime' },
      },
      {
        id: 'lastJobState',
        accessorKey: 'lastJobState',
        header: 'Last Result',
        size: 130,
        meta: { filterType: 'set' },
      },
      {
        id: 'queue',
        accessorKey: 'queue',
        header: 'Queue',
        size: 120,
        meta: { filterType: 'set' },
      },
      {
        id: 'error',
        accessorKey: 'error',
        header: 'Error',
        size: 260,
        // Set when the stored cron or invocation data no longer resolves —
        // the schedule exists but cannot run until it is removed and recreated.
        cell: ({ row }) =>
          row.original.error ? (
            <Typography.Text type="danger">{row.original.error}</Typography.Text>
          ) : null,
      },
    ],
    [canDelete, handleRemoveRecurring],
  )

  return (
    <WaydGrid
      columns={columns}
      data={recurringJobs}
      onRefresh={refetch}
      isLoading={isLoading}
      persistStateKey="settings-background-jobs-recurring"
      csvFileName="recurring-jobs"
      initialSorting={[{ id: 'id', desc: false }]}
      emptyMessage="No recurring jobs are registered."
    />
  )
}

export default RecurringJobsTab
