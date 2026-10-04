'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetStrategicInitiativesQuery } from '@/src/store/features/ppm/strategic-initiatives-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { CreateStrategicInitiativeForm } from './_components'
import {
  StrategicInitiativesFilterBar,
  StrategicInitiativesGrid,
} from '../_components'
import { useMessage } from '@/src/components/contexts/messaging'
import { StrategicInitiativeStatus } from '@/src/services/wayd-api'

const DEFAULT_STATUSES = [
  StrategicInitiativeStatus.Approved,
  StrategicInitiativeStatus.Active,
  StrategicInitiativeStatus.OnHold,
]

const StrategicInitiativesPage: FC = () => {
  useDocumentTitle('Strategic Initiatives')
  const [
    openCreateStrategicInitiativeForm,
    setOpenCreateStrategicInitiativeForm,
  ] = useState<boolean>(false)
  const [selectedStatuses, setSelectedStatuses] =
    useState<StrategicInitiativeStatus[]>(DEFAULT_STATUSES)
  const [selectedPortfolioId, setSelectedPortfolioId] = useState<
    string | null
  >(null)

  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreateStrategicInitiative = hasPermissionClaim(
    'Permissions.StrategicInitiatives.Create',
  )
  const showActions = canCreateStrategicInitiative

  const {
    data: strategicInitiativeData,
    isLoading,
    error,
    refetch,
  } = useGetStrategicInitiativesQuery({
    status: selectedStatuses.length > 0 ? selectedStatuses : undefined,
    portfolioId: selectedPortfolioId ?? undefined,
  })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load strategic initiatives.')
    }
  }, [error, messageApi])

  const handleResetFilters = () => {
    setSelectedStatuses(DEFAULT_STATUSES)
    setSelectedPortfolioId(null)
  }

  const handleStatusChange = (statuses: StrategicInitiativeStatus[]) => {
    setSelectedStatuses(statuses)
  }

  const actions = !showActions ? null : (
      <>
        {canCreateStrategicInitiative && (
          <Button onClick={() => setOpenCreateStrategicInitiativeForm(true)}>
            Create Strategic Initiative
          </Button>
        )}
      </>
    )

  const onCreateStrategicInitiativeFormClosed = (wasCreated: boolean) => {
    setOpenCreateStrategicInitiativeForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Strategic Initiatives" actions={actions} />
      <StrategicInitiativesFilterBar
        selectedStatuses={selectedStatuses}
        onStatusChange={handleStatusChange}
        selectedPortfolioId={selectedPortfolioId}
        onPortfolioChange={setSelectedPortfolioId}
        onReset={handleResetFilters}
      />
      <StrategicInitiativesGrid
        strategicInitiatives={strategicInitiativeData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="ppm-strategic-initiatives"
      />
      {openCreateStrategicInitiativeForm && (
        <CreateStrategicInitiativeForm
          onFormComplete={() => onCreateStrategicInitiativeFormClosed(true)}
          onFormCancel={() => onCreateStrategicInitiativeFormClosed(false)}
        />
      )}
    </div>
  )
}

const StrategicInitiativesPageWithAuthorization = authorizePage(
  StrategicInitiativesPage,
  'Permission',
  'Permissions.StrategicInitiatives.View',
)

export default StrategicInitiativesPageWithAuthorization
