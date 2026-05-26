'use client'

import { useEffect, useState } from 'react'
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
  SyncRunDetailsDto,
  SyncRunListDto,
  SyncRunStatus,
  SyncTriggerSource,
  SyncType,
  WorkspaceSyncDetail,
} from '@/src/services/wayd-api'
import styles from './sync-history-tab.module.css'

export type SyncHistoryCategory = 'work' | 'people'

interface Props {
  connectionId: string
  category: SyncHistoryCategory
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

function toDate(value: string | Date): Date {
  return value instanceof Date ? value : new Date(value)
}

function formatDuration(
  startedAt: string | Date,
  finishedAt?: string | Date,
): string {
  if (!finishedAt) return '—'
  const ms = toDate(finishedAt).getTime() - toDate(startedAt).getTime()
  const seconds = Math.floor(ms / 1000)
  if (seconds < 60) return `${seconds}s`
  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`
}

// --- Category-specific schemas + renderers -----------------------------------

interface PeopleSyncDetail {
  employeesFetched?: number
  employeesUpserted?: number
  errors?: string[]
}

function parseDetailsJson<T>(json: string | null | undefined): T | undefined {
  if (!json) return undefined
  try {
    return JSON.parse(json) as T
  } catch {
    return undefined
  }
}

function WorkExpandedRow({ syncRun }: { syncRun: SyncRunDetailsDto }) {
  const details = parseDetailsJson<WorkspaceSyncDetail[]>(syncRun.detailsJson)
  if (!details?.length) {
    return (
      <Typography.Text type="secondary" style={{ paddingLeft: 24 }}>
        No per-workspace details available.
      </Typography.Text>
    )
  }

  const workspaceColumns: TableColumnsType<WorkspaceSyncDetail> = [
    { title: 'Workspace', dataIndex: 'workspaceName', key: 'workspaceName' },
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
      dataSource={details}
      rowKey="internalWorkspaceId"
      pagination={false}
      size="small"
      style={{ margin: '0 24px 8px' }}
    />
  )
}

function PeopleExpandedRow({ syncRun }: { syncRun: SyncRunDetailsDto }) {
  const detail = parseDetailsJson<PeopleSyncDetail>(syncRun.detailsJson)

  if (!detail) {
    return (
      <Typography.Text type="secondary" style={{ paddingLeft: 24 }}>
        No details available.
      </Typography.Text>
    )
  }

  return (
    <Space orientation="vertical" style={{ paddingLeft: 24, paddingBottom: 8 }}>
      <Typography.Text>
        <strong>Employees fetched:</strong> {detail.employeesFetched ?? 0}
        {'  '}
        <strong>Employees upserted:</strong> {detail.employeesUpserted ?? 0}
      </Typography.Text>
      {detail.errors && detail.errors.length > 0 && (
        <>
          <Typography.Text type="danger">Errors:</Typography.Text>
          <ul style={{ margin: 0 }}>
            {detail.errors.map((e, i) => (
              <li key={i}>
                <Typography.Text type="danger">{e}</Typography.Text>
              </li>
            ))}
          </ul>
        </>
      )}
    </Space>
  )
}

function ExpandedRow({
  syncRunId,
  category,
}: {
  syncRunId: string
  category: SyncHistoryCategory
}) {
  const { data, isLoading, isError } = useGetSyncRunQuery(syncRunId)

  if (isLoading) return <Spin size="small" />
  if (isError) {
    return (
      <Alert
        type="error"
        message="Failed to load sync run details."
        showIcon
        style={{ margin: '0 24px 8px' }}
      />
    )
  }
  if (!data) return null

  return category === 'people' ? (
    <PeopleExpandedRow syncRun={data} />
  ) : (
    <WorkExpandedRow syncRun={data} />
  )
}

const workCountColumns: TableColumnsType<SyncRunListDto> = [
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
]

// People-sync metrics live in detailsJson, not on the SyncRunListDto columns. The list view
// fetches each run's details lazily on demand below; that would mean N detail queries to
// populate the list, which we don't want. For the row-level count we just show a single
// "Processed" column derived from workItemsProcessed (always 0 for people sync today) and
// rely on the expanded row for the real fetched/upserted numbers.
const peopleCountColumns: TableColumnsType<SyncRunListDto> = []

export default function SyncHistoryTab({ connectionId, category }: Props) {
  const messageApi = useMessage()
  const { hasClaim } = useAuth()
  const canSync = hasClaim('Permission', 'Permissions.Connections.Update')

  const [rangeHours, setRangeHours] = useState<RangeHours>(24)
  // The cutoff is computed when the user picks a range and stored as an ISO string
  // so the RTK Query cache key stays serializable (Redux flags raw Date objects).
  // Calling Date.now() in render would be impure; we only refresh it on change.
  const [sinceIso, setSinceIso] = useState<string>(
    () => new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
  )

  const handleRangeChange = (hours: RangeHours) => {
    setRangeHours(hours)
    setSinceIso(new Date(Date.now() - hours * 60 * 60 * 1000).toISOString())
  }

  // After clicking Sync now, the Hangfire job may take a moment to start. We keep
  // polling until either a Running row shows up (then the regular polling takes
  // over) or the grace period expires. waitingForRun is cleared by a setTimeout
  // scheduled inside the effect — never by a synchronous setState inside the
  // effect body, which the lint rule (rightly) flags as a cascading-render smell.
  const [waitingForRun, setWaitingForRun] = useState(false)

  useEffect(() => {
    if (!waitingForRun) return
    const handle = setTimeout(() => setWaitingForRun(false), 30000)
    return () => clearTimeout(handle)
  }, [waitingForRun])

  // Initial fetch — no polling here; pollingInterval is wired in the second
  // subscription below so it can react to data this call returns. RTK Query
  // coalesces both calls into a single underlying subscription since they share
  // the same cache key.
  const {
    data: runs,
    isLoading,
    isError,
  } = useGetSyncRunsQuery({ connectionId, sinceIso })

  const isAnyRunning = runs?.some((r) => r.status === SyncRunStatus.Running) ?? false
  const shouldPoll = isAnyRunning || waitingForRun

  useGetSyncRunsQuery(
    { connectionId, sinceIso },
    { pollingInterval: shouldPoll ? 3000 : 0, skip: !shouldPoll },
  )

  const [runSync, { isLoading: isTriggeringSync }] = useRunSyncMutation()

  const handleSyncNow = async (syncType?: SyncType) => {
    try {
      await runSync({ connectionId, syncType }).unwrap()
      const label =
        category === 'people'
          ? 'Sync'
          : syncType === SyncType.Full
            ? 'Full sync'
            : 'Differential sync'
      messageApi.success(`${label} triggered — a new run will appear shortly.`)
      // The effect on [waitingForRun] schedules the 30 s reset.
      setWaitingForRun(true)
    } catch {
      messageApi.error('Failed to trigger sync.')
    }
  }

  const sharedColumns: TableColumnsType<SyncRunListDto> = [
    {
      title: 'Started',
      dataIndex: 'startedAt',
      key: 'startedAt',
      render: (v: string | Date) => {
        const formatted = toDate(v).toLocaleString()
        return (
          <Tooltip title={formatted}>
            <span>{formatted}</span>
          </Tooltip>
        )
      },
      width: 180,
    },
    {
      title: 'Duration',
      key: 'duration',
      render: (_, r) => formatDuration(r.startedAt, r.finishedAt),
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
  ]

  // Type column is only meaningful when the connector supports multiple sync types.
  const typeColumn: TableColumnsType<SyncRunListDto> =
    category === 'people'
      ? []
      : [
          {
            title: 'Type',
            dataIndex: 'syncType',
            key: 'syncType',
            render: (t: SyncType) => SYNC_TYPE_LABEL[t] ?? t,
            width: 110,
          },
        ]

  const errorColumn: TableColumnsType<SyncRunListDto> = [
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

  const countColumns =
    category === 'people' ? peopleCountColumns : workCountColumns

  const columns: TableColumnsType<SyncRunListDto> = [
    ...sharedColumns,
    ...typeColumn,
    ...countColumns,
    ...errorColumn,
  ]

  const expandable: TableProps<SyncRunListDto>['expandable'] = {
    expandedRowRender: (record) => (
      <ExpandedRow syncRunId={record.id} category={category} />
    ),
    rowExpandable: () => true,
  }

  const syncButtons =
    category === 'people' ? (
      <Button
        icon={<SyncOutlined />}
        disabled={!canSync}
        loading={isTriggeringSync}
        onClick={() => handleSyncNow()}
      >
        Sync Now
      </Button>
    ) : (
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
    )

  return (
    <Space
      orientation="vertical"
      style={{ width: '100%' }}
      size="middle"
      className={styles.expandRoot}
    >
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
          onChange={handleRangeChange}
          options={RANGE_OPTIONS.map((o) => ({ label: o.label, value: o.value }))}
          style={{ width: 160 }}
        />
        <Tooltip
          title={!canSync ? 'Requires Connections: Update permission' : undefined}
        >
          {syncButtons}
        </Tooltip>
      </div>

      {isLoading ? (
        <Spin />
      ) : isError ? (
        <Alert
          type="error"
          message="Failed to load sync run history."
          showIcon
        />
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
