'use client'

import { PageTitle } from '@/src/components/common'
import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { IncidentSummaryDto } from '@/src/services/wayd-api'
import { useGetIncidentsQuery } from '@/src/store/features/product-management/incidents-api'
import { Button, Table } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import Link from 'next/link'
import { FC, useEffect, useMemo, useState } from 'react'
import { CreateIncidentForm } from './_components/create-incident-form'
import { formatInstantInZone } from '@/src/utils'

const IncidentsPage: FC = () => {
  useDocumentTitle('Incidents')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [openCreateForm, setOpenCreateForm] = useState(false)

  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.Incidents.Create')

  const viewerZone = useMemo(
    () => Intl.DateTimeFormat().resolvedOptions().timeZone,
    [],
  )

  const { data, isLoading, error, refetch } = useGetIncidentsQuery({
    page,
    pageSize,
  })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load incidents.')
    }
  }, [error, messageApi])

  const columns: ColumnsType<IncidentSummaryDto> = [
    {
      title: 'Key',
      dataIndex: 'key',
      width: 90,
      render: (key: number) => (
        <Link href={`/product-management/incidents/${key}`}>{key}</Link>
      ),
    },
    { title: 'Title', dataIndex: 'title' },
    {
      title: 'Severity',
      render: (_, row) => row.severityLevel?.name,
      width: 120,
    },
    {
      title: 'Commander',
      render: (_, row) => row.commander?.name,
      width: 160,
    },
    {
      title: 'Status',
      width: 140,
      render: (_, row) =>
        row.status ? (
          <WorkflowStatusTag
            name={row.status.name}
            category={row.status.category}
          />
        ) : null,
    },
    {
      title: 'Noticed',
      width: 180,
      render: (_, row) => formatInstantInZone(row.noticedAt, viewerZone),
    },
  ]

  const actions = canCreate ? (
    <Button onClick={() => setOpenCreateForm(true)}>Open Incident</Button>
  ) : null

  return (
    <div className="page-gutters">
      <PageTitle title="Incidents" actions={actions} />
      <Table
        rowKey="id"
        loading={isLoading}
        columns={columns}
        dataSource={data?.items ?? []}
        pagination={{
          current: page,
          pageSize,
          total: data?.totalCount ?? 0,
          showSizeChanger: true,
          onChange: (nextPage, nextSize) => {
            setPage(nextPage)
            setPageSize(nextSize)
          },
        }}
      />
      {openCreateForm && (
        <CreateIncidentForm
          onFormComplete={() => {
            setOpenCreateForm(false)
            refetch()
          }}
          onFormCancel={() => setOpenCreateForm(false)}
        />
      )}
    </div>
  )
}

const IncidentsPageWithAuthorization = requireFeatureFlag(
  authorizePage(IncidentsPage, 'Permission', 'Permissions.Incidents.View'),
  'product-management',
)

export default requireFeatureFlag(
  IncidentsPageWithAuthorization,
  'incident-management',
)
