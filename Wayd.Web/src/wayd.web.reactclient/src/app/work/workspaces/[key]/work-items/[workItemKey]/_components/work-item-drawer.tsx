'use client'

import ExternalIconLink from '@/src/components/common/external-icon-link'
import { useMessage } from '@/src/components/contexts/messaging'
import { useGetWorkItemQuery } from '@/src/store/features/work-management/workspace-api'
import { getDrawerWidthPixels, isApiError } from '@/src/utils'
import { Divider, Drawer, Flex, Tag, Typography } from 'antd'
import Link from 'next/link'
import { useEffect, useState } from 'react'
import WorkItemFacts from './work-item-facts'
import { workItemHref } from './work-item-dependency-neighbourhood'

const { Text } = Typography

export interface WorkItemDrawerProps {
  workspaceKey: string
  workItemKey: string
  open: boolean
  onClose: () => void
}

/** A work item's facts, opened beside whatever the reader was looking at rather than in place of it. */
const WorkItemDrawer = ({
  workspaceKey,
  workItemKey,
  open,
  onClose,
}: WorkItemDrawerProps) => {
  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const messageApi = useMessage()

  const {
    data: workItem,
    isLoading,
    error,
  } = useGetWorkItemQuery({ idOrKey: workspaceKey, workItemKey })

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the work item. Please try again.',
      )
    }
  }, [error, messageApi])

  return (
    <Drawer
      title={
        <ExternalIconLink
          content={workItemKey}
          url={workItem?.externalViewWorkItemUrl}
          tooltip="Open in external system"
        />
      }
      extra={
        <Link href={workItemHref({ workspaceKey, key: workItemKey })}>
          Open
        </Link>
      }
      placement="right"
      onClose={onClose}
      open={open}
      loading={isLoading}
      size={size}
      resizable={{
        onResize: (newSize) => setSize(newSize),
      }}
      destroyOnHidden={true}
    >
      {workItem && (
        <Flex vertical gap="middle">
          <Flex vertical gap={4}>
            <Text strong>{workItem.title}</Text>
            <div>
              <Tag>{workItem.status}</Tag>
            </div>
          </Flex>
          <Divider size="small" style={{ margin: 0 }} />
          <WorkItemFacts workItem={workItem} />
        </Flex>
      )}
    </Drawer>
  )
}

export default WorkItemDrawer
