'use client'

import PageTitle from '@/src/components/common/page-title'
import { useState } from 'react'
import { useDocumentTitle } from '@/src/hooks'
import {
  CreatePokerSessionForm,
  DeletePokerSessionForm,
  PokerSessionsGrid,
} from './_components'
import EditPokerSessionForm from '@/src/app/planning/poker-sessions/[key]/_components/edit-poker-session-form'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { Button } from 'antd'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { ControlItemSwitch } from '@/src/components/common/control-items-menu'
import { ItemType } from 'antd/es/menu/interface'
import {
  PokerSessionListDto,
  PokerSessionStatus,
} from '@/src/services/wayd-api'
import {
  useGetPokerSessionsQuery,
  useCompletePokerSessionMutation,
} from '@/src/store/features/planning/poker-sessions-api'

const PokerSessionsPage = () => {
  useDocumentTitle('Planning Poker')

  const [openCreateForm, setOpenCreateForm] = useState(false)
  const [includeCompleted, setIncludeCompleted] = useState(false)
  const [editSessionKey, setEditSessionKey] = useState<number | null>(null)
  const [deleteSession, setDeleteSession] =
    useState<PokerSessionListDto | null>(null)

  const messageApi = useMessage()

  const {
    data: sessionsData,
    isLoading,
    refetch,
  } = useGetPokerSessionsQuery(
    includeCompleted ? undefined : PokerSessionStatus.Active,
  )

  const [completeSession] = useCompletePokerSessionMutation()

  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.PokerSessions.Create')
  const canUpdate = hasPermissionClaim('Permissions.PokerSessions.Update')
  const canDelete = hasPermissionClaim('Permissions.PokerSessions.Delete')

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Completed"
          checked={includeCompleted}
          onChange={setIncludeCompleted}
        />
      ),
      key: 'include-completed',
      onClick: () => setIncludeCompleted((prev) => !prev),
    },
  ]

  const refresh = () => {
    refetch()
  }

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  const handleEdit = (session: PokerSessionListDto) => {
    setEditSessionKey(session.key)
  }

  const handleComplete = async (session: PokerSessionListDto) => {
    try {
      const response = await completeSession({
        id: session.id,
        key: session.key,
      })
      if (response.error) throw response.error
      messageApi.success('Session completed.')
    } catch {
      messageApi.error('Failed to complete session.')
    }
  }

  const handleDelete = (session: PokerSessionListDto) => {
    setDeleteSession(session)
  }

  const onEditFormClosed = () => {
    setEditSessionKey(null)
  }

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setDeleteSession(null)
    if (wasDeleted) {
      refetch()
    }
  }

  const actions = () => (
    <>
      {canCreate && (
        <Button onClick={() => setOpenCreateForm(true)}>Create Session</Button>
      )}
    </>
  )

  return (
    <div className="page-gutters">
      <PageTitle title="Planning Poker" actions={canCreate && actions()} />
      <PokerSessionsGrid
        sessions={sessionsData ?? []}
        isLoading={isLoading}
        refetch={refresh}
        canUpdate={canUpdate}
        canDelete={canDelete}
        gridControlMenuItems={controlItems}
        onEditClicked={handleEdit}
        onCompleteClicked={handleComplete}
        onDeleteClicked={handleDelete}
      />
      {openCreateForm && (
        <CreatePokerSessionForm
          onFormCreate={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
      {editSessionKey && (
        <EditPokerSessionForm
          sessionKey={editSessionKey}
          onFormUpdate={() => onEditFormClosed()}
          onFormCancel={() => onEditFormClosed()}
        />
      )}
      {deleteSession && (
        <DeletePokerSessionForm
          session={deleteSession}
          onFormComplete={() => onDeleteFormClosed(true)}
          onFormCancel={() => onDeleteFormClosed(false)}
        />
      )}
    </div>
  )
}

const PokerSessionsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    PokerSessionsPage,
    'Permission',
    'Permissions.PokerSessions.View',
  ),
  'planning-poker',
)

export default PokerSessionsPageWithAuthorization
