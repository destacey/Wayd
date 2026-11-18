'use client'

import { Alert, Button, Card, Flex, Spin } from 'antd'
import { EditOutlined, EditTwoTone, PlusOutlined } from '@ant-design/icons'
import { useState } from 'react'
import LinkForm from './link-form'
import LinkItem from './link-item'
import WaydEmpty from '../wayd-empty'
import {
  LinkOwner,
  LinkOwnerType,
  useGetLinksQuery,
} from '@/src/store/features/common/links-api'
import { LinkDto } from '@/src/services/api/wayd.schemas'
import { caseInsensitiveCompare } from '../wayd-grid'

export interface LinksCardProps {
  /** The kind of record the links are on, from `LinkOwnerTypes`. */
  ownerType: LinkOwnerType
  /** The record's id. */
  ownerId: string
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
  owner: LinkOwner
  isLoading: boolean
  links: LinkDto[]
  editModeEnabled: boolean
  error?: unknown
}

const LinksContent = ({
  owner,
  isLoading,
  links,
  editModeEnabled,
  error,
}: LinksContentProps) => {
  if (isLoading) {
    return <Spin size="small" />
  }

  if (error) {
    return <Alert type="error" title="Failed to load links" showIcon />
  }

  if (links.length === 0) {
    return <WaydEmpty message="No links found" />
  }

  const sortedLinks = [...links].sort((a, b) =>
    caseInsensitiveCompare(a.name, b.name),
  )

  return (
    <Flex vertical gap="small" style={{ width: '100%' }} role="list">
      {sortedLinks.map((item) => (
        <LinkItem
          key={item.id}
          owner={owner}
          link={item}
          editModeEnabled={editModeEnabled}
        />
      ))}
    </Flex>
  )
}

/**
 * A record's links. Whether the viewer may add, change and remove them is the
 * server's answer for this record (`canManage`), the same rule that lets them
 * edit the record itself.
 */
const LinksCard = ({ ownerType, ownerId, width = 300 }: LinksCardProps) => {
  const [openCreateLinkForm, setOpenCreateLinkForm] = useState<boolean>(false)
  const [editModeEnabled, setEditModeEnabled] = useState<boolean>(false)

  const owner: LinkOwner = { ownerType, ownerId }

  // currentData, not data: while a drawer moves to another record, data still holds the previous record's links and
  // its canManage.
  const { currentData, isFetching, error } = useGetLinksQuery(owner, {
    skip: !ownerId,
  })

  const links = currentData?.links ?? []
  const canManage = currentData?.canManage ?? false
  const isLoading = isFetching && !currentData

  // Edit mode ends when there is nothing left to edit, so a link added afterwards is not shown already in it.
  if (editModeEnabled && !isLoading && (!canManage || links.length === 0)) {
    setEditModeEnabled(false)
  }

  const editing = editModeEnabled && canManage && links.length > 0

  return (
    <>
      <Card
        size="small"
        title="Links"
        style={{ width }}
        extra={
          canManage && (
            <>
              <Button
                type="text"
                aria-label="Add new link"
                icon={<PlusOutlined />}
                onClick={() => setOpenCreateLinkForm(true)}
              />
              {links.length > 0 && (
                <EditModeButton
                  editModeEnabled={editing}
                  setEditModeEnabled={setEditModeEnabled}
                />
              )}
            </>
          )
        }
      >
        <LinksContent
          owner={owner}
          isLoading={isLoading}
          links={links}
          editModeEnabled={editing}
          error={error}
        />
      </Card>
      {openCreateLinkForm && (
        <LinkForm
          owner={owner}
          onFormComplete={() => setOpenCreateLinkForm(false)}
          onFormCancel={() => setOpenCreateLinkForm(false)}
        />
      )}
    </>
  )
}

export default LinksCard
