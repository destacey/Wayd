'use client'

import { PageTitle } from '@/src/components/common'
import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { IncidentSummaryDto } from '@/src/services/wayd-api'
import { useGetIncidentsQuery } from '@/src/store/features/product-management/incidents-api'
import { Button, Pagination, Spin } from 'antd'
import Link from 'next/link'
import { FC, useEffect, useMemo, useState } from 'react'
import { CreateIncidentForm } from './_components/create-incident-form'
import {
  formatIncidentAge,
  formatIncidentProductLabel,
} from './_components/incident-format'
import { IncidentSeverityRank } from './_components/incident-severity-rank'
import styles from './_components/incidents.module.css'

const IncidentsPage: FC = () => {
  useDocumentTitle('Incidents')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [openCreateForm, setOpenCreateForm] = useState(false)

  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.Incidents.Create')

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

  const items = data?.items ?? []
  const total = data?.totalCount ?? 0

  const actions = canCreate ? (
    <Button type="primary" onClick={() => setOpenCreateForm(true)}>
      Open incident
    </Button>
  ) : null

  return (
    <div className="page-gutters">
      <PageTitle title="Incidents" actions={actions} />
      <div className={styles.sheet}>
        <div className={styles.listHead}>
          <span>Severity</span>
          <span>Title</span>
          <span>Products</span>
          <span>State</span>
          <span>Age</span>
          <span>Commander</span>
        </div>
        {isLoading ? (
          <div style={{ padding: 24, textAlign: 'center' }}>
            <Spin />
          </div>
        ) : items.length === 0 ? (
          <div style={{ padding: 16, color: 'var(--ant-color-text-secondary)' }}>
            No incidents yet.
          </div>
        ) : (
          items.map((row) => <IncidentListRow key={row.id} row={row} />)
        )}
      </div>
      {total > pageSize ? (
        <div className={styles.pagination}>
          <Pagination
            current={page}
            pageSize={pageSize}
            total={total}
            showSizeChanger
            onChange={(nextPage, nextSize) => {
              setPage(nextPage)
              setPageSize(nextSize)
            }}
          />
        </div>
      ) : null}
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

function IncidentListRow({ row }: { row: IncidentSummaryDto }) {
  const age = useMemo(
    () => formatIncidentAge(row.noticedAt),
    [row.noticedAt],
  )

  return (
    <Link
      className={styles.incidentRow}
      href={`/product-management/incidents/${row.key}`}
    >
      <IncidentSeverityRank
        name={row.severityLevel.name}
        color={row.severityLevel.color}
      />
      <span className={styles.titleCell}>
        <strong>{row.title}</strong>
        <span className={styles.id}>INC-{row.key}</span>
      </span>
      <span>{formatIncidentProductLabel(row.products ?? [])}</span>
      <span>
        {row.status ? (
          <WorkflowStatusTag
            name={row.status.name}
            category={row.status.category}
          />
        ) : null}
      </span>
      <span>{age}</span>
      <span>{row.commander?.name ?? '—'}</span>
    </Link>
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
