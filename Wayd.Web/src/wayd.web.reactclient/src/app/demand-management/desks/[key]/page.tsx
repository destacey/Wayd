'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetIntakeDeskQuery,
  useGetIntakeDeskVisibilitiesQuery,
  useGetIntakeRequestsQuery,
  useGetNeedByBasesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Alert, Button } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { use, useState } from 'react'
import IntakeRequestsGrid from '../../_components/intake-requests-grid'
import { UnownedDeskAlert } from '../../_components/desk-status'
import DeleteIntakeDeskForm from './_components/delete-intake-desk-form'
import EditIntakeDeskMembersForm from './_components/edit-intake-desk-members-form'
import EditIntakeDeskSettingsForm from './_components/edit-intake-desk-settings-form'
import IntakeDeskFacts from './_components/intake-desk-facts'
import IntakeDeskMembers from './_components/intake-desk-members'
import IntakeDeskSettings from './_components/intake-desk-settings'
import IntakeDeskDetailsLoading from './loading'

enum DeskSection {
  Requests = 'requests',
  Members = 'members',
  Settings = 'settings',
}

type DeskDialog = 'settings' | 'members' | 'delete'

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

  const { data: desk, error, refetch } = useGetIntakeDeskQuery(key)
  const { data: visibilities } = useGetIntakeDeskVisibilitiesQuery()
  const { data: needByBases } = useGetNeedByBasesQuery()
  const requestsQuery = useGetIntakeRequestsQuery(
    { deskId: desk?.id },
    { skip: !desk || activeSection !== DeskSection.Requests },
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

  const sections: RecordSection[] = [
    { id: DeskSection.Requests, label: 'Requests' },
    { id: DeskSection.Members, label: 'Members', count: desk.members.length },
    { id: DeskSection.Settings, label: 'Settings' },
  ]

  const actionItems: ItemType[] = desk.canDelete
    ? [{ key: 'delete', label: 'Delete', onClick: () => setDialog('delete') }]
    : []

  const sectionActions =
    activeSection === DeskSection.Members && desk.canManage ? (
      <Button size="small" onClick={() => setDialog('members')}>
        Edit Members
      </Button>
    ) : activeSection === DeskSection.Settings && desk.canManage ? (
      <Button size="small" onClick={() => setDialog('settings')}>
        Edit Settings
      </Button>
    ) : undefined

  const renderSection = (section: string) => {
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
            />
          </>
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
