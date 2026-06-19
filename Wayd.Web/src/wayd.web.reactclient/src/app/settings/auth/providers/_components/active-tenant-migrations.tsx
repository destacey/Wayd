'use client'

import { WaydGrid } from '@/src/components/common'
import { PendingTenantMigrationDto } from '@/src/services/wayd-api'
import { useGetPendingTenantMigrationsQuery } from '@/src/store/features/user-management/oidc-providers-api'
import {
  ColDef,
  ICellRendererParams,
  ValueFormatterParams,
} from 'ag-grid-community'
import Link from 'next/link'
import dayjs from 'dayjs'

const formatDate = (
  params: ValueFormatterParams<PendingTenantMigrationDto, Date | undefined>,
) => (params.value ? dayjs(params.value).format('MMM D, YYYY h:mm A') : '—')

const columnDefs: ColDef<PendingTenantMigrationDto>[] = [
  { field: 'userId', hide: true },
  {
    field: 'userName',
    headerName: 'User',
    cellRenderer: (params: ICellRendererParams<PendingTenantMigrationDto>) => {
      if (!params.data) return null
      return (
        <Link href={`/settings/user-management/users/${params.data.userId}`}>
          {params.data.userName}
        </Link>
      )
    },
  },
  {
    field: 'email',
    headerName: 'Email',
    width: 175,
    valueFormatter: (params) => params.value ?? '—',
  },
  {
    field: 'sourceTenantId',
    headerName: 'Source tenant',
    width: 175,
    valueFormatter: (params) => params.value ?? '—',
  },
  {
    field: 'targetTenantId',
    headerName: 'Target tenant',
    width: 175,
  },
  {
    field: 'stagedAt',
    headerName: 'Staged',
    valueFormatter: formatDate,
  },
]

interface ActiveTenantMigrationsProps {
  providerId: string
}

const ActiveTenantMigrations = ({
  providerId,
}: ActiveTenantMigrationsProps) => {
  const { data, isLoading, refetch } =
    useGetPendingTenantMigrationsQuery(providerId)

  return (
    <WaydGrid
      height={500}
      columnDefs={columnDefs}
      rowData={data}
      loading={isLoading}
      loadData={() => {
        refetch()
      }}
      emptyMessage="No tenant migrations are currently in progress."
    />
  )
}

export default ActiveTenantMigrations
