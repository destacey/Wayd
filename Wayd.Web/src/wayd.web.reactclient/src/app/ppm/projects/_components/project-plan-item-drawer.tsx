'use client'

import { MoreOutlined, PlusOutlined } from '@ant-design/icons'
import {
  LabeledContent,
} from '@/src/components/common/content'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useGetProjectTaskQuery } from '@/src/store/features/ppm/project-tasks-api'
import { getDrawerWidthPixels, isApiError } from '@/src/utils'
import { Button, Divider, Drawer, Dropdown, Flex } from 'antd'
import Link from 'next/link'
import { FC, useEffect, useState } from 'react'

export interface ProjectPlanItemDrawerProps {
  projectKey: string
  taskId: string | null
  stageName?: string
  drawerOpen: boolean
  onDrawerClose: () => void
  onOpenTask: (taskId: string) => void
  onEditTask: (taskId: string) => void
  onDeleteTask: (taskId: string) => void
  onAddChildTask: (taskId: string) => void
}

const formatDate = (value?: Date) =>
  value
    ? new Date(value).toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric',
        year: 'numeric',
      })
    : ''

const ProjectPlanItemDrawer: FC<ProjectPlanItemDrawerProps> = ({
  projectKey,
  taskId,
  stageName,
  drawerOpen,
  onDrawerClose,
  onOpenTask,
  onEditTask,
  onDeleteTask,
  onAddChildTask,
}) => {
  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const messageApi = useMessage()

  const {
    data: taskData,
    isLoading,
    error,
  } = useGetProjectTaskQuery(
    { projectIdOrKey: projectKey, taskIdOrKey: taskId ?? '' },
    { skip: !drawerOpen || !taskId },
  )

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading task details. Please try again.',
      )
    }
  }, [error, messageApi])

  const assignees = taskData?.assignees ?? []
  const isMilestone = taskData?.type?.name === 'Milestone'
  const taskMenuItems = taskData
    ? [
        {
          key: 'edit',
          label: 'Edit',
          onClick: () => onEditTask(taskData.id),
        },
        {
          key: 'add-child',
          label: 'Add Child Task',
          icon: <PlusOutlined />,
          onClick: () => onAddChildTask(taskData.id),
        },
        { key: 'divider', type: 'divider' as const },
        {
          key: 'delete',
          label: 'Delete',
          danger: true,
          onClick: () => onDeleteTask(taskData.id),
        },
      ]
    : []

  return (
    <Drawer
      title={taskData?.name ?? 'Task Details'}
      extra={
        taskData ? (
          <Dropdown menu={{ items: taskMenuItems }} trigger={['click']}>
            <Button type="text" size="small" icon={<MoreOutlined />} />
          </Dropdown>
        ) : null
      }
      placement="right"
      onClose={onDrawerClose}
      open={drawerOpen}
      loading={isLoading}
      size={size}
      resizable={{
        onResize: (newSize) => setSize(newSize),
      }}
      destroyOnHidden={true}
    >
      <Flex vertical gap={10}>
        <LabeledContent label="Key">{taskData?.key ?? '-'}</LabeledContent>
        <LabeledContent label="Type">{taskData?.type?.name ?? '-'}</LabeledContent>
        <LabeledContent label="Status">{taskData?.status?.name ?? '-'}</LabeledContent>
        <LabeledContent label="Priority">{taskData?.priority?.name ?? '-'}</LabeledContent>
        <LabeledContent label="Progress">
          {taskData?.progress !== undefined ? `${taskData.progress}%` : '-'}
        </LabeledContent>
        {isMilestone ? (
          <LabeledContent label="Planned Date">
            {formatDate(taskData?.plannedDate) || '-'}
          </LabeledContent>
        ) : (
          <>
            <LabeledContent label="Planned Start">
              {formatDate(taskData?.plannedStart) || '-'}
            </LabeledContent>
            <LabeledContent label="Planned End">
              {formatDate(taskData?.plannedEnd) || '-'}
            </LabeledContent>
          </>
        )}
        <Divider size="small" />
        {taskData?.estimatedEffortHours !== undefined &&
          taskData?.estimatedEffortHours !== null && (
            <LabeledContent label="Estimated Effort (hrs)">
              {taskData.estimatedEffortHours}
            </LabeledContent>
          )}
        <LabeledContent label="Stage">{stageName ?? '-'}</LabeledContent>
        {taskData?.parent?.id && (
          <LabeledContent label="Parent Task">
            <Button
              type="link"
              size="small"
              style={{ padding: 0, height: 'auto' }}
              onClick={() => onOpenTask(taskData.parent!.id)}
            >
              {`${taskData.parent.key} - ${taskData.parent.name}`}
            </Button>
          </LabeledContent>
        )}
        <LabeledContent label="Assignees">
          {assignees.length > 0 ? (
            <ul style={{ margin: 0, paddingLeft: 16 }}>
              {assignees.map((a) => (
                <li key={a.id}>
                  <Link href={`/organizations/employees/${a.key}`}>
                    {a.name}
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            'No assignees'
          )}
        </LabeledContent>
        <LabeledContent label="Description">
          {taskData?.description?.trim() ? (
            <MarkdownRenderer markdown={taskData.description} />
          ) : (
            'No description'
          )}
        </LabeledContent>
      </Flex>
    </Drawer>
  )
}

export default ProjectPlanItemDrawer
