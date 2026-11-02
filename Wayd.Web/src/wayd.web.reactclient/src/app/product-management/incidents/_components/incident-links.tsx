'use client'

import ManageIncidentWorkItemsForm from '@/src/app/product-management/incidents/_components/manage-incident-work-items-form'
import CreateLinkForm from '@/src/components/common/links/create-link-form'
import { renderWorkItemLink } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { LinkDto } from '@/src/services/wayd-api'
import {
  StoreDeleteLinkRequest,
  useDeleteLinkMutation,
  useGetLinksQuery,
} from '@/src/store/features/common/links-api'
import { useGetIncidentWorkItemsQuery } from '@/src/store/features/product-management/incidents-api'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Modal, Popconfirm } from 'antd'
import Link from 'next/link'
import { useMemo, useState } from 'react'
import styles from '@/src/app/product-management/incidents/_components/incidents.module.css'

export interface IncidentLinksProps {
  incidentId: string
  idOrKey: string
  canManageIncident: boolean
}

type AddLinkStep = 'closed' | 'choose' | 'work-items' | 'generic'

const externalHref = (url: string) =>
  url.match(/^.{3,5}\:\/\//) ? url : `//${url}`

const GenericLinkRow = ({
  link,
  canDelete,
}: {
  link: LinkDto
  canDelete: boolean
}) => {
  const messageApi = useMessage()
  const [deleteLink, { isLoading: isDeleting }] = useDeleteLinkMutation()

  const handleDelete = async () => {
    try {
      const storeRequest: StoreDeleteLinkRequest = {
        id: link.id,
        objectId: link.objectId,
      }
      const response = await deleteLink(storeRequest)
      if (response.error) {
        throw response.error
      }
      messageApi.success('Link removed.')
    } catch (error) {
      messageApi.error('Failed to remove link.')
      console.error(error)
    }
  }

  return (
    <div className={styles.linkRow}>
      <Link
        href={externalHref(link.url)}
        target="_blank"
        prefetch={false}
        className={styles.externalLink}
        title={link.name}
      >
        {link.name}
      </Link>
      {canDelete ? (
        <Popconfirm
          title="Remove this link?"
          onConfirm={() => void handleDelete()}
          okButtonProps={{ loading: isDeleting }}
          okText="Remove"
          cancelText="Cancel"
        >
          <Button
            type="text"
            size="small"
            aria-label={`Remove ${link.name}`}
            icon={<DeleteOutlined />}
          />
        </Popconfirm>
      ) : null}
    </div>
  )
}

const IncidentLinks = ({
  incidentId,
  idOrKey,
  canManageIncident,
}: IncidentLinksProps) => {
  const { hasPermissionClaim } = useAuth()
  const canCreateGenericLinks = hasPermissionClaim('Permissions.Links.Create')
  const canDeleteGenericLinks = hasPermissionClaim('Permissions.Links.Delete')
  const canViewGenericLinks = hasPermissionClaim('Permissions.Links.View')

  const [addStep, setAddStep] = useState<AddLinkStep>('closed')

  const { data: workItemsData, refetch: refetchWorkItems } =
    useGetIncidentWorkItemsQuery({ idOrKey, incidentId })

  const {
    data: genericLinks,
    refetch: refetchGenericLinks,
    isError: genericLinksError,
  } = useGetLinksQuery(incidentId, {
    skip: !incidentId || !canViewGenericLinks,
  })

  const workItems = workItemsData?.workItems ?? []
  const sortedGenericLinks = useMemo(
    () =>
      [...(genericLinks ?? [])].sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }),
      ),
    [genericLinks],
  )

  const hasAnyLinks = workItems.length > 0 || sortedGenericLinks.length > 0
  const canAddLinks = canManageIncident || canCreateGenericLinks

  const closeAddFlow = () => setAddStep('closed')

  const onWorkItemsSaved = () => {
    closeAddFlow()
    void refetchWorkItems()
  }

  const onGenericLinkCreated = () => {
    closeAddFlow()
    void refetchGenericLinks()
  }

  return (
    <>
      <div className={styles.linksHeadingRow}>
        <h2 className={styles.asideHeading}>Links</h2>
        {canAddLinks ? (
          <Button
            type="default"
            size="small"
            icon={<PlusOutlined />}
            onClick={() => setAddStep('choose')}
          >
            Add link
          </Button>
        ) : null}
      </div>
      <div className={styles.linkList}>
        {!hasAnyLinks ? (
          <span className={styles.linkMuted}>None linked</span>
        ) : null}
        {workItems.map((item) => (
          <div key={item.id} className={styles.linkRow}>
            {renderWorkItemLink({
              key: item.key,
              workspaceKey: item.workspace.key,
              externalViewWorkItemUrl: item.externalViewWorkItemUrl,
              label: item.title,
            })}
          </div>
        ))}
        {genericLinksError ? (
          <span className={styles.linkMuted}>Could not load external links</span>
        ) : (
          sortedGenericLinks.map((link) => (
            <GenericLinkRow
              key={link.id}
              link={link}
              canDelete={canDeleteGenericLinks}
            />
          ))
        )}
      </div>

      <Modal
        title="Add link"
        open={addStep === 'choose'}
        onCancel={closeAddFlow}
        footer={null}
        destroyOnHidden
      >
        <div className={styles.addLinkChoices}>
          <Button
            block
            disabled={!canManageIncident}
            onClick={() => setAddStep('work-items')}
          >
            Wayd work item
          </Button>
          <Button
            block
            disabled={!canCreateGenericLinks}
            onClick={() => setAddStep('generic')}
          >
            Generic link
          </Button>
          {!canManageIncident && !canCreateGenericLinks ? (
            <p className={styles.linkMuted}>
              You do not have permission to add links here.
            </p>
          ) : null}
        </div>
      </Modal>

      {addStep === 'work-items' ? (
        <ManageIncidentWorkItemsForm
          incidentId={incidentId}
          idOrKey={idOrKey}
          onFormComplete={onWorkItemsSaved}
          onFormCancel={closeAddFlow}
        />
      ) : null}
      {addStep === 'generic' ? (
        <CreateLinkForm
          objectId={incidentId}
          onFormCreate={onGenericLinkCreated}
          onFormCancel={closeAddFlow}
        />
      ) : null}
    </>
  )
}

export default IncidentLinks
