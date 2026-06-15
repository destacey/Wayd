'use client'

import { WaydGrid } from '@/src/components/common'
import { PendingTenantMigrationDto } from '@/src/services/wayd-api'
import { useGetPendingTenantMigrationsQuery } from '@/src/store/features/user-management/oidc-providers-api'
import { ColDef, ICellRendererParams, ValueFormatterParams } from 'ag-grid-community'
import { Space, Typography } from 'antd'
import dayjs from 'dayjs'

const { Text } = Typography

const formatDate = (
  params: ValueFormatterParams<PendingTenantMigrationDto, Date | undefined>,
) => (params.value ? dayjs(params.value).format('MMM D, YYYY h:mm A') : '—')

const columnDefs: ColDef<PendingTenantMigrationDto>[] = [
  { field: 'userId', hide: true },
  {
    field: 'userName',
    headerName: 'User',
    flex: 1,
    cellRenderer: (params: ICellRendererParams<PendingTenantMigrationDto>) => {
      if (!params.data) return null
      return (
        <Space orientation="vertical" size={0}>
          <Text>{params.data.userName}</Text>
          {params.data.email && (
            <Text type="secondary">{params.data.email}</Text>
          )}
        </Space>
      )
    },
  },
  {
    field: 'sourceTenantId',
    headerName: 'Source tenant',
    flex: 1,
    valueFormatter: (params) => params.value ?? '—',
  },
  {
    field: 'targetTenantId',
    headerName: 'Target tenant',
    flex: 1,
  },
  {
    field: 'stagedAt',
    headerName: 'Staged',
    width: 220,
    valueFormatter: formatDate,
  },
]

interface ActiveTenantMigrationsProps {
  providerId: string
}

const ActiveTenantMigrations = ({ providerId }: ActiveTenantMigrationsProps) => {
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
