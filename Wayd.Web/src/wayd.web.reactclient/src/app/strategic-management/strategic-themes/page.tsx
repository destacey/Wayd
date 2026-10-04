'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetStrategicThemesQuery } from '@/src/store/features/strategic-management/strategic-themes-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import {
  CreateStrategicThemeForm,
  StrategicThemesFilterBar,
  StrategicThemesGrid,
} from './_components'
import { useMessage } from '@/src/components/contexts/messaging'
import { StrategicThemeState } from '@/src/services/wayd-api'

const DEFAULT_STATES = [StrategicThemeState.Active]

const StrategicThemesPage: FC = () => {
  useDocumentTitle('Strategic Themes')

  const [openCreateStrategicThemeForm, setOpenCreateStrategicThemeForm] =
    useState<boolean>(false)
  const [selectedStates, setSelectedStates] =
    useState<StrategicThemeState[]>(DEFAULT_STATES)

  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreateStrategicTheme = hasPermissionClaim(
    'Permissions.StrategicThemes.Create',
  )
  const showActions = canCreateStrategicTheme

  const {
    data: strategicThemesData,
    isLoading,
    error,
    refetch,
  } = useGetStrategicThemesQuery({
    state: selectedStates.length > 0 ? selectedStates : undefined,
  })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load strategic themes.')
    }
  }, [error, messageApi])

  const handleStateChange = (states: StrategicThemeState[]) => {
    setSelectedStates(states)
  }

  const refresh = async () => {
    refetch()
  }

  const actions = (
    <>
      {canCreateStrategicTheme && (
        <Button onClick={() => setOpenCreateStrategicThemeForm(true)}>
          Create Strategic Theme
        </Button>
      )}
    </>
  )

  return (
    <div className="page-gutters">
      <PageTitle title="Strategic Themes" actions={showActions && actions} />
      <StrategicThemesFilterBar
        selectedStates={selectedStates}
        onStateChange={handleStateChange}
      />
      <StrategicThemesGrid
        strategicThemesData={strategicThemesData || []}
        strategicThemesLoading={isLoading}
        refreshStrategicThemes={refresh}
      />
      {openCreateStrategicThemeForm && (
        <CreateStrategicThemeForm
          onFormComplete={() => setOpenCreateStrategicThemeForm(false)}
          onFormCancel={() => setOpenCreateStrategicThemeForm(false)}
        />
      )}
    </div>
  )
}

const StrategicThemesPageWithAuthorization = authorizePage(
  StrategicThemesPage,
  'Permission',
  'Permissions.StrategicThemes.View',
)

export default StrategicThemesPageWithAuthorization
