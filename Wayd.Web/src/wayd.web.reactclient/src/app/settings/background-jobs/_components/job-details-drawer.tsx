'use client'

import { useGetJobDetailQuery } from '@/src/store/features/admin/background-jobs-api'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { getDrawerWidthPixels } from '@/src/utils'
import { Button, Drawer, Flex, Space, Table, Typography } from 'antd'
import { FC, useEffect, useState } from 'react'
import { LabeledContent } from '@/src/components/common/content'
import useBackgroundJobActions from './use-background-job-actions'

export interface JobDetailsDrawerProps {
  jobId: string
  drawerOpen: boolean
  onDrawerClose: () => void
}

const preStyle: React.CSSProperties = {
  margin: 0,
  padding: '8px 12px',
  borderRadius: 'var(--ant-border-radius)',
  backgroundColor: 'var(--ant-color-fill-quaternary)',
  overflowX: 'auto',
  fontSize: 'var(--ant-font-size-sm)',
}

// The NSwag axios client doesn't revive date strings into Date instances, so
// these are ISO strings at runtime despite the typed Date.
const formatTimestamp = (value?: Date | null): string | undefined =>
  value ? new Date(value).toLocaleString() : undefined

const JobDetailsDrawer: FC<JobDetailsDrawerProps> = ({
  jobId,
  drawerOpen,
  onDrawerClose,
}) => {
  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()

  const canRun = hasPermissionClaim('Permissions.BackgroundJobs.Run')
  const canDelete = hasPermissionClaim('Permissions.BackgroundJobs.Delete')

  const { data: job, isLoading, error } = useGetJobDetailQuery(jobId)
  const { handleRequeue, handleDelete } = useBackgroundJobActions()

  useEffect(() => {
    if (error) {
      messageApi.error(
        'An error occurred while loading the job. Please try again.',
      )
    }
  }, [error, messageApi])

  const extraActions =
    job && (canRun || canDelete) ? (
      <Space>
        {canRun && (
          <Button onClick={() => handleRequeue(job, onDrawerClose)}>
            Requeue
          </Button>
        )}
        {canDelete && (
          <Button danger onClick={() => handleDelete(job, onDrawerClose)}>
            Delete
          </Button>
        )}
      </Space>
    ) : undefined

  return (
    <Drawer
      title={job ? `${job.type}.${job.action}` : 'Job'}
      placement="right"
      onClose={onDrawerClose}
      open={drawerOpen}
      loading={isLoading}
      size={size}
      resizable={{
        onResize: (newSize) => setSize(newSize),
      }}
      destroyOnHidden={true}
      extra={extraActions}
    >
      <Flex vertical gap={10}>
        <LabeledContent label="Job Id">{job?.id}</LabeledContent>
        <LabeledContent label="State">{job?.state}</LabeledContent>
        <LabeledContent label="Type">
          {job ? `${job.namespace}.${job.type}` : undefined}
        </LabeledContent>
        <LabeledContent label="Method">{job?.action}</LabeledContent>
        <LabeledContent label="Created">
          {formatTimestamp(job?.createdAt)}
        </LabeledContent>
        {job?.expiresAt && (
          <LabeledContent label="Expires">
            {formatTimestamp(job.expiresAt)}
          </LabeledContent>
        )}
        {job?.arguments && job.arguments.length > 0 && (
          <LabeledContent label="Arguments">
            <pre style={preStyle}>{job.arguments.join('\n')}</pre>
          </LabeledContent>
        )}
        {job?.exceptionType && (
          <LabeledContent label="Exception Type">
            {job.exceptionType}
          </LabeledContent>
        )}
        {job?.exceptionMessage && (
          <LabeledContent label="Exception Message">
            {job.exceptionMessage}
          </LabeledContent>
        )}
        {job?.exceptionDetails && (
          <LabeledContent label="Stack Trace">
            <pre style={preStyle}>{job.exceptionDetails}</pre>
          </LabeledContent>
        )}
        {job?.history && job.history.length > 0 && (
          <LabeledContent label="State History">
            <Table
              size="small"
              pagination={false}
              rowKey={(record, index) => `${record.state}-${index}`}
              dataSource={job.history}
              columns={[
                { title: 'State', dataIndex: 'state', key: 'state' },
                {
                  title: 'Changed',
                  dataIndex: 'changedAt',
                  key: 'changedAt',
                  render: (value: Date | undefined) => formatTimestamp(value),
                },
                {
                  title: 'Reason',
                  dataIndex: 'reason',
                  key: 'reason',
                  render: (value: string | undefined) =>
                    value ? (
                      <Typography.Text type="secondary">{value}</Typography.Text>
                    ) : null,
                },
              ]}
            />
          </LabeledContent>
        )}
      </Flex>
    </Drawer>
  )
}

export default JobDetailsDrawer
