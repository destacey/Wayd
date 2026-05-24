'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import {
  Button,
  Select,
  Table,
  Tag,
  Tooltip,
  Typography,
  Space,
  Spin,
  Alert,
} from 'antd'
import type { TableColumnsType, TableProps } from 'antd'
import { SyncOutlined } from '@ant-design/icons'
import {
  useGetSyncRunsQuery,
  useGetSyncRunQuery,
  useRunSyncMutation,
} from '@/src/store/features/app-integration/connections-api'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  SyncRunListDto,
  SyncRunStatus,
  SyncTriggerSource,
  SyncType,
  WorkspaceSyncDetail,
} from '@/src/services/wayd-api'

interface Props {
  connectionId: string
}

const STATUS_COLOR: Record<SyncRunStatus, string> = {
  [SyncRunStatus.Running]: 'processing',
  [SyncRunStatus.Succeeded]: 'success',
  [SyncRunStatus.Failed]: 'error',
  [SyncRunStatus.Cancelled]: 'warning',
}

const STATUS_LABEL: Record<SyncRunStatus, string> = {
  [SyncRunStatus.Running]: 'Running',
  [SyncRunStatus.Succeeded]: 'Succeeded',
  [SyncRunStatus.Failed]: 'Failed',
  [SyncRunStatus.Cancelled]: 'Cancelled',
}

const TRIGGER_LABEL: Record<SyncTriggerSource, string> = {
  [SyncTriggerSource.Scheduled]: 'Scheduled',
  [SyncTriggerSource.Manual]: 'Manual',
  [SyncTriggerSource.Api]: 'API',
}

const SYNC_TYPE_LABEL: Record<SyncType, string> = {
  [SyncType.Full]: 'Full',
  [SyncType.Differential]: 'Differential',
}

const RANGE_OPTIONS = [
  { label: 'Last 24 hours', value: 24 },
  { label: 'Last 7 days', value: 24 * 7 },
  { label: 'Last 30 days', value: 24 * 30 },
] as const

type RangeHours = (typeof RANGE_OPTIONS)[number]['value']

function formatDuration(startedAt: string, finishedAt?: string): string {
  if (!finishedAt) return '—'
  const ms = new Date(finishedAt).getTime() - new Date(startedAt).getTime()
  const seconds = Math.floor(ms / 1000)
  if (seconds < 60) return `${seconds}s`
  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`
}

function ExpandedRow({ syncRunId }: { syncRunId: string }) {
  const { data, isLoading } = useGetSyncRunQuery(syncRunId)

  if (isLoading) return <Spin size="small" />

  if (!data?.details?.length) {
    return (
      <Typography.Text type="secondary" style={{ paddingLeft: 24 }}>
        No per-workspace details available.
      </Typography.Text>
    )
  }

  const workspaceColumns: TableColumnsType<WorkspaceSyncDetail> = [
    {
      title: 'Workspace',
      dataIndex: 'workspaceName',
      key: 'workspaceName',
    },
    {
      title: 'Status',
      key: 'status',
      render: (_, r) => (
        <Tag color={r.succeeded ? 'success' : 'error'}>
          {r.succeeded ? 'Succeeded' : 'Failed'}
        </Tag>
      ),
      width: 110,
    },
    { title: 'Items', dataIndex: 'workItemsProcessed', key: 'workItemsProcessed', width: 80 },
    { title: 'Parent links', dataIndex: 'parentLinkChangesProcessed', key: 'parentLinks', width: 110 },
    { title: 'Dep. links', dataIndex: 'dependencyLinkChangesProcessed', key: 'depLinks', width: 100 },
    { title: 'Deletions', dataIndex: 'deletedWorkItemsProcessed', key: 'deletions', width: 90 },
    {
      title: 'Error',
      dataIndex: 'error',
      key: 'error',
      render: (error: string | undefined) =>
        error ? (
          <Tooltip title={error}>
            <Typography.Text type="danger" style={{ cursor: 'pointer' }}>
              {error.length > 60 ? `${error.slice(0, 60)}…` : error}
            </Typography.Text>
          </Tooltip>
        ) : null,
    },
  ]

  return (
    <Table<WorkspaceSyncDetail>
      columns={workspaceColumns}
      dataSource={data.details}
      rowKey="internalWorkspaceId"
      pagination={false}
      size="small"
      style={{ margin: '0 24px 8px' }}
    />
  )
}

export default function SyncHistoryTab({ connectionId }: Props) {
  const messageApi = useMessage()
  const { hasClaim } = useAuth()
  const canSync = hasClaim('Permission', 'Permissions.Connections.Update')

  const [rangeHours, setRangeHours] = useState<RangeHours>(24)

  // Recompute the cutoff each time the range changes. We pass an ISO string so the
  // RTK Query cache key stays serializable (Redux flags raw Date objects in state).
  const sinceIso = useMemo(
    () => new Date(Date.now() - rangeHours * 60 * 60 * 1000).toISOString(),
    [rangeHours],
  )

  const { data: runs, isLoading } = useGetSyncRunsQuery({ connectionId, sinceIso })
  const isAnyRunning = runs?.some((r) => r.status === SyncRunStatus.Running) ?? false

  // After clicking Sync now, the Hangfire job may take a moment to start. Poll
  // briefly until we actually see a Running row, then the regular polling takes over.
  const [waitingForRun, setWaitingForRun] = useState(false)
  const waitingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    if (waitingForRun && isAnyRunning) {
      setWaitingForRun(false)
      if (waitingTimeoutRef.current) {
        clearTimeout(waitingTimeoutRef.current)
        waitingTimeoutRef.current = null
      }
    }
  }, [waitingForRun, isAnyRunning])

  useEffect(() => {
    return () => {
      if (waitingTimeoutRef.current) clearTimeout(waitingTimeoutRef.current)
    }
  }, [])

  const shouldPoll = isAnyRunning || waitingForRun

  // Re-fetch every 3 s while a run is in Running state or we are waiting for
  // a freshly-triggered run to appear.
  useGetSyncRunsQuery(
    { connectionId, sinceIso },
    { pollingInterval: shouldPoll ? 3000 : 0, skip: !shouldPoll },
  )

  const [runSync, { isLoading: isTriggeringSync }] = useRunSyncMutation()

  const handleSyncNow = async (syncType: SyncType) => {
    try {
      await runSync({ connectionId, syncType }).unwrap()
      const label = syncType === SyncType.Full ? 'Full sync' : 'Differential sync'
      messageApi.success(`${label} triggered — a new run will appear shortly.`)
      setWaitingForRun(true)
      if (waitingTimeoutRef.current) clearTimeout(waitingTimeoutRef.current)
      // Safety net: stop polling after 30 s if the Running row never shows up.
      waitingTimeoutRef.current = setTimeout(() => setWaitingForRun(false), 30000)
    } catch {
      messageApi.error('Failed to trigger sync.')
    }
  }

  const columns: TableColumnsType<SyncRunListDto> = [
    {
      title: 'Started',
      dataIndex: 'startedAt',
      key: 'startedAt',
      render: (v: string) => (
        <Tooltip title={new Date(v).toLocaleString()}>
          <span>{new Date(v).toLocaleString()}</span>
        </Tooltip>
      ),
      width: 180,
    },
    {
      title: 'Duration',
      key: 'duration',
      render: (_, r) => formatDuration(r.startedAt as unknown as string, r.finishedAt as unknown as string | undefined),
      width: 100,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: SyncRunStatus) => (
        <Tag
          color={STATUS_COLOR[status]}
          icon={status === SyncRunStatus.Running ? <SyncOutlined spin /> : undefined}
        >
          {STATUS_LABEL[status]}
        </Tag>
      ),
      width: 120,
    },
    {
      title: 'Trigger',
      dataIndex: 'triggerSource',
      key: 'triggerSource',
      render: (t: SyncTriggerSource) => TRIGGER_LABEL[t] ?? t,
      width: 100,
    },
    {
      title: 'Type',
      dataIndex: 'syncType',
      key: 'syncType',
      render: (t: SyncType) => SYNC_TYPE_LABEL[t] ?? t,
      width: 110,
    },
    {
      title: 'Workspaces',
      key: 'workspaces',
      render: (_, r) => {
        const label = `${r.workspacesSucceeded}/${r.workspacesPlanned}`
        return r.workspacesFailed > 0 ? (
          <Typography.Text type="danger">{label}</Typography.Text>
        ) : (
          label
        )
      },
      width: 110,
    },
    {
      title: 'Items',
      dataIndex: 'workItemsProcessed',
      key: 'workItemsProcessed',
      width: 80,
    },
    {
      title: 'Error',
      dataIndex: 'errorMessage',
      key: 'errorMessage',
      render: (msg: string | undefined) =>
        msg ? (
          <Tooltip title={msg}>
            <Typography.Text type="danger" style={{ cursor: 'pointer' }}>
              {msg.length > 50 ? `${msg.slice(0, 50)}…` : msg}
            </Typography.Text>
          </Tooltip>
        ) : null,
    },
  ]

  const expandable: TableProps<SyncRunListDto>['expandable'] = {
    expandedRowRender: (record) => (
      <ExpandedRow syncRunId={record.id} />
    ),
    rowExpandable: () => true,
  }

  return (
    <Space orientation="vertical" style={{ width: '100%' }} size="middle">
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 8,
        }}
      >
        <Select<RangeHours>
          value={rangeHours}
          onChange={setRangeHours}
          options={RANGE_OPTIONS.map((o) => ({ label: o.label, value: o.value }))}
          style={{ width: 160 }}
        />
        <Tooltip
          title={!canSync ? 'Requires Connections: Update permission' : undefined}
        >
          <Space>
            <Button
              icon={<SyncOutlined />}
              disabled={!canSync}
              loading={isTriggeringSync}
              onClick={() => handleSyncNow(SyncType.Differential)}
            >
              Diff Sync
            </Button>
            <Button
              icon={<SyncOutlined />}
              disabled={!canSync}
              loading={isTriggeringSync}
              onClick={() => handleSyncNow(SyncType.Full)}
            >
              Full Sync
            </Button>
          </Space>
        </Tooltip>
      </div>

      {isLoading ? (
        <Spin />
      ) : runs?.length === 0 ? (
        <Alert
          type="info"
          message="No sync runs in the selected window."
          showIcon
        />
      ) : (
        <Table<SyncRunListDto>
          columns={columns}
          dataSource={runs}
          rowKey="id"
          expandable={expandable}
          pagination={{ pageSize: 20, hideOnSinglePage: true }}
          size="small"
          loading={isLoading}
        />
      )}
    </Space>
  )
}
