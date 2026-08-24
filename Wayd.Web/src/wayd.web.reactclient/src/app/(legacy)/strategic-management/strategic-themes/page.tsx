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

// Strategic Theme state enum values matching the backend
const THEME_STATE = {
  Proposed: 1,
  Active: 2,
  Archived: 3,
} as const

const DEFAULT_STATES = [THEME_STATE.Active]

const StrategicThemesPage: FC = () => {
  useDocumentTitle('Strategic Themes')

  const [openCreateStrategicThemeForm, setOpenCreateStrategicThemeForm] =
    useState<boolean>(false)
  const [selectedStates, setSelectedStates] = useState<number[]>(DEFAULT_STATES)

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

  const handleStateChange = (states: number[]) => {
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
    <>
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
    </>
  )
}

const StrategicThemesPageWithAuthorization = authorizePage(
  StrategicThemesPage,
  'Permission',
  'Permissions.StrategicThemes.View',
)

export default StrategicThemesPageWithAuthorization
