'use client'

import PageTitle from '@/src/components/common/page-title'
import { use, useEffect, useMemo, useState } from 'react'
import { Alert, Card } from 'antd'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { notFound, usePathname, useRouter } from 'next/navigation'
import EditConnectionForm from '../_components/edit-connection-form'
import { ExportOutlined } from '@ant-design/icons'
import Link from 'next/link'
import { useAppDispatch } from '@/src/hooks'
import { BreadcrumbItem, setBreadcrumbRoute } from '@/src/store/breadcrumbs'
import DeleteConnectionForm from '../_components/delete-connection-form'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import { PageActions } from '@/src/components/common'
import { ItemType } from 'antd/es/menu/interface'
import {
  useActivateConnectionMutation,
  useDeactivateConnectionMutation,
  useGetConnectionQuery,
} from '@/src/store/features/app-integration/connections-api'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ConnectionActionContext,
  getDetailEntry,
} from './_components/detail-registry'
import { ConnectionDetailsDto } from '@/src/services/wayd-api'

const DETAILS_TAB = 'details'

const IdentityWrapper = ({ children }: { children: React.ReactNode }) => (
  <>{children}</>
)

const ConnectionDetailsPage = (props: {
  params: Promise<{ id: string }>
}) => {
  const { id } = use(props.params)
  useDocumentTitle('Connection Details')

  const [activeTab, setActiveTab] = useState<string>(DETAILS_TAB)
  const [openEditConnectionForm, setOpenEditConnectionForm] = useState(false)
  const [openDeleteConnectionForm, setOpenDeleteConnectionForm] =
    useState(false)
  const [extraActionItems, setExtraActionItems] = useState<ItemType[]>([])

  const dispatch = useAppDispatch()
  const pathname = usePathname()
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

  const {
    data: connection,
    isLoading,
    refetch,
  } = useGetConnectionQuery(id)

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

  const tabs = useMemo(() => {
    const result = [{ key: DETAILS_TAB, tab: 'Details' }]
    for (const t of entry?.extraTabs ?? []) {
      result.push({ key: t.key, tab: t.label })
    }
    return result
  }, [entry])

  useEffect(() => {
    if (!connection) return
    const breadcrumbRoute: BreadcrumbItem[] = [
      { title: 'Settings' },
      { href: `/settings/connections`, title: 'Connections' },
      { title: connection.name },
    ]
    dispatch(setBreadcrumbRoute({ route: breadcrumbRoute, pathname }))
  }, [connection, dispatch, pathname])

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

  if (!isLoading && !connection) {
    return notFound()
  }

  if (!connection) {
    // Still loading — render nothing for the brief window before the query resolves.
    return null
  }

  if (!entry) {
    // The backend returned a connector that the frontend registry doesn't know
    // about — usually because a new connector type shipped on the API before
    // the corresponding UI registration landed. Surface the situation rather
    // than rendering a blank page.
    return (
      <>
        <BasicBreadcrumb
          items={[
            { title: 'Settings' },
            { title: 'Connections', href: '/settings/connections' },
            { title: 'Details' },
          ]}
        />
        <PageTitle
          title={connection.name}
          subtitle="Connection Details"
        />
        <Alert
          type="warning"
          showIcon
          message={`The "${connection.connector?.name}" connector type is not supported by this version of the UI.`}
          description="The connection exists on the server but the frontend doesn't know how to render its details. Update the app, or ask an administrator if this is unexpected."
        />
      </>
    )
  }

  const renderTabContent = () => {
    if (activeTab === DETAILS_TAB) {
      const DetailsView = entry.Details
      return <DetailsView connection={connection} />
    }
    const extra = entry.extraTabs?.find((t) => t.key === activeTab)
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
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'Connections', href: '/settings/connections' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={
          <>
            {connection.name}{' '}
            {externalUrl && (
              <Link
                href={externalUrl}
                target="_blank"
                title={`Open in ${connection.connector?.name}`}
              >
                <ExportOutlined style={{ width: '12px' }} />
              </Link>
            )}
          </>
        }
        subtitle="Connection Details"
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      {ExtraActions && (
        <ExtraActions ctx={actionCtx} setItems={setExtraActionItems} />
      )}
      <Wrapper connection={connection} reload={refetch}>
        <Card
          tabList={tabs}
          activeTabKey={activeTab}
          onTabChange={(k) => setActiveTab(k)}
        >
          {renderTabContent()}
        </Card>
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
