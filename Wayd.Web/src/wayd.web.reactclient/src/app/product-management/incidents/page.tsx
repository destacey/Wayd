'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetIncidentsQuery } from '@/src/store/features/product-management/incidents-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { CreateIncidentForm } from './_components/create-incident-form'
import IncidentsGrid from './_components/incidents-grid'

// Column filters run on the rows the grid has. 500 is the largest page the API accepts, so the
// products, releases, severity, and state filters cover the list rather than one page of 50.
const INCIDENT_LIST_PAGE_SIZE = 500

const IncidentsPage: FC = () => {
  useDocumentTitle('Incidents')
  const [openCreateForm, setOpenCreateForm] = useState(false)

  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.Incidents.Create')

  const { data, isLoading, error, refetch } = useGetIncidentsQuery({
    page: 1,
    pageSize: INCIDENT_LIST_PAGE_SIZE,
  })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load incidents.')
    }
  }, [error, messageApi])

  const actions = canCreate ? (
    <Button type="primary" onClick={() => setOpenCreateForm(true)}>
      Open incident
    </Button>
  ) : null

  return (
    <div className="page-gutters">
      <PageTitle title="Incidents" actions={actions} />
      <IncidentsGrid
        incidents={data?.items ?? []}
        isLoading={isLoading}
        refetch={refetch}
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
