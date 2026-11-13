'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { requireFeatureFlag } from '@/src/components/hoc'
import {
  useDocumentTitle,
  useFeatureFlag,
  useLinkedEmployee,
} from '@/src/hooks'
import {
  useGetIntakeDeskQuery,
  useGetIntakeDeskVisibilitiesQuery,
  useGetIntakeRequestsQuery,
  useGetNeedByBasesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Alert, Button, Space } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import type { RowSelectionState } from '@tanstack/react-table'
import { use, useState } from 'react'
import {
  canRunDeskPoker,
  POKER_VIEW_PERMISSION,
  pokerSessionHref,
} from '../../_components/desk-poker'
import DeskPokerSessionForm from '../../_components/desk-poker-session-form'
import StartPokerSessionButton from '../../_components/start-poker-session-button'
import IntakeRequestsGrid from '../../_components/intake-requests-grid'
import { UnownedDeskAlert } from '../../_components/desk-status'
import DeleteIntakeDeskForm from './_components/delete-intake-desk-form'
import EditIntakeDeskMembersForm from './_components/edit-intake-desk-members-form'
import EditIntakeDeskSettingsForm from './_components/edit-intake-desk-settings-form'
import EditIntakeDeskTriagersForm from './_components/edit-intake-desk-triagers-form'
import IntakeDeskFacts from './_components/intake-desk-facts'
import IntakeDeskMembers from './_components/intake-desk-members'
import IntakeDeskPokerSessions from './_components/intake-desk-poker-sessions'
import IntakeDeskSettings from './_components/intake-desk-settings'
import IntakeDeskDetailsLoading from './loading'

enum DeskSection {
  Requests = 'requests',
  PokerSessions = 'poker-sessions',
  Members = 'members',
  Settings = 'settings',
}

type DeskDialog = 'settings' | 'members' | 'triagers' | 'delete' | 'poker'

/**
 * One desk, run from its own page rather than from Settings: a Desk Admin usually holds no administer
 * permission. Every action comes from the desk's own flags, so the page offers exactly what the server
 * would accept.
 */
const IntakeDeskDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  useDocumentTitle('Desk Details')
  const router = useRouter()

  // The active section lives in the URL, owned by RecordLayout; read here only to load requests lazily.
  const searchParams = useSearchParams()
  const activeSection = searchParams.get('section') ?? DeskSection.Requests

  const [dialog, setDialog] = useState<DeskDialog | null>(null)
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({})
  const { employeeId } = useLinkedEmployee()
  const { hasPermissionClaim } = useAuth()
  const { isEnabled: pokerEnabled } = useFeatureFlag('planning-poker')
  // Sessions are Planning Poker's, so the section needs what opening one needs.
  const showPoker = pokerEnabled && hasPermissionClaim(POKER_VIEW_PERMISSION)

  const { data: desk, error, refetch } = useGetIntakeDeskQuery(key)
  const { data: visibilities } = useGetIntakeDeskVisibilitiesQuery()
  const { data: needByBases } = useGetNeedByBasesQuery()
  const requestsQuery = useGetIntakeRequestsQuery(
    { deskId: desk?.id },
    {
      // Also loaded for the poker section's address where Planning Poker is off, which shows the requests.
      skip:
        !desk ||
        (activeSection !== DeskSection.Requests &&
          (activeSection !== DeskSection.PokerSessions || showPoker)),
    },
  )

  if (error && toApiError(error).status === 404) {
    return notFound()
  }

  if (!desk) {
    if (error) {
      return (
        <div className="page-gutters">
          <Alert
            type="error"
            showIcon
            title="The desk could not be loaded."
            action={<Button onClick={() => refetch()}>Try again</Button>}
          />
        </div>
      )
    }
    return <IntakeDeskDetailsLoading />
  }

  const canStartPoker = showPoker && canRunDeskPoker(desk, employeeId)
  const selectedRequests = (requestsQuery.data ?? []).filter(
    (r) => rowSelection[r.id],
  )

  const sections: RecordSection[] = [
    { id: DeskSection.Requests, label: 'Requests' },
    ...(showPoker
      ? [{ id: DeskSection.PokerSessions, label: 'Poker Sessions' }]
      : []),
    { id: DeskSection.Members, label: 'Members', count: desk.members.length },
    { id: DeskSection.Settings, label: 'Settings' },
  ]

  const actionItems: ItemType[] = desk.canDelete
    ? [{ key: 'delete', label: 'Delete', onClick: () => setDialog('delete') }]
    : []

  const sectionActions =
    canStartPoker &&
    activeSection === DeskSection.Requests &&
    selectedRequests.length > 0 ? (
      // Says why when the selection cannot be estimated, such as a closed request.
      <StartPokerSessionButton
        selected={selectedRequests}
        desks={[desk]}
        employeeId={employeeId}
        onStarted={() => setRowSelection({})}
      />
    ) : canStartPoker &&
      (activeSection === DeskSection.PokerSessions ||
        activeSection === DeskSection.Requests) ? (
      <Button size="small" onClick={() => setDialog('poker')}>
        Start Poker Session
      </Button>
    ) : activeSection === DeskSection.Members && desk.canManage ? (
      <Space size="small">
        <Button size="small" onClick={() => setDialog('triagers')}>
          Edit Triagers
        </Button>
        <Button size="small" onClick={() => setDialog('members')}>
          Edit Members
        </Button>
      </Space>
    ) : activeSection === DeskSection.Settings && desk.canManage ? (
      <Button size="small" onClick={() => setDialog('settings')}>
        Edit Settings
      </Button>
    ) : undefined

  const renderSection = (section: string): React.ReactNode => {
    switch (section) {
      case DeskSection.Requests:
        return (
          <>
            <UnownedDeskAlert desk={desk} />
            <IntakeRequestsGrid
              requests={requestsQuery.data}
              isLoading={requestsQuery.isLoading}
              refetch={requestsQuery.refetch}
              needByBases={needByBases}
              persistStateKey="demand-management-desk-requests"
              csvFileName={`desk-${desk.key}-requests`}
              emptyMessage="No requests you can see on this desk"
              rowSelection={canStartPoker ? rowSelection : undefined}
              onRowSelectionChange={
                canStartPoker ? setRowSelection : undefined
              }
            />
          </>
        )
      case DeskSection.PokerSessions:
        // An address naming the section, opened where Planning Poker is off, shows the desk's requests.
        return showPoker ? (
          <IntakeDeskPokerSessions desk={desk} />
        ) : (
          renderSection(DeskSection.Requests)
        )
      case DeskSection.Members:
        return (
          <>
            <UnownedDeskAlert desk={desk} />
            <IntakeDeskMembers desk={desk} />
          </>
        )
      case DeskSection.Settings:
        return <IntakeDeskSettings desk={desk} visibilities={visibilities} />
      default:
        return null
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={DeskSection.Requests}
        record={{
          name: desk.name,
          recordKey: String(desk.key),
          parent: { label: 'Desks', href: '/demand-management/desks' },
          subtitle: 'Desk Details',
          actions: <PageActions actionItems={actionItems} />,
        }}
        facts={<IntakeDeskFacts desk={desk} visibilities={visibilities} />}
        sectionActions={sectionActions}
      >
        {renderSection}
      </RecordLayout>
      {dialog === 'settings' && desk.canManage && (
        <EditIntakeDeskSettingsForm
          desk={desk}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'members' && desk.canManage && (
        <EditIntakeDeskMembersForm
          desk={desk}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'triagers' && desk.canManage && (
        <EditIntakeDeskTriagersForm
          desk={desk}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'poker' && canStartPoker && (
        <DeskPokerSessionForm
          desk={desk}
          onFormComplete={(started) => {
            setDialog(null)
            if (started) router.push(pokerSessionHref(started.key))
          }}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'delete' && desk.canDelete && (
        <DeleteIntakeDeskForm
          desk={desk}
          onFormComplete={() => router.push('/demand-management/desks')}
          onFormCancel={() => setDialog(null)}
        />
      )}
    </>
  )
}

// No permission gate: what the reader may do comes from the desk's own flags.
const IntakeDeskDetailsPageWithAuthorization = requireFeatureFlag(
  IntakeDeskDetailsPage,
  'demand-management',
)

export default IntakeDeskDetailsPageWithAuthorization
