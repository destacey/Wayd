'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import { useMemo } from 'react'
import type { ColumnDef } from '../../../../components/common/wayd-grid-core'
import { JobServerResponse } from '@/src/services/wayd-api'
import {
  JOB_SERVER_POLLING_MS,
  useGetJobServersQuery,
} from '@/src/store/features/admin/background-jobs-api'

const JobServersTab = () => {
  const {
    data: servers,
    isLoading,
    refetch,
  } = useGetJobServersQuery(undefined, {
    pollingInterval: JOB_SERVER_POLLING_MS,
  })

  const columns = useMemo<ColumnDef<JobServerResponse, any>[]>(
    () => [
      { id: 'name', accessorKey: 'name', header: 'Server', size: 280 },
      {
        id: 'workerCount',
        accessorKey: 'workerCount',
        header: 'Workers',
        size: 100,
      },
      {
        id: 'queues',
        accessorFn: (row) => row.queues?.join(', '),
        header: 'Queues',
        size: 200,
      },
      {
        id: 'heartbeat',
        accessorKey: 'heartbeat',
        // A stale heartbeat is how a wedged or stopped worker shows up.
        header: 'Last Heartbeat',
        meta: { columnType: 'dateTime' },
      },
      {
        id: 'startedAt',
        accessorKey: 'startedAt',
        header: 'Started',
        meta: { columnType: 'dateTime' },
      },
    ],
    [],
  )

  return (
    <WaydGrid
      columns={columns}
      data={servers}
      onRefresh={refetch}
      isLoading={isLoading}
      persistStateKey="settings-background-jobs-servers"
      csvFileName="job-servers"
      initialSorting={[{ id: 'name', desc: false }]}
      emptyMessage="No job servers are running."
    />
  )
}

export default JobServersTab
