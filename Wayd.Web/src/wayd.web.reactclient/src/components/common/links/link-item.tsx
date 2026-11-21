'use client'

import { LinkDto } from '@/src/services/api/wayd.schemas'
import { DeleteOutlined, EditOutlined } from '@ant-design/icons'
import { Button, Flex, Popconfirm } from 'antd'
import Link from 'next/link'
import { CSSProperties, useState } from 'react'
import LinkForm from './link-form'
import {
  LinkOwner,
  useDeleteLinkMutation,
} from '@/src/store/features/common/links-api'
import { useMessage } from '../../contexts/messaging'
import { toApiError } from '@/src/utils'
import { safeLinkHref } from '../markdown/wysiwyg/safe-urls'

export interface LinkItemProps {
  owner: LinkOwner
  link: LinkDto
  /** Shows the edit and delete buttons. Only ever on for a viewer who may manage the record's links. */
  editModeEnabled: boolean
}

const truncateStyle: CSSProperties = {
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
  minWidth: 0,
  flex: 1,
}

const LinkItem = ({ owner, link, editModeEnabled }: LinkItemProps) => {
  const [openEditLinkForm, setOpenEditLinkForm] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)
  const messageApi = useMessage()

  const [deleteLinkMutation] = useDeleteLinkMutation()

  const handleDelete = async () => {
    setIsDeleting(true)
    try {
      const response = await deleteLinkMutation({ ...owner, linkId: link.id })
      if (response.error) {
        throw response.error
      }
      messageApi.success('Successfully deleted Link.')
    } catch (error) {
      messageApi.error(
        toApiError(error).detail ??
          'An unexpected error occurred while deleting the Link.',
      )
      console.error(error)
    } finally {
      setIsDeleting(false)
    }
  }

  // A link saved before addresses were checked may hold any scheme; it shows as text, never as an href.
  const href = safeLinkHref(link.url)
  const externalLink = href ? (
    <Link
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      prefetch={false}
      style={editModeEnabled ? truncateStyle : undefined}
      title={link.name}
    >
      {link.name}
    </Link>
  ) : (
    <span style={editModeEnabled ? truncateStyle : undefined} title={link.name}>
      {link.name}
    </span>
  )

  if (!editModeEnabled) {
    return externalLink
  }

  return (
    <>
      <Flex justify="space-between" align="center" style={{ minWidth: 0 }}>
        {externalLink}
        <Flex gap={4} style={{ flexShrink: 0 }}>
          <Button
            type="text"
            size="small"
            aria-label={`Edit ${link.name}`}
            icon={<EditOutlined style={{ width: '14px' }} />}
            onClick={() => setOpenEditLinkForm(true)}
          />
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
        </Flex>
      </Flex>
      {openEditLinkForm && (
        <LinkForm
          owner={owner}
          link={link}
          onFormComplete={() => setOpenEditLinkForm(false)}
          onFormCancel={() => setOpenEditLinkForm(false)}
        />
      )}
    </>
  )
}

export default LinkItem
