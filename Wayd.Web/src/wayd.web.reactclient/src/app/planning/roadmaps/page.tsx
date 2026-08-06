'use client'

import PageTitle from '@/src/components/common/page-title'
import { FC, useEffect, useState } from 'react'
import { useDocumentTitle } from '../../../hooks/use-document-title'
import useAuth from '../../../components/contexts/auth'
import { Alert, Button } from 'antd'
import { authorizePage } from '../../../components/hoc'
import {
  ROADMAP_STATE,
  useGetRoadmapsQuery,
} from '@/src/store/features/planning/roadmaps-api'
import {
  CreateRoadmapForm,
  RoadmapsFilterBar,
  RoadmapsGrid,
} from './_components'
import { useMessage } from '@/src/components/contexts/messaging'
import { useLinkedEmployee } from '@/src/hooks'

const DEFAULT_STATES = [ROADMAP_STATE.Active]

const RoadmapsPage: FC = () => {
  useDocumentTitle('Roadmaps')

  const [openCreateRoadmapForm, setOpenCreateRoadmapForm] =
    useState<boolean>(false)
  const [selectedStates, setSelectedStates] = useState<number[]>(DEFAULT_STATES)

  const messageApi = useMessage()

  const {
    data: roadmapData,
    isLoading,
    error,
    refetch,
  } = useGetRoadmapsQuery({
    state: selectedStates.length > 0 ? selectedStates : undefined,
  })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load roadmaps.')
    }
  }, [error, messageApi])

  const { hasPermissionClaim } = useAuth()
  const { hasLinkedEmployee } = useLinkedEmployee()

  // Creating a roadmap records the creator as its manager, so it needs an employee link as well as
  // the permission — the API rejects an unlinked account with 403. Offering the button anyway would
  // walk the user into a form they cannot submit.
  const hasCreateRoadmapPermission = hasPermissionClaim(
    'Permissions.Roadmaps.Create',
  )
  const canCreateRoadmap = hasCreateRoadmapPermission && hasLinkedEmployee
  const showActions = canCreateRoadmap

  const handleStateChange = (states: number[]) => {
    setSelectedStates(states)
  }

  const refresh = async () => {
    refetch()
  }

  const onCreateRoadmapFormClosed = (wasCreated: boolean) => {
    setOpenCreateRoadmapForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  const actions = () => {
    return (
      <>
        {canCreateRoadmap && (
          <Button onClick={() => setOpenCreateRoadmapForm(true)}>
            Create Roadmap
          </Button>
        )}
      </>
    )
  }

  return (
    <>
      <PageTitle title="Roadmaps" actions={showActions && actions()} />
      {hasCreateRoadmapPermission && !hasLinkedEmployee && (
        <Alert
          title="Your account isn't linked to an employee record"
          description="Creating a roadmap requires a linked employee record, so that action is unavailable. Ask an administrator to link your account. You can still view roadmaps that are shared publicly."
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
        />
      )}
      <RoadmapsFilterBar
        selectedStates={selectedStates}
        onStateChange={handleStateChange}
      />
      <RoadmapsGrid
        roadmapsData={roadmapData || []}
        roadmapsLoading={isLoading}
        refreshRoadmaps={refresh}
      />
      {openCreateRoadmapForm && (
        <CreateRoadmapForm
          onFormComplete={() => onCreateRoadmapFormClosed(true)}
          onFormCancel={() => onCreateRoadmapFormClosed(false)}
        />
      )}
    </>
  )
}

const RoadmapsPageWithAuthorization = authorizePage(
  RoadmapsPage,
  'Permission',
  'Permissions.Roadmaps.View',
)

export default RoadmapsPageWithAuthorization
