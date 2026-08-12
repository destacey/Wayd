'use client'

import { Empty, Flex, Modal, Skeleton, Timeline, Typography } from 'antd'
import dayjs from 'dayjs'
import { LifecycleStatusTag } from '@/src/components/common'
import { LifecycleCategory } from '@/src/components/types'
import { ProjectStatusHistoryDto } from '@/src/services/wayd-api'
import { getLifecycleCategoryTagColor } from '@/src/utils'
import { useGetProjectStatusHistoryQuery } from '@/src/store/features/ppm/projects-api'

const { Text } = Typography

export interface ProjectStatusHistoryModalProps {
  projectId: string
  isOpen: boolean
  onClose: () => void
}

const changedByLabel = (entry: ProjectStatusHistoryDto) => {
  if (entry.changedBy) return entry.changedBy.name
  return entry.source.name === 'Recorded' ? 'System' : 'Unknown'
}

const ProjectStatusHistoryModal = ({
  projectId,
  isOpen,
  onClose,
}: ProjectStatusHistoryModalProps) => {
  const { data: history, isLoading } = useGetProjectStatusHistoryQuery(
    projectId,
    { skip: !isOpen },
  )

  const items = (history ?? []).map((entry) => ({
    color: getLifecycleCategoryTagColor(
      LifecycleCategory[
        entry.toStatus.lifecycleCategory as keyof typeof LifecycleCategory
      ],
    ),
    content: (
      <Flex vertical gap={2}>
        <Flex gap="small" align="center" wrap>
          {entry.fromStatus && (
            <>
              <LifecycleStatusTag status={entry.fromStatus} />
              <Text type="secondary">→</Text>
            </>
          )}
          <LifecycleStatusTag status={entry.toStatus} />
        </Flex>
        <Text type="secondary">
          {dayjs(entry.changedOn).format('MMM D, YYYY hh:mm A')} by{' '}
          {changedByLabel(entry)}
        </Text>
        {entry.reason && <Text>{entry.reason}</Text>}
      </Flex>
    ),
  }))

  return (
    <Modal
      title="Status History"
      open={isOpen}
      width={'40vw'}
      onCancel={onClose}
      footer={null}
      destroyOnHidden
    >
      {isLoading ? (
        <Skeleton active paragraph={{ rows: 4 }} />
      ) : items.length === 0 ? (
        <Empty description="No status changes have been recorded for this project." />
      ) : (
        <Timeline items={items} />
      )}
    </Modal>
  )
}

export default ProjectStatusHistoryModal
