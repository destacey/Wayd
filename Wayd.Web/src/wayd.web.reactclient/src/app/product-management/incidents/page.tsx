'use client'

import { PageTitle } from '@/src/components/common'
import { useGridPaging, WaydGridPager } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetIncidentsQuery } from '@/src/store/features/product-management/incidents-api'
import { useGetIncidentSeveritySetsQuery } from '@/src/store/features/product-management/incident-severity-sets-api'
import { ProductStatusAlias } from '@/src/services/wayd-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { CreateIncidentForm } from './_components/create-incident-form'
import IncidentFilterBar from './_components/incident-filter-bar'
import IncidentGridFigures from './_components/incident-grid-figures'
import IncidentsGrid from './_components/incidents-grid'

const STATE_OPTIONS = [
  { value: ProductStatusAlias.Open, label: 'Open' },
  { value: ProductStatusAlias.Responding, label: 'Responding' },
  { value: ProductStatusAlias.Resolved, label: 'Recovered' },
  { value: ProductStatusAlias.Closed, label: 'Closed' },
  { value: ProductStatusAlias.Canceled, label: 'Canceled' },
]

const IncidentsPage: FC = () => {
  useDocumentTitle('Incidents')
  const [openCreateForm, setOpenCreateForm] = useState(false)
  const [statuses, setStatuses] = useState<ProductStatusAlias[]>([])
  const [severityLevelIds, setSeverityLevelIds] = useState<string[]>([])
  const paging = useGridPaging()

  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.Incidents.Create')
  const { data: severitySets } = useGetIncidentSeveritySetsQuery(false)

  const { data, isLoading, error, refetch } = useGetIncidentsQuery({
    page: paging.page,
    pageSize: paging.pageSize,
    statuses: statuses.length > 0 ? statuses : undefined,
    severityLevelIds:
      severityLevelIds.length > 0 ? severityLevelIds : undefined,
  })

  paging.keepInRange(data?.totalPages)

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
      <IncidentFilterBar
        statuses={STATE_OPTIONS}
        selectedStatuses={statuses}
        onStatusesChange={(next) => {
          setStatuses(next)
          paging.resetPage()
        }}
        severities={(
          severitySets?.find((set) => set.isSelected)?.levels ?? []
        ).map((level) => ({
          value: level.id,
          label: level.name,
          color: level.color,
        }))}
        selectedSeverities={severityLevelIds}
        onSeveritiesChange={(next) => {
          setSeverityLevelIds(next)
          paging.resetPage()
        }}
      />
      <IncidentGridFigures
        activeCount={data?.activeCount ?? 0}
        longestActive={data?.longestActive}
        meanTimeToRecoverMilliseconds={data?.meanTimeToRecoverMilliseconds}
        loading={isLoading}
      />
      <IncidentsGrid
        incidents={data?.items ?? []}
        isLoading={isLoading}
        refetch={refetch}
        rightSlot={
          <WaydGridPager
            paging={paging}
            totalCount={data?.totalCount}
            noun="incidents"
          />
        }
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
