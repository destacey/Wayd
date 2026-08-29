'use client'

import { Alert, Button, Card, Flex, Spin } from 'antd'
import useAuth from '../../contexts/auth'
import { EditOutlined, EditTwoTone, PlusOutlined } from '@ant-design/icons'
import { useState } from 'react'
import CreateLinkForm from './create-link-form'
import LinkItem from './link-item'
import WaydEmpty from '../wayd-empty'
import { useGetLinksQuery } from '@/src/store/features/common/links-api'
import { LinkDto } from '@/src/services/wayd-api'

export interface LinksCardProps {
  objectId: string
  width?: number | string
}

const EditModeButton = ({
  editModeEnabled,
  setEditModeEnabled,
}: {
  editModeEnabled: boolean
  setEditModeEnabled: (enabled: boolean) => void
}) => (
  <Button
    type="text"
    title={`Click to turn ${editModeEnabled ? 'off' : 'on'} Edit mode`}
    aria-label={`Turn ${editModeEnabled ? 'off' : 'on'} edit mode`}
    icon={editModeEnabled ? <EditTwoTone /> : <EditOutlined />}
    onClick={() => setEditModeEnabled(!editModeEnabled)}
  />
)

interface LinksContentProps {
  isLoading: boolean
  hasLinks: boolean
  linksData: LinkDto[]
  editModeEnabled: boolean
  canUpdateLinks: boolean
  canDeleteLinks: boolean
  error?: unknown
}

const LinksContent = ({
  isLoading,
  hasLinks,
  linksData,
  editModeEnabled,
  canUpdateLinks,
  canDeleteLinks,
  error,
}: LinksContentProps) => {
  const sortedLinks = [...linksData].sort((a, b) => a.name.localeCompare(b.name))

  if (isLoading) {
    return <Spin size="small" />
  }

  if (error) {
    return <Alert type="error" title="Failed to load links" showIcon />
  }

  if (!hasLinks) {
    return <WaydEmpty message="No links found" />
  }

  return (
    <Flex vertical gap="small" style={{ width: '100%' }} role="list">
      {sortedLinks.map((item) => (
        <LinkItem
          key={item.id}
          link={item}
          editModeEnabled={editModeEnabled}
          canUpdateLinks={canUpdateLinks}
          canDeleteLinks={canDeleteLinks}
        />
      ))}
    </Flex>
  )
}

const LinksCard = ({ objectId, width = 300 }: LinksCardProps) => {
  const [openCreateLinkForm, setOpenCreateLinkForm] = useState<boolean>(false)
  const [editModeEnabled, setEditModeEnabled] = useState<boolean>(false)

  const {
    data: linksData,
    isLoading,
    error,
  } = useGetLinksQuery(objectId, { skip: !objectId })

  const hasLinks = linksData && linksData.length > 0

  const { hasPermissionClaim } = useAuth()
  const canCreateLinks = hasPermissionClaim('Permissions.Links.Create')
  const canUpdateLinks = hasPermissionClaim('Permissions.Links.Update')
  const canDeleteLinks = hasPermissionClaim('Permissions.Links.Delete')

  return (
    <>
      <Card
        size="small"
        title="Links"
        style={{ width }}
        extra={
          <>
            {canCreateLinks && (
              <Button
                type="text"
                aria-label="Add new link"
                icon={<PlusOutlined />}
                onClick={() => setOpenCreateLinkForm(true)}
              />
            )}
            {hasLinks && (canUpdateLinks || canDeleteLinks) && (
              <EditModeButton
                editModeEnabled={editModeEnabled}
                setEditModeEnabled={setEditModeEnabled}
              />
            )}
          </>
        }
      >
        <LinksContent
          isLoading={isLoading}
          hasLinks={hasLinks}
          linksData={linksData || []}
          editModeEnabled={editModeEnabled}
          canUpdateLinks={canUpdateLinks}
          canDeleteLinks={canDeleteLinks}
          error={error}
        />
      </Card>
      {openCreateLinkForm && (
        <CreateLinkForm
          objectId={objectId}
          onFormCreate={() => setOpenCreateLinkForm(false)}
          onFormCancel={() => setOpenCreateLinkForm(false)}
        />
      )}
    </>
  )
}

export default LinksCard
