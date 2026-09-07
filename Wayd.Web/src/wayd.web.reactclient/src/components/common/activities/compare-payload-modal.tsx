'use client'

import {
  ArrowRightOutlined,
  CheckOutlined,
  CopyOutlined,
  DiffOutlined,
  EyeOutlined,
} from '@ant-design/icons'
import {
  App,
  Button,
  Card,
  Empty,
  Flex,
  Modal,
  Segmented,
  Select,
  Switch,
  Tag,
  Typography,
  theme,
} from 'antd'
import dayjs from 'dayjs'
import { FC, useMemo, useState } from 'react'
import { ActivityLogDto } from '@/src/services/wayd-api'

const { Text } = Typography

export interface ComparePayloadModalProps {
  open: boolean
  onClose: () => void
  currentActivity: ActivityLogDto | null
  previousActivity?: ActivityLogDto | null
  allActivities?: ActivityLogDto[]
}

export type DiffFieldStatus = 'changed' | 'added' | 'removed' | 'unchanged'

export interface PayloadFieldDiff {
  key: string
  label: string
  previousValue: unknown
  currentValue: unknown
  status: DiffFieldStatus
  isMetadata: boolean
}

const METADATA_KEYS = new Set([
  'id',
  'eventid',
  'timestamp',
  'actor',
  'correlationid',
  'aggregateid',
  'aggregatetype',
  'eventversion',
])

const formatFieldLabel = (key: string): string => {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, (s) => s.toUpperCase())
    .trim()
}

const parsePayloadObject = (
  payload: unknown,
): Record<string, unknown> | null => {
  if (!payload) return null
  if (typeof payload === 'object' && !Array.isArray(payload)) {
    return payload as Record<string, unknown>
  }
  if (typeof payload === 'string') {
    try {
      const parsed = JSON.parse(payload)
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        return parsed as Record<string, unknown>
      }
    } catch {
      return null
    }
  }
  return null
}

export const computePayloadDiff = (
  prevPayload: unknown,
  currPayload: unknown,
  includeMetadata = false,
): PayloadFieldDiff[] => {
  const prevObj = parsePayloadObject(prevPayload) ?? {}
  const currObj = parsePayloadObject(currPayload) ?? {}

  const allKeys = Array.from(
    new Set([...Object.keys(prevObj), ...Object.keys(currObj)]),
  ).sort((a, b) => a.localeCompare(b))

  const results: PayloadFieldDiff[] = []

  for (const key of allKeys) {
    const isMeta = METADATA_KEYS.has(key.toLowerCase())
    if (!includeMetadata && isMeta) {
      continue
    }

    const hasPrev = Object.prototype.hasOwnProperty.call(prevObj, key)
    const hasCurr = Object.prototype.hasOwnProperty.call(currObj, key)
    const prevVal = hasPrev ? prevObj[key] : undefined
    const currVal = hasCurr ? currObj[key] : undefined

    let status: DiffFieldStatus = 'unchanged'
    if (!hasPrev && hasCurr) {
      status = 'added'
    } else if (hasPrev && !hasCurr) {
      status = 'removed'
    } else {
      const prevJson = JSON.stringify(prevVal)
      const currJson = JSON.stringify(currVal)
      if (prevJson !== currJson) {
        status = 'changed'
      } else {
        status = 'unchanged'
      }
    }

    results.push({
      key,
      label: formatFieldLabel(key),
      previousValue: prevVal,
      currentValue: currVal,
      status,
      isMetadata: isMeta,
    })
  }

  return results
}

const renderValue = (
  val: unknown,
  token: ReturnType<typeof theme.useToken>['token'],
) => {
  if (val === undefined || val === null || val === '') {
    return (
      <Text type="secondary" italic>
        None
      </Text>
    )
  }

  if (typeof val === 'boolean') {
    return (
      <Tag color={val ? 'green' : 'default'} style={{ margin: 0 }}>
        {val ? 'Yes' : 'No'}
      </Tag>
    )
  }

  if (typeof val === 'object') {
    return (
      <Text code style={{ fontSize: token.fontSizeSM, wordBreak: 'break-all' }}>
        {JSON.stringify(val)}
      </Text>
    )
  }

  return (
    <Text style={{ fontSize: token.fontSizeSM, wordBreak: 'break-word' }}>
      {String(val)}
    </Text>
  )
}

export const ComparePayloadModal: FC<ComparePayloadModalProps> = ({
  open,
  onClose,
  currentActivity,
  previousActivity,
  allActivities = [],
}) => {
  const { token } = theme.useToken()
  const { message: messageApi } = App.useApp()

  const [viewMode, setViewMode] = useState<'visual' | 'raw'>('visual')
  const [showChangedOnly, setShowChangedOnly] = useState(true)
  const [includeMetadata, setIncludeMetadata] = useState(false)
  const [copiedPrev, setCopiedPrev] = useState(false)
  const [copiedCurr, setCopiedCurr] = useState(false)

  // Determine selectable earlier activities to compare against
  const earlierActivities = useMemo(() => {
    if (!currentActivity || !allActivities || allActivities.length === 0) {
      return previousActivity ? [previousActivity] : []
    }

    const currentIndex = allActivities.findIndex(
      (a) => a.id === currentActivity.id,
    )
    if (currentIndex >= 0) {
      return allActivities.slice(currentIndex + 1)
    }

    // Fallback: compare by timestamp
    const currTime = new Date(currentActivity.timestamp).getTime()
    return allActivities.filter(
      (a) => new Date(a.timestamp).getTime() < currTime,
    )
  }, [currentActivity, allActivities, previousActivity])

  const [selectedBaseId, setSelectedBaseId] = useState<string | null>(null)

  const activeBaseActivity = useMemo(() => {
    if (selectedBaseId) {
      const found = earlierActivities.find((a) => a.id === selectedBaseId)
      if (found) return found
    }
    if (previousActivity) return previousActivity
    if (earlierActivities.length > 0) return earlierActivities[0]
    return null
  }, [selectedBaseId, earlierActivities, previousActivity])

  const diffItems = useMemo(() => {
    if (!currentActivity || !activeBaseActivity) return []
    return computePayloadDiff(
      activeBaseActivity.payload,
      currentActivity.payload,
      includeMetadata,
    )
  }, [currentActivity, activeBaseActivity, includeMetadata])

  const filteredDiffItems = useMemo(() => {
    if (!showChangedOnly) return diffItems
    return diffItems.filter((item) => item.status !== 'unchanged')
  }, [diffItems, showChangedOnly])

  const stats = useMemo(() => {
    const changed = diffItems.filter((d) => d.status === 'changed').length
    const added = diffItems.filter((d) => d.status === 'added').length
    const removed = diffItems.filter((d) => d.status === 'removed').length
    const unchanged = diffItems.filter((d) => d.status === 'unchanged').length
    return {
      changed,
      added,
      removed,
      unchanged,
      totalChanges: changed + added + removed,
    }
  }, [diffItems])

  const handleCopy = (payload: unknown, isPrev: boolean) => {
    let str = ''
    try {
      const parsed = typeof payload === 'string' ? JSON.parse(payload) : payload
      str = JSON.stringify(parsed, null, 2)
    } catch {
      str = String(payload)
    }
    navigator.clipboard.writeText(str)
    if (isPrev) {
      setCopiedPrev(true)
      setTimeout(() => setCopiedPrev(false), 2000)
    } else {
      setCopiedCurr(true)
      setTimeout(() => setCopiedCurr(false), 2000)
    }
    messageApi.success('Payload copied to clipboard')
  }

  const renderStatusTag = (status: DiffFieldStatus) => {
    switch (status) {
      case 'changed':
        return (
          <Tag color="blue" style={{ margin: 0 }}>
            Modified
          </Tag>
        )
      case 'added':
        return (
          <Tag color="green" style={{ margin: 0 }}>
            Added
          </Tag>
        )
      case 'removed':
        return (
          <Tag color="red" style={{ margin: 0 }}>
            Removed
          </Tag>
        )
      case 'unchanged':
      default:
        return <Tag style={{ margin: 0 }}>Unchanged</Tag>
    }
  }

  const formatRawPayload = (payload: unknown): string => {
    if (!payload) return '{}'
    try {
      const parsed = typeof payload === 'string' ? JSON.parse(payload) : payload
      return JSON.stringify(parsed, null, 2)
    } catch {
      return String(payload)
    }
  }

  return (
    <Modal
      open={open}
      onCancel={onClose}
      width={920}
      title={
        <Flex align="center" gap="small">
          <DiffOutlined style={{ color: token.colorPrimary }} />
          <span>Compare Event Payloads</span>
        </Flex>
      }
      footer={[
        <Button key="close" type="primary" onClick={onClose}>
          Done
        </Button>,
      ]}
    >
      <Flex vertical gap="middle" style={{ marginTop: token.marginMD }}>
        {/* Context Bar: Base vs Target Event */}
        <Flex
          vertical
          gap="small"
          style={{
            padding: token.paddingSM,
            background: token.colorFillQuaternary,
            borderRadius: token.borderRadiusLG,
            border: `1px solid ${token.colorBorderSecondary}`,
          }}
        >
          {/* Unified Event Header */}
          <Flex align="center" gap="small" wrap="wrap">
            <Text strong style={{ fontSize: token.fontSize }}>
              {currentActivity?.summary || currentActivity?.eventType}
            </Text>
            <Tag color="blue" style={{ margin: 0 }}>
              {currentActivity?.eventType}
            </Tag>
            {activeBaseActivity &&
              (activeBaseActivity.summary !== currentActivity?.summary ||
                activeBaseActivity.eventType !==
                  currentActivity?.eventType) && (
                <>
                  <Text type="secondary" style={{ fontSize: token.fontSizeSM }}>
                    vs
                  </Text>
                  <Text strong style={{ fontSize: token.fontSize }}>
                    {activeBaseActivity.summary || activeBaseActivity.eventType}
                  </Text>
                  {activeBaseActivity.eventType !==
                    currentActivity?.eventType && (
                    <Tag style={{ margin: 0 }}>
                      {activeBaseActivity.eventType}
                    </Tag>
                  )}
                </>
              )}
          </Flex>

          <Flex gap="middle" wrap="wrap" align="center">
            {/* Base / Earlier Event */}
            <Flex
              vertical
              gap="xs"
              style={{ flex: '1 1 320px', minWidth: 260 }}
            >
              <Flex
                justify="space-between"
                align="center"
                style={{ minHeight: 24 }}
              >
                <Text
                  type="secondary"
                  strong
                  style={{ fontSize: token.fontSizeSM }}
                >
                  BASE (EARLIER EVENT):
                </Text>
                {earlierActivities.length > 1 && (
                  <Select
                    size="small"
                    style={{ minWidth: 200, maxWidth: 260 }}
                    value={activeBaseActivity?.id}
                    onChange={(val) => setSelectedBaseId(val)}
                    options={earlierActivities.map((act) => {
                      const timeStr = dayjs(act.timestamp).format(
                        'MMM D, h:mm:ss A',
                      )
                      const isDifferentType =
                        act.eventType !== currentActivity?.eventType &&
                        Boolean(act.summary || act.eventType)
                      return {
                        value: act.id,
                        label: isDifferentType
                          ? `${timeStr} (${act.summary || act.eventType})`
                          : timeStr,
                      }
                    })}
                  />
                )}
              </Flex>

              {activeBaseActivity ? (
                <Card size="small" variant="outlined">
                  <Flex justify="space-between" align="center">
                    <Text
                      type="secondary"
                      style={{ fontSize: token.fontSizeSM }}
                    >
                      {dayjs(activeBaseActivity.timestamp).format(
                        'MMM D, YYYY [at] h:mm:ss A',
                      )}
                    </Text>
                    <Text
                      type="secondary"
                      style={{ fontSize: token.fontSizeSM }}
                    >
                      By{' '}
                      <Text strong style={{ fontSize: token.fontSizeSM }}>
                        {activeBaseActivity.employee?.name ||
                          activeBaseActivity.actorKind}
                      </Text>
                    </Text>
                  </Flex>
                </Card>
              ) : (
                <Empty
                  image={Empty.PRESENTED_IMAGE_SIMPLE}
                  description="No earlier event selected"
                  style={{ margin: `${token.marginXS}px 0` }}
                />
              )}
            </Flex>

            <Flex align="center" justify="center" style={{ padding: '0 4px' }}>
              <ArrowRightOutlined
                style={{ fontSize: 18, color: token.colorTextSecondary }}
              />
            </Flex>

            {/* Current / Target Event */}
            <Flex
              vertical
              gap="xs"
              style={{ flex: '1 1 320px', minWidth: 260 }}
            >
              <Flex align="center" style={{ minHeight: 24 }}>
                <Text
                  type="secondary"
                  strong
                  style={{ fontSize: token.fontSizeSM }}
                >
                  TARGET (CURRENT EVENT):
                </Text>
              </Flex>
              {currentActivity ? (
                <Card size="small" variant="outlined">
                  <Flex justify="space-between" align="center">
                    <Text
                      type="secondary"
                      style={{ fontSize: token.fontSizeSM }}
                    >
                      {dayjs(currentActivity.timestamp).format(
                        'MMM D, YYYY [at] h:mm:ss A',
                      )}
                    </Text>
                    <Text
                      type="secondary"
                      style={{ fontSize: token.fontSizeSM }}
                    >
                      By{' '}
                      <Text strong style={{ fontSize: token.fontSizeSM }}>
                        {currentActivity.employee?.name ||
                          currentActivity.actorKind}
                      </Text>
                    </Text>
                  </Flex>
                </Card>
              ) : null}
            </Flex>
          </Flex>
        </Flex>

        {/* Toolbar & Filters */}
        <Flex justify="space-between" align="center" wrap="wrap" gap="small">
          <Flex align="center" gap="middle">
            <Segmented
              value={viewMode}
              onChange={(val) => setViewMode(val as 'visual' | 'raw')}
              options={[
                {
                  label: 'Property Changes',
                  value: 'visual',
                  icon: <DiffOutlined />,
                },
                { label: 'Raw JSON Diff', value: 'raw', icon: <EyeOutlined /> },
              ]}
            />
            {viewMode === 'visual' && (
              <Tag
                color={stats.totalChanges > 0 ? 'blue' : 'default'}
                style={{ margin: 0 }}
              >
                {stats.totalChanges > 0
                  ? `${stats.totalChanges} field${stats.totalChanges > 1 ? 's' : ''} modified`
                  : 'Identical domain payloads'}
              </Tag>
            )}
          </Flex>

          {viewMode === 'visual' && (
            <Flex align="center" gap="middle">
              <Flex align="center" gap="small">
                <Switch
                  size="small"
                  checked={showChangedOnly}
                  onChange={setShowChangedOnly}
                />
                <Text style={{ fontSize: token.fontSizeSM }}>
                  Show modified only
                </Text>
              </Flex>
              <Flex align="center" gap="small">
                <Switch
                  size="small"
                  checked={includeMetadata}
                  onChange={setIncludeMetadata}
                />
                <Text style={{ fontSize: token.fontSizeSM }}>
                  Include metadata
                </Text>
              </Flex>
            </Flex>
          )}
        </Flex>

        {/* View 1: Visual Table Diff */}
        {viewMode === 'visual' && (
          <div style={{ maxHeight: 420, overflowY: 'auto' }}>
            {filteredDiffItems.length > 0 ? (
              <table
                style={{
                  width: '100%',
                  borderCollapse: 'collapse',
                  border: `1px solid ${token.colorBorderSecondary}`,
                  borderRadius: token.borderRadius,
                  fontSize: token.fontSizeSM,
                }}
              >
                <thead
                  style={{
                    background: token.colorFillQuaternary,
                    position: 'sticky',
                    top: 0,
                    zIndex: 1,
                  }}
                >
                  <tr
                    style={{
                      borderBottom: `1px solid ${token.colorBorderSecondary}`,
                    }}
                  >
                    <th
                      style={{
                        textAlign: 'left',
                        padding: '8px 12px',
                        width: 180,
                      }}
                    >
                      Property
                    </th>
                    <th
                      style={{
                        textAlign: 'left',
                        padding: '8px 12px',
                        width: 110,
                      }}
                    >
                      Status
                    </th>
                    <th style={{ textAlign: 'left', padding: '8px 12px' }}>
                      Previous Value
                    </th>
                    <th style={{ textAlign: 'left', padding: '8px 12px' }}>
                      Current Value
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {filteredDiffItems.map((item) => (
                    <tr
                      key={item.key}
                      style={{
                        borderBottom: `1px solid ${token.colorBorderSecondary}`,
                        background: token.colorBgContainer,
                      }}
                    >
                      <td style={{ padding: '8px 12px', verticalAlign: 'top' }}>
                        <Flex vertical gap={2}>
                          <Text strong style={{ fontSize: token.fontSizeSM }}>
                            {item.label}
                          </Text>
                          <Text type="secondary" code style={{ fontSize: 11 }}>
                            {item.key}
                          </Text>
                        </Flex>
                      </td>
                      <td style={{ padding: '8px 12px', verticalAlign: 'top' }}>
                        {renderStatusTag(item.status)}
                      </td>
                      <td style={{ padding: '8px 12px', verticalAlign: 'top' }}>
                        <div
                          style={{
                            padding: '4px 8px',
                            borderRadius: token.borderRadiusSM,
                            background:
                              item.status === 'changed' ||
                              item.status === 'removed'
                                ? token.colorErrorBg
                                : undefined,
                            color:
                              item.status === 'changed' ||
                              item.status === 'removed'
                                ? token.colorErrorText
                                : undefined,
                          }}
                        >
                          {renderValue(item.previousValue, token)}
                        </div>
                      </td>
                      <td style={{ padding: '8px 12px', verticalAlign: 'top' }}>
                        <div
                          style={{
                            padding: '4px 8px',
                            borderRadius: token.borderRadiusSM,
                            background:
                              item.status === 'changed' ||
                              item.status === 'added'
                                ? token.colorSuccessBg
                                : undefined,
                            fontWeight:
                              item.status === 'changed' ||
                              item.status === 'added'
                                ? 600
                                : undefined,
                            color:
                              item.status === 'changed' ||
                              item.status === 'added'
                                ? token.colorSuccessText
                                : undefined,
                          }}
                        >
                          {renderValue(item.currentValue, token)}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <Card
                variant="outlined"
                style={{
                  textAlign: 'center',
                  padding: token.paddingLG,
                  borderRadius: token.borderRadiusLG,
                }}
              >
                <Empty
                  description={
                    showChangedOnly
                      ? 'No differences detected in domain fields between these two events.'
                      : 'No properties available to display.'
                  }
                />
                {showChangedOnly && diffItems.length > 0 && (
                  <Button
                    type="link"
                    size="small"
                    onClick={() => setShowChangedOnly(false)}
                    style={{ marginTop: token.marginXS }}
                  >
                    View all {diffItems.length} properties
                  </Button>
                )}
              </Card>
            )}
          </div>
        )}

        {/* View 2: Raw JSON Side-by-Side */}
        {viewMode === 'raw' && (
          <Flex gap="middle" style={{ minHeight: 320 }}>
            {/* Base JSON */}
            <Flex vertical gap="xs" style={{ flex: 1, minWidth: 0 }}>
              <Flex justify="space-between" align="center">
                <Text strong style={{ fontSize: token.fontSizeSM }}>
                  Previous Event Payload
                </Text>
                <Button
                  type="text"
                  size="small"
                  icon={
                    copiedPrev ? (
                      <CheckOutlined style={{ color: token.colorSuccess }} />
                    ) : (
                      <CopyOutlined />
                    )
                  }
                  onClick={() => handleCopy(activeBaseActivity?.payload, true)}
                >
                  Copy
                </Button>
              </Flex>
              <pre
                style={{
                  margin: 0,
                  padding: token.paddingSM,
                  borderRadius: token.borderRadius,
                  background: token.colorFillQuaternary,
                  border: `1px solid ${token.colorBorderSecondary}`,
                  maxHeight: 380,
                  overflow: 'auto',
                  fontSize: 12,
                  fontFamily: 'monospace',
                }}
              >
                {formatRawPayload(activeBaseActivity?.payload)}
              </pre>
            </Flex>

            {/* Current JSON */}
            <Flex vertical gap="xs" style={{ flex: 1, minWidth: 0 }}>
              <Flex justify="space-between" align="center">
                <Text strong style={{ fontSize: token.fontSizeSM }}>
                  Current Event Payload
                </Text>
                <Button
                  type="text"
                  size="small"
                  icon={
                    copiedCurr ? (
                      <CheckOutlined style={{ color: token.colorSuccess }} />
                    ) : (
                      <CopyOutlined />
                    )
                  }
                  onClick={() => handleCopy(currentActivity?.payload, false)}
                >
                  Copy
                </Button>
              </Flex>
              <pre
                style={{
                  margin: 0,
                  padding: token.paddingSM,
                  borderRadius: token.borderRadius,
                  background: token.colorFillQuaternary,
                  border: `1px solid ${token.colorBorderSecondary}`,
                  maxHeight: 380,
                  overflow: 'auto',
                  fontSize: 12,
                  fontFamily: 'monospace',
                }}
              >
                {formatRawPayload(currentActivity?.payload)}
              </pre>
            </Flex>
          </Flex>
        )}
      </Flex>
    </Modal>
  )
}

export default ComparePayloadModal

