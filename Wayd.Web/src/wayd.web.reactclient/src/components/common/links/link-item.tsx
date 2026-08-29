'use client'

import { LinkDto } from '@/src/services/wayd-api'
import { DeleteOutlined, EditOutlined } from '@ant-design/icons'
import { Button, Flex, Popconfirm } from 'antd'
import Link from 'next/link'
import { CSSProperties, useState } from 'react'
import EditLinkForm from './edit-link-form'
import {
  StoreDeleteLinkRequest,
  useDeleteLinkMutation,
} from '@/src/store/features/common/links-api'
import { useMessage } from '../../contexts/messaging'

export interface LinkItemProps {
  link: LinkDto
  editModeEnabled: boolean
  canUpdateLinks: boolean
  canDeleteLinks: boolean
}

const truncateStyle: CSSProperties = {
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
  minWidth: 0,
  flex: 1,
}

const LinkItem = ({
  link,
  editModeEnabled,
  canUpdateLinks,
  canDeleteLinks,
}: LinkItemProps) => {
  const [openEditLinkForm, setOpenEditLinkForm] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)
  const messageApi = useMessage()

  const [deleteLinkMutation] = useDeleteLinkMutation()

  const handleDelete = async () => {
    setIsDeleting(true)
    try {
      const storeRequest: StoreDeleteLinkRequest = {
        id: link.id,
        objectId: link.objectId,
      }
      const response = await deleteLinkMutation(storeRequest)
      if (response.error) {
        throw response.error
      }
      messageApi.success('Successfully deleted Link.')
    } catch (error) {
      messageApi.error('An unexpected error occurred while deleting the Link.')
      console.log(error)
    } finally {
      setIsDeleting(false)
    }
  }

  // without this, pages without http or https will open as a relative path
  const url = link.url.match(/^.{3,5}\:\/\//) ? link.url : `//${link.url}`

  const externalLink = (
    <Link
      href={url}
      target="_blank"
      prefetch={false}
      style={editModeEnabled ? truncateStyle : undefined}
      title={link.name}
    >
      {link.name}
    </Link>
  )

  if (!editModeEnabled) {
    return externalLink
  }

  return (
    <>
      <Flex justify="space-between" align="center" style={{ minWidth: 0 }}>
        {externalLink}
        <Flex gap={4} style={{ flexShrink: 0 }}>
          {canUpdateLinks && (
            <Button
              type="text"
              size="small"
              aria-label={`Edit ${link.name}`}
              icon={<EditOutlined style={{ width: '14px' }} />}
              onClick={() => setOpenEditLinkForm(true)}
            />
          )}
          {canDeleteLinks && (
            <Popconfirm
              title="Delete the Link"
              description="Are you sure you want to delete this Link?"
              onConfirm={handleDelete}
              okButtonProps={{ loading: isDeleting }}
              okText="Yes"
              cancelText="No"
            >
              <Button
                type="text"
                size="small"
                aria-label={`Delete ${link.name}`}
                icon={<DeleteOutlined style={{ width: '14px' }} />}
              />
            </Popconfirm>
          )}
        </Flex>
      </Flex>
      {openEditLinkForm && (
        <EditLinkForm
          id={link.id}
          onFormUpdate={() => setOpenEditLinkForm(false)}
          onFormCancel={() => setOpenEditLinkForm(false)}
        />
      )}
    </>
  )
}

export default LinkItem
