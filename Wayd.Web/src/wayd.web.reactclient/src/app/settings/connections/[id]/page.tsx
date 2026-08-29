'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import {
  useActivateConnectionMutation,
  useDeactivateConnectionMutation,
  useGetConnectionQuery,
} from '@/src/store/features/app-integration/connections-api'
import { ExportOutlined } from '@ant-design/icons'
import { Alert, Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { notFound, useRouter } from 'next/navigation'
import { use, useMemo, useState } from 'react'
import DeleteConnectionForm from '../_components/delete-connection-form'
import EditConnectionForm from '../_components/edit-connection-form'
import {
  ConnectionActionContext,
  getDetailEntry,
} from './_components/detail-registry'
import ConnectionDetailsLoading from './loading'

/** The connector's own configuration — every connector has one. */
const OVERVIEW_SECTION = 'overview'

const IdentityWrapper = ({ children }: { children: React.ReactNode }) => (
  <>{children}</>
)

const ConnectionDetailsPage = (props: { params: Promise<{ id: string }> }) => {
  const { id } = use(props.params)
  useDocumentTitle('Connection Details')

  const [openEditConnectionForm, setOpenEditConnectionForm] = useState(false)
  const [openDeleteConnectionForm, setOpenDeleteConnectionForm] =
    useState(false)
  const [extraActionItems, setExtraActionItems] = useState<ItemType[]>([])

  const router = useRouter()
  const { hasClaim } = useAuth()
  const canUpdateConnections = hasClaim(
    'Permission',
    'Permissions.Connections.Update',
  )
  const canDeleteConnections = hasClaim(
    'Permission',
    'Permissions.Connections.Delete',
  )

  const { data: connection, isLoading, refetch } = useGetConnectionQuery(id)

  const messageApi = useMessage()
  const [activateConnection, { isLoading: isActivating }] =
    useActivateConnectionMutation()
  const [deactivateConnection, { isLoading: isDeactivating }] =
    useDeactivateConnectionMutation()
  const isTogglingActive = isActivating || isDeactivating

  const onToggleActive = async () => {
    if (!connection || isTogglingActive) return
    const mutation = connection.isActive
      ? deactivateConnection
      : activateConnection
    const verb = connection.isActive ? 'deactivate' : 'activate'
    const response = await mutation(id)
    if ('error' in response && response.error) {
      messageApi.error(`Failed to ${verb} connection.`)
      console.error(response.error)
      return
    }
    messageApi.success(
      `Connection ${connection.isActive ? 'deactivated' : 'activated'}.`,
    )
  }

  const entry = useMemo(() => getDetailEntry(connection), [connection])
  const externalUrl = entry?.getExternalUrl?.(connection!)

  // Overview plus whatever the connector registers. A connector with no extra
  // sections gets no rail, which `RecordLayout` decides on its own.
  const sections: RecordSection[] = [
    { id: OVERVIEW_SECTION, label: 'Overview' },
    ...(entry?.extraSections ?? []).map((s) => ({
      id: s.key,
      label: s.label,
    })),
  ]

  const onEditConnectionFormClosed = (wasSaved: boolean) => {
    setOpenEditConnectionForm(false)
    if (wasSaved) refetch()
  }

  const onDeleteConnectionFormClosed = (wasSaved: boolean) => {
    setOpenDeleteConnectionForm(false)
    if (wasSaved) router.push('/settings/connections')
  }

  const actionsMenuItems: ItemType[] = (() => {
    const items: ItemType[] = []
    if (canUpdateConnections) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setOpenEditConnectionForm(true),
      })
      items.push({
        key: 'toggle-active',
        label: connection?.isActive ? 'Deactivate' : 'Activate',
        disabled: isTogglingActive,
        onClick: onToggleActive,
      })
    }
    if (canDeleteConnections) {
      items.push({
        key: 'delete',
        label: 'Delete',
        onClick: () => setOpenDeleteConnectionForm(true),
      })
    }
    if (extraActionItems.length > 0) {
      if (canUpdateConnections || canDeleteConnections) {
        items.push({ key: 'divider', type: 'divider' })
      }
      items.push(...extraActionItems)
    }
    return items
  })()

  if (isLoading) {
    return <ConnectionDetailsLoading />
  }

  if (!connection) {
    return notFound()
  }

  if (!entry) {
    // The backend returned a connector the frontend registry does not know —
    // usually a new connector type shipped on the API before its UI
    // registration landed. Surface that rather than rendering a blank page.
    return (
      <>
        <RecordLayout
          sections={[{ id: OVERVIEW_SECTION, label: 'Overview' }]}
          defaultSection={OVERVIEW_SECTION}
          record={{
            name: connection.name,
            parent: { label: 'Connections', href: '/settings/connections' },
            subtitle: 'Connection Details',
          }}
        >
          {() => (
            <Alert
              type="warning"
              showIcon
              title={`The "${connection.connector?.name}" connector type is not supported by this version of the UI.`}
              description="The connection exists on the server but the frontend doesn't know how to render its details. Update the app, or ask an administrator if this is unexpected."
            />
          )}
        </RecordLayout>
      </>
    )
  }

  const renderSection = (section: string) => {
    if (section === OVERVIEW_SECTION) {
      const DetailsView = entry.Details
      return <DetailsView connection={connection} />
    }
    const extra = entry.extraSections?.find((s) => s.key === section)
    return extra?.render(connection) ?? null
  }

  const Wrapper = entry.Wrapper ?? IdentityWrapper
  const EditForm = entry.EditForm ?? EditConnectionForm
  const ExtraActions = entry.ExtraActions

  const actionCtx: ConnectionActionContext = {
    connectionId: id,
    connection: connection as ConnectionDetailsDto,
    reload: refetch,
    canUpdate: canUpdateConnections,
  }

  return (
    <>
      {/*
        Outside RecordLayout: ExtraActions renders nothing and exists only to
        push menu items up via setItems, so mounting it inside a section would
        tie the connector's actions to whichever section happened to be open.
      */}
      {ExtraActions && (
        <ExtraActions ctx={actionCtx} setItems={setExtraActionItems} />
      )}
      <Wrapper connection={connection} reload={refetch}>
        <RecordLayout
          sections={sections}
          defaultSection={OVERVIEW_SECTION}
          record={{
            name: connection.name,
            parent: { label: 'Connections', href: '/settings/connections' },
            subtitle: 'Connection Details',
            tags: (
              <>
                <Tag color={connection.isActive ? 'success' : 'default'}>
                  {connection.isActive ? 'Active' : 'Inactive'}
                </Tag>
                {externalUrl && (
                  <Link
                    href={externalUrl}
                    target="_blank"
                    title={`Open in ${connection.connector?.name}`}
                  >
                    <ExportOutlined />
                  </Link>
                )}
              </>
            ),
            actions:
              actionsMenuItems.length > 0 ? (
                <PageActions actionItems={actionsMenuItems} />
              ) : undefined,
          }}
        >
          {(section) => renderSection(section)}
        </RecordLayout>
      </Wrapper>
      {openEditConnectionForm && (
        <EditForm
          id={connection.id}
          connection={connection}
          onFormUpdate={() => onEditConnectionFormClosed(true)}
          onFormCancel={() => onEditConnectionFormClosed(false)}
        />
      )}
      {openDeleteConnectionForm && (
        <DeleteConnectionForm
          connection={connection}
          onFormSave={() => onDeleteConnectionFormClosed(true)}
          onFormCancel={() => onDeleteConnectionFormClosed(false)}
        />
      )}
    </>
  )
}

const PageWithAuthorization = authorizePage(
  ConnectionDetailsPage,
  'Permission',
  'Permissions.Connections.View',
)

export default PageWithAuthorization
