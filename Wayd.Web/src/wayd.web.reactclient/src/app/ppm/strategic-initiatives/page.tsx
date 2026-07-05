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

// Strategic Initiative status enum values matching the backend
const SI_STATUS = {
  Proposed: 1,
  Approved: 2,
  Active: 3,
  OnHold: 4,
  Completed: 5,
  Cancelled: 6,
} as const

const DEFAULT_STATUSES = [SI_STATUS.Approved, SI_STATUS.Active, SI_STATUS.OnHold]

const StrategicInitiativesPage: FC = () => {
  useDocumentTitle('Strategic Initiatives')
  const [
    openCreateStrategicInitiativeForm,
    setOpenCreateStrategicInitiativeForm,
  ] = useState<boolean>(false)
  const [selectedStatuses, setSelectedStatuses] =
    useState<number[]>(DEFAULT_STATUSES)
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

  const handleStatusChange = (statuses: number[]) => {
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
    <>
      <PageTitle title="Strategic Initiatives" actions={actions} />
      <StrategicInitiativesFilterBar
        selectedStatuses={selectedStatuses}
        onStatusChange={handleStatusChange}
        selectedPortfolioId={selectedPortfolioId}
        onPortfolioChange={setSelectedPortfolioId}
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
    </>
  )
}

const StrategicInitiativesPageWithAuthorization = authorizePage(
  StrategicInitiativesPage,
  'Permission',
  'Permissions.StrategicInitiatives.View',
)

export default StrategicInitiativesPageWithAuthorization
